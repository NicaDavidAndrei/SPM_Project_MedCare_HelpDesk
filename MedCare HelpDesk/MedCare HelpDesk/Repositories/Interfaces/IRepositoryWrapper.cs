namespace MedCare_HelpDesk.Repositories.Interfaces
{
    public interface IRepositoryWrapper
    {
        ITicketRepository Ticket { get; }
        IDepartmentRepository Department { get; }
        ICategoryRepository Category { get; }
        IKnowledgeBaseRepository KnowledgeBase { get; }
        ICommentRepository Comment { get; }
        IAttachmentRepository Attachment { get; }
        INotificationRepository Notification { get; }
        void Save();
        Task SaveAsync();
    }
}
