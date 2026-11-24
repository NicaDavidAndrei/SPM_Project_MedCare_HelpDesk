using MedCare_HelpDesk.Models;
using MedCare_HelpDesk.Repositories.Interfaces;
using MedCare_HelpDesk.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace MedCare_HelpDesk.Services
{
    public class DepartmentService : IDepartmentService
    {
        private readonly IRepositoryWrapper _wrapper;
        private readonly HelpdeskContext _context;

        public DepartmentService(IRepositoryWrapper wrapper, HelpdeskContext context)
        {
            _wrapper = wrapper;
            _context = context;
        }

        public IEnumerable<Department> GetAllDepartments(string? searchString = null, bool activeOnly = false)
        {
            var query = _wrapper.Department.FindAll();
            if (activeOnly) query = query.Where(d => d.IsActive);

            if (!string.IsNullOrWhiteSpace(searchString))
            {
                string term = searchString.Trim().ToLower();
                query = query.Where(d =>
                    (d.Name != null && d.Name.ToLower().Contains(term)) ||
                    (d.WardCode != null && d.WardCode.ToLower().Contains(term)) ||
                    d.Id.ToString() == term);
            }

            return query.OrderByDescending(d => d.IsCriticalCare).ThenBy(d => d.Name).ToList();
        }

        public Department? GetDepartmentById(int id) =>
            _wrapper.Department.FindByCondition(d => d.Id == id).FirstOrDefault();

        public void CreateDepartment(Department department)
        {
            _wrapper.Department.Create(department);
            _wrapper.Save();
        }

        public void UpdateDepartment(Department department)
        {
            _wrapper.Department.Update(department);
            _wrapper.Save();
        }

        /// <summary>
        /// FR-06: Soft-deactivates hospital wards referenced by users or tickets; hard-deletes only unreferenced wards.
        /// </summary>
        public bool DeleteOrDeactivateDepartment(int id)
        {
            var department = _context.Departments.FirstOrDefault(d => d.Id == id);
            if (department == null) return false;

            bool hasUsersOrTickets = _context.Users.Any(u => u.DepartmentId == id)
                                  || _context.Tickets.Any(t => t.DepartmentId == id);
            if (hasUsersOrTickets)
            {
                department.IsActive = false;
                _context.SaveChanges();
                return false; // Soft-deactivated
            }

            _context.Departments.Remove(department);
            _context.SaveChanges();
            return true; // Hard-deleted
        }

        public bool DepartmentExists(int id) =>
            _wrapper.Department.FindByCondition(d => d.Id == id).Any();
    }

    public class CategoryService : ICategoryService
    {
        private readonly IRepositoryWrapper _wrapper;
        private readonly HelpdeskContext _context;

        public CategoryService(IRepositoryWrapper wrapper, HelpdeskContext context)
        {
            _wrapper = wrapper;
            _context = context;
        }

        public IEnumerable<Category> GetAllCategories(string? searchString = null, bool activeOnly = false)
        {
            var query = _wrapper.Category.FindAll();
            if (activeOnly) query = query.Where(c => c.IsActive);

            if (!string.IsNullOrWhiteSpace(searchString))
            {
                string term = searchString.Trim().ToLower();
                query = query.Where(c =>
                    (c.Name != null && c.Name.ToLower().Contains(term)) ||
                    (c.SpecialtyTag != null && c.SpecialtyTag.ToLower().Contains(term)) ||
                    c.Id.ToString() == term);
            }

            return query.OrderBy(c => c.Name).ToList();
        }

        public Category? GetCategoryById(int id) =>
            _wrapper.Category.FindByCondition(c => c.Id == id).FirstOrDefault();

        public void CreateCategory(Category category)
        {
            _wrapper.Category.Create(category);
            _wrapper.Save();
        }

        public void UpdateCategory(Category category)
        {
            _wrapper.Category.Update(category);
            _wrapper.Save();
        }

        public bool DeleteOrDeactivateCategory(int id)
        {
            var category = _context.Categories.FirstOrDefault(c => c.Id == id);
            if (category == null) return false;

            if (_context.Tickets.Any(t => t.CategoryId == id))
            {
                category.IsActive = false;
                _context.SaveChanges();
                return false;
            }

            _context.Categories.Remove(category);
            _context.SaveChanges();
            return true;
        }

        public bool CategoryExists(int id) =>
            _wrapper.Category.FindByCondition(c => c.Id == id).Any();
    }

    public class KnowledgeBaseService : IKnowledgeBaseService
    {
        private readonly IRepositoryWrapper _wrapper;
        public KnowledgeBaseService(IRepositoryWrapper wrapper) => _wrapper = wrapper;

        public async Task<IEnumerable<KnowledgeArticle>> GetAllArticlesAsync() =>
            await _wrapper.KnowledgeBase.FindAll().OrderByDescending(a => a.UpdatedAt).ToListAsync();

        public async Task<KnowledgeArticle?> GetArticleByIdAsync(int id) =>
            await _wrapper.KnowledgeBase.GetArticleByIdAsync(id);

        public async Task CreateArticleAsync(KnowledgeArticle article)
        {
            article.UpdatedAt = DateTime.UtcNow;
            _wrapper.KnowledgeBase.Create(article);
            await _wrapper.SaveAsync();
        }

        public async Task UpdateArticleAsync(KnowledgeArticle article)
        {
            article.UpdatedAt = DateTime.UtcNow;
            _wrapper.KnowledgeBase.Update(article);
            await _wrapper.SaveAsync();
        }

        public async Task DeleteArticleAsync(int id)
        {
            var article = await _wrapper.KnowledgeBase.GetArticleByIdAsync(id);
            if (article != null)
            {
                _wrapper.KnowledgeBase.Delete(article);
                await _wrapper.SaveAsync();
            }
        }

        public bool ArticleExists(int id) => _wrapper.KnowledgeBase.ArticleExists(id);

        public async Task<IEnumerable<KnowledgeArticle>> GetFilteredArticlesAsync(string? searchString, string? category)
        {
            var query = _wrapper.KnowledgeBase.FindAll();

            if (!string.IsNullOrWhiteSpace(category))
            {
                query = query.Where(a => a.Category == category);
            }

            if (!string.IsNullOrWhiteSpace(searchString))
            {
                string term = searchString.Trim().ToLower();
                query = query.Where(a =>
                    (a.Title != null && a.Title.ToLower().Contains(term)) ||
                    (a.Content != null && a.Content.ToLower().Contains(term)) ||
                    (a.Category != null && a.Category.ToLower().Contains(term)));
            }

            return await query.OrderByDescending(a => a.UpdatedAt).ToListAsync();
        }
    }
}
