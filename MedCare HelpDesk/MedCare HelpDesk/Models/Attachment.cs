using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace MedCare_HelpDesk.Models
{
    public class Attachment
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(255)]
        public string FileName { get; set; } = default!;

        [Required]
        public string FilePath { get; set; } = default!;

        public long FileSizeBytes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public int TicketId { get; set; }

        [ValidateNever]
        [JsonIgnore]
        [ForeignKey("TicketId")]
        public Ticket? Ticket { get; set; }

        public int? CommentId { get; set; }

        [ValidateNever]
        [JsonIgnore]
        [ForeignKey("CommentId")]
        public Comment? Comment { get; set; }
    }
}
