using MedCare_HelpDesk.Models;
using MedCare_HelpDesk.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace MedCare_HelpDesk.Repositories
{
    public class TicketRepository : RepositoryBase<Ticket>, ITicketRepository
    {
        public TicketRepository(HelpdeskContext context) : base(context) { }

        public IQueryable<Ticket> GetQueryableTicketsWithDetails() =>
            FindAll()
                .Include(t => t.AssignedAgent)
                .Include(t => t.Category)
                .Include(t => t.Department)
                .Include(t => t.Requester)
                    .ThenInclude(u => u!.Department)
                .Include(t => t.Attachments)
                .Include(t => t.Comments)
                    .ThenInclude(c => c.User)
                .Include(t => t.Comments)
                    .ThenInclude(c => c.Attachments);

        public async Task<Ticket?> GetTicketByIdAsync(int id) =>
            await GetQueryableTicketsWithDetails().FirstOrDefaultAsync(t => t.Id == id);

        public bool TicketExists(int id) => FindByCondition(t => t.Id == id).Any();
    }

    public class DepartmentRepository : RepositoryBase<Department>, IDepartmentRepository
    {
        public DepartmentRepository(HelpdeskContext context) : base(context) { }
    }

    public class CategoryRepository : RepositoryBase<Category>, ICategoryRepository
    {
        public CategoryRepository(HelpdeskContext context) : base(context) { }
    }

    public class CommentRepository : RepositoryBase<Comment>, ICommentRepository
    {
        public CommentRepository(HelpdeskContext context) : base(context) { }
    }

    public class AttachmentRepository : RepositoryBase<Attachment>, IAttachmentRepository
    {
        public AttachmentRepository(HelpdeskContext context) : base(context) { }
    }

    public class NotificationRepository : RepositoryBase<TicketNotification>, INotificationRepository
    {
        public NotificationRepository(HelpdeskContext context) : base(context) { }
    }

    public class KnowledgeBaseRepository : RepositoryBase<KnowledgeArticle>, IKnowledgeBaseRepository
    {
        public KnowledgeBaseRepository(HelpdeskContext context) : base(context) { }

        public async Task<KnowledgeArticle?> GetArticleByIdAsync(int id) =>
            await FindByCondition(a => a.Id == id).FirstOrDefaultAsync();

        public bool ArticleExists(int id) => FindByCondition(a => a.Id == id).Any();
    }
}
