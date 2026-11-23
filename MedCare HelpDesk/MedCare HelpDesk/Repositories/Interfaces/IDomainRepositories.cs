using MedCare_HelpDesk.Models;

namespace MedCare_HelpDesk.Repositories.Interfaces
{
    public interface IDepartmentRepository : IRepositoryBase<Department> { }
    public interface ICategoryRepository : IRepositoryBase<Category> { }
    public interface ICommentRepository : IRepositoryBase<Comment> { }
    public interface IAttachmentRepository : IRepositoryBase<Attachment> { }
    public interface INotificationRepository : IRepositoryBase<TicketNotification> { }

    public interface IKnowledgeBaseRepository : IRepositoryBase<KnowledgeArticle>
    {
        Task<KnowledgeArticle?> GetArticleByIdAsync(int id);
        bool ArticleExists(int id);
    }
}
