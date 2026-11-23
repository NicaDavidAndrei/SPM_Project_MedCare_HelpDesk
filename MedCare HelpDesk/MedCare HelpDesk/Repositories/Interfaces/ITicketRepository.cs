using MedCare_HelpDesk.Models;

namespace MedCare_HelpDesk.Repositories.Interfaces
{
    public interface ITicketRepository : IRepositoryBase<Ticket>
    {
        IQueryable<Ticket> GetQueryableTicketsWithDetails();
        Task<Ticket?> GetTicketByIdAsync(int id);
        bool TicketExists(int id);
    }
}
