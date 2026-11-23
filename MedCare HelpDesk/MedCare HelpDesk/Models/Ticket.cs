using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MedCare_HelpDesk.Models
{
    /// <summary>
    /// Hospital incident/support ticket with 24/7 SLA deadline tracking (2h/4h/12h/24h).
    /// </summary>
    public class Ticket
    {
        [Key]
        public int Id { get; set; }

        [StringLength(24)]
        public string? TicketCode { get; set; } // e.g., TKT-2026-0001

        [Required]
        [StringLength(200)]
        public string? Title { get; set; }

        [Required]
        public string? Description { get; set; }

        [StringLength(80)]
        public string? EquipmentTag { get; set; }

        [Required]
        [StringLength(20)]
        public string? Status { get; set; } // Open, In Progress, Pending Info, Resolved, Closed

        [Required]
        [StringLength(20)]
        public string? Priority { get; set; } // Critical (2h), High (4h), Medium (12h), Low (24h)

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? DueDate { get; set; }

        public DateTime? ResolvedAt { get; set; }

        public string? ResolutionSummary { get; set; }

        public int CategoryId { get; set; }
        [ForeignKey("CategoryId")]
        public Category? Category { get; set; }

        public int? DepartmentId { get; set; }
        [ForeignKey("DepartmentId")]
        public Department? Department { get; set; }

        public int? RequesterId { get; set; }
        [ForeignKey("RequesterId")]
        public User? Requester { get; set; }

        public int? AssignedAgentId { get; set; }
        [ForeignKey("AssignedAgentId")]
        public User? AssignedAgent { get; set; }

        public ICollection<Comment> Comments { get; set; } = new List<Comment>();
        public ICollection<Attachment> Attachments { get; set; } = new List<Attachment>();
        public ICollection<TicketNotification> Notifications { get; set; } = new List<TicketNotification>();
    }
}
