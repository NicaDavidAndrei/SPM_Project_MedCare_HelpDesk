using MedCare_HelpDesk.Models;
using Microsoft.AspNetCore.Identity;

namespace MedCare_HelpDesk.Services.Interfaces
{
    public interface IUserService
    {
        Task<IEnumerable<User>> GetFilteredUsersAsync(string? searchString);
        Task<User?> GetUserByIdWithDepartmentAsync(int id);
        Task<IEnumerable<string>> GetUserRolesAsync(User user);
        Task<IEnumerable<string>> GetAllRolesAsync();
        Task<IdentityResult> CreateUserWithRoleAsync(User user, string password, string role);
        Task<IdentityResult> UpdateUserWithRoleAsync(User formUser, string selectedRole, string? newPassword);
        Task<IdentityResult> ToggleUserActiveAsync(int id);
        Task<IdentityResult> DeleteUserAsync(int id);
    }

    public interface IDepartmentService
    {
        IEnumerable<Department> GetAllDepartments(string? searchString = null, bool activeOnly = false);
        Department? GetDepartmentById(int id);
        void CreateDepartment(Department department);
        void UpdateDepartment(Department department);
        bool DeleteOrDeactivateDepartment(int id);
        bool DepartmentExists(int id);
    }

    public interface ICategoryService
    {
        IEnumerable<Category> GetAllCategories(string? searchString = null, bool activeOnly = false);
        Category? GetCategoryById(int id);
        void CreateCategory(Category category);
        void UpdateCategory(Category category);
        bool DeleteOrDeactivateCategory(int id);
        bool CategoryExists(int id);
    }

    public interface IKnowledgeBaseService
    {
        Task<IEnumerable<KnowledgeArticle>> GetAllArticlesAsync();
        Task<KnowledgeArticle?> GetArticleByIdAsync(int id);
        Task CreateArticleAsync(KnowledgeArticle article);
        Task UpdateArticleAsync(KnowledgeArticle article);
        Task DeleteArticleAsync(int id);
        bool ArticleExists(int id);
        Task<IEnumerable<KnowledgeArticle>> GetFilteredArticlesAsync(string? searchString, string? category);
    }
}
