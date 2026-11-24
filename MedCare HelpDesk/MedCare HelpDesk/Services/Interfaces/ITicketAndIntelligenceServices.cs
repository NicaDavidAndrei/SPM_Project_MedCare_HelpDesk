using MedCare_HelpDesk.Models;
using MedCare_HelpDesk.Models.ML;
using Microsoft.AspNetCore.Http;

namespace MedCare_HelpDesk.Services.Interfaces
{
    public record SlaHealthInfo(bool IsResolved, bool IsBreached, bool IsApproaching, double RemainingRatio, TimeSpan? RemainingTime);

    public interface ITicketService
    {
        Task<IEnumerable<Ticket>> GetAllTicketsAsync();
        Task<Ticket?> GetTicketDetailsAsync(int id);
        DateTime CalculateDueDate(string? priority, DateTime createdAt);
        SlaHealthInfo EvaluateSlaHealth(Ticket ticket, DateTime? utcNow = null);
        Task CreateTicketAsync(Ticket ticket, bool isUserSubmission = false);
        Task UpdateTicketAsync(Ticket ticket);
        Task<(bool Success, string? ErrorMessage)> UpdateTicketWorkflowAsync(int ticketId, string status, string priority, int? assignedAgentId, string? resolutionSummary, int actorUserId);
        Task<bool> ClaimTicketAsync(int ticketId, int agentId);
        Task DeleteTicketAsync(int id);
        bool TicketExists(int id);
        Task<IEnumerable<Ticket>> GetFilteredTicketsAsync(int? requesterId, int? wardId, string? searchString, string? statusFilter, string? priorityFilter, int? departmentFilter = null, string? slaFilter = null);
        Task AddCommentAsync(Comment comment);
        Task AddAttachmentAsync(Attachment attachment);
        (bool IsValid, string? ErrorMessage) ValidateUploadFile(IFormFile file, bool imagesOnly = false);
        Task<(List<string> UploadedPaths, string? ErrorMessage)> ProcessAttachmentsAsync(int ticketId, IEnumerable<IFormFile> files, int? commentId = null);
        Task<IEnumerable<User>> GetSupportStaffAsync();
        Task<IEnumerable<User>> GetAllUsersAsync();
        Task<int> CalculateAgentWorkloadScoreAsync(int agentId);
        Task<Dictionary<int, int>> GetStaffWorkloadScoresAsync();
        Task<User?> GetLeastLoadedAgentAsync(int? categoryId = null);
        Task CreateNotificationAsync(int userId, int ticketId, string message);
        Task<List<TicketNotification>> GetUnreadNotificationsAsync(int userId);
        Task MarkNotificationAsReadAsync(int notificationId, int userId);
        Task MarkTicketNotificationsReadAsync(int ticketId, int userId);
    }

    public interface IRecommendationService
    {
        Task<List<ArticleRecommendationDto>> GetRecommendedArticlesAsync(string text, int topK = 3);
        Task<List<SimilarTicketDto>> GetSimilarResolvedTicketsAsync(int ticketId, int topK = 3, int? requesterId = null, int? wardId = null);
    }

    public interface ITicketPredictionService
    {
        Task TrainModelFromDatabaseAsync();
        TicketPredictionOutput PredictMetadata(string title, string desc);
    }
}
