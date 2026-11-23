using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MedCare_HelpDesk.Models
{
    public class KnowledgeArticle
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "The Article Title is required.")]
        [StringLength(255)]
        public string? Title { get; set; }

        [Required(ErrorMessage = "The Content field cannot be empty.")]
        public string? Content { get; set; }

        [Required(ErrorMessage = "Please select a Category.")]
        [StringLength(100)]
        public string? Category { get; set; }

        [StringLength(50)]
        public string? IconClass { get; set; } = "bi-journal-medical";

        [StringLength(50)]
        public string? ColorClass { get; set; } = "text-primary";

        public int? AuthorId { get; set; }
        [ForeignKey("AuthorId")]
        public User? Author { get; set; }

        [StringLength(100)]
        public string? AuthorName { get; set; } = "MedCare Technical Team";

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
