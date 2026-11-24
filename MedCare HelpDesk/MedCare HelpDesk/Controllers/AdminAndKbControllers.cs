using MedCare_HelpDesk.Models;
using MedCare_HelpDesk.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MedCare_HelpDesk.Controllers
{
    [Authorize(Roles = "Admin")]
    public class DepartmentsController : Controller
    {
        private readonly IDepartmentService _departmentService;
        public DepartmentsController(IDepartmentService departmentService) => _departmentService = departmentService;

        public IActionResult Index(string? searchString)
        {
            ViewData["CurrentFilter"] = searchString;
            return View(_departmentService.GetAllDepartments(searchString));
        }

        public IActionResult Details(int? id)
        {
            if (id == null) return NotFound();
            var department = _departmentService.GetDepartmentById(id.Value);
            return department == null ? NotFound() : View(department);
        }

        public IActionResult Create() => View(new Department { IsActive = true });

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create([Bind("Id,Name,WardCode,WardPhoneExtension,IsCriticalCare,IsActive")] Department department)
        {
            if (ModelState.IsValid)
            {
                _departmentService.CreateDepartment(department);
                TempData["SuccessMessage"] = "Hospital ward created.";
                return RedirectToAction(nameof(Index));
            }
            return View(department);
        }

        public IActionResult Edit(int? id)
        {
            if (id == null) return NotFound();
            var department = _departmentService.GetDepartmentById(id.Value);
            return department == null ? NotFound() : View(department);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(int id, [Bind("Id,Name,WardCode,WardPhoneExtension,IsCriticalCare,IsActive")] Department department)
        {
            if (id != department.Id) return NotFound();
            if (ModelState.IsValid)
            {
                try
                {
                    _departmentService.UpdateDepartment(department);
                    TempData["SuccessMessage"] = "Hospital ward updated.";
                }
                catch
                {
                    if (!_departmentService.DepartmentExists(department.Id)) return NotFound();
                    throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(department);
        }

        public IActionResult Delete(int? id)
        {
            if (id == null) return NotFound();
            var department = _departmentService.GetDepartmentById(id.Value);
            return department == null ? NotFound() : View(department);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(int id)
        {
            bool hardDeleted = _departmentService.DeleteOrDeactivateDepartment(id);
            TempData["SuccessMessage"] = hardDeleted
                ? "Hospital ward deleted."
                : "Ward has existing staff/tickets and was soft-deactivated (FR-06).";
            return RedirectToAction(nameof(Index));
        }
    }

    [Authorize(Roles = "Admin")]
    public class CategoriesController : Controller
    {
        private readonly ICategoryService _categoryService;
        public CategoriesController(ICategoryService categoryService) => _categoryService = categoryService;

        public IActionResult Index(string? searchString)
        {
            ViewData["CurrentFilter"] = searchString;
            return View(_categoryService.GetAllCategories(searchString));
        }

        public IActionResult Details(int? id)
        {
            if (id == null) return NotFound();
            var category = _categoryService.GetCategoryById(id.Value);
            return category == null ? NotFound() : View(category);
        }

        public IActionResult Create() => View(new Category { IsActive = true, IconClass = "bi-tag" });

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create([Bind("Id,Name,Description,SpecialtyTag,IconClass,IsActive")] Category category)
        {
            if (ModelState.IsValid)
            {
                _categoryService.CreateCategory(category);
                TempData["SuccessMessage"] = "Problem category created.";
                return RedirectToAction(nameof(Index));
            }
            return View(category);
        }

        public IActionResult Edit(int? id)
        {
            if (id == null) return NotFound();
            var category = _categoryService.GetCategoryById(id.Value);
            return category == null ? NotFound() : View(category);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(int id, [Bind("Id,Name,Description,SpecialtyTag,IconClass,IsActive")] Category category)
        {
            if (id != category.Id) return NotFound();
            if (ModelState.IsValid)
            {
                try
                {
                    _categoryService.UpdateCategory(category);
                    TempData["SuccessMessage"] = "Problem category updated.";
                }
                catch
                {
                    if (!_categoryService.CategoryExists(category.Id)) return NotFound();
                    throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(category);
        }

        public IActionResult Delete(int? id)
        {
            if (id == null) return NotFound();
            var category = _categoryService.GetCategoryById(id.Value);
            return category == null ? NotFound() : View(category);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(int id)
        {
            bool hardDeleted = _categoryService.DeleteOrDeactivateCategory(id);
            TempData["SuccessMessage"] = hardDeleted
                ? "Category deleted."
                : "Category is linked to existing tickets and was soft-deactivated.";
            return RedirectToAction(nameof(Index));
        }
    }

    [Authorize(Roles = "Admin,Agent")]
    public class KnowledgeArticlesController : Controller
    {
        private readonly IKnowledgeBaseService _knowledgeArticleService;
        private readonly UserManager<User> _userManager;

        public KnowledgeArticlesController(IKnowledgeBaseService knowledgeArticleService, UserManager<User> userManager)
        {
            _knowledgeArticleService = knowledgeArticleService;
            _userManager = userManager;
        }

        private bool CanManageArticle(KnowledgeArticle article, int? currentUserId) =>
            User.IsInRole("Admin") || (currentUserId.HasValue && article.AuthorId == currentUserId.Value);

        public async Task<IActionResult> Index(string? searchString, string? categoryFilter)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            ViewBag.CurrentUserId = currentUser?.Id;
            ViewBag.IsAdmin = User.IsInRole("Admin");
            ViewData["CurrentSearch"] = searchString;
            ViewData["CategoryFilter"] = categoryFilter;
            return View(await _knowledgeArticleService.GetFilteredArticlesAsync(searchString, categoryFilter));
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var article = await _knowledgeArticleService.GetArticleByIdAsync(id.Value);
            if (article == null) return NotFound();
            var currentUser = await _userManager.GetUserAsync(User);
            ViewBag.CanManage = CanManageArticle(article, currentUser?.Id);
            return View(article);
        }

        public async Task<IActionResult> Create()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            return View(new KnowledgeArticle
            {
                IconClass = "bi-journal-medical",
                ColorClass = "text-primary",
                AuthorName = currentUser != null ? $"{currentUser.FirstName} {currentUser.LastName}".Trim() : "MedCare Team"
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Title,Content,Category,IconClass,ColorClass,AuthorName")] KnowledgeArticle knowledgeArticle)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (ModelState.IsValid)
            {
                knowledgeArticle.AuthorId = currentUser?.Id;
                if (!User.IsInRole("Admin") || string.IsNullOrWhiteSpace(knowledgeArticle.AuthorName))
                {
                    knowledgeArticle.AuthorName = currentUser != null
                        ? $"{currentUser.FirstName} {currentUser.LastName}".Trim()
                        : (User.Identity?.Name ?? "MedCare Team");
                }
                await _knowledgeArticleService.CreateArticleAsync(knowledgeArticle);
                TempData["SuccessMessage"] = "Knowledge Base article published.";
                return RedirectToAction(nameof(Index));
            }
            return View(knowledgeArticle);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var article = await _knowledgeArticleService.GetArticleByIdAsync(id.Value);
            if (article == null) return NotFound();

            var currentUser = await _userManager.GetUserAsync(User);
            if (!CanManageArticle(article, currentUser?.Id)) return Forbid();

            return View(article);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Title,Content,Category,IconClass,ColorClass,AuthorName")] KnowledgeArticle knowledgeArticle)
        {
            if (id != knowledgeArticle.Id) return NotFound();

            var existing = await _knowledgeArticleService.GetArticleByIdAsync(id);
            if (existing == null) return NotFound();

            var currentUser = await _userManager.GetUserAsync(User);
            if (!CanManageArticle(existing, currentUser?.Id)) return Forbid();

            if (ModelState.IsValid)
            {
                try
                {
                    // Preserve ownership so Agents cannot hijack or forge article authors
                    knowledgeArticle.AuthorId = existing.AuthorId;
                    if (!User.IsInRole("Admin") || string.IsNullOrWhiteSpace(knowledgeArticle.AuthorName))
                    {
                        knowledgeArticle.AuthorName = existing.AuthorName;
                    }
                    await _knowledgeArticleService.UpdateArticleAsync(knowledgeArticle);
                    TempData["SuccessMessage"] = "Knowledge Base article updated.";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_knowledgeArticleService.ArticleExists(knowledgeArticle.Id)) return NotFound();
                    throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(knowledgeArticle);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var article = await _knowledgeArticleService.GetArticleByIdAsync(id.Value);
            if (article == null) return NotFound();

            var currentUser = await _userManager.GetUserAsync(User);
            if (!CanManageArticle(article, currentUser?.Id)) return Forbid();

            return View(article);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var existing = await _knowledgeArticleService.GetArticleByIdAsync(id);
            if (existing == null) return NotFound();

            var currentUser = await _userManager.GetUserAsync(User);
            if (!CanManageArticle(existing, currentUser?.Id)) return Forbid();

            await _knowledgeArticleService.DeleteArticleAsync(id);
            TempData["SuccessMessage"] = "Knowledge Base article deleted.";
            return RedirectToAction(nameof(Index));
        }
    }

    public class KnowledgeBaseController : Controller
    {
        private readonly IKnowledgeBaseService _knowledgeBaseService;
        private readonly ICategoryService _categoryService;

        public KnowledgeBaseController(IKnowledgeBaseService knowledgeBaseService, ICategoryService categoryService)
        {
            _knowledgeBaseService = knowledgeBaseService;
            _categoryService = categoryService;
        }

        [AllowAnonymous]
        public async Task<IActionResult> Index(string? searchString, string? category)
        {
            var articles = await _knowledgeBaseService.GetFilteredArticlesAsync(searchString, category);
            ViewData["CurrentSearch"] = searchString;
            ViewData["CurrentCategory"] = category;
            ViewBag.CategoryIds = _categoryService.GetAllCategories(activeOnly: true)
                .ToDictionary(c => c.Name!, c => c.Id);
            return View(articles);
        }
    }
}
