using System.ComponentModel.DataAnnotations;

namespace MedCare_HelpDesk.Models
{
    /// <summary>
    /// Represents a hospital problem category mapped to target technician specialty.
    /// </summary>
    public class Category
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Category name is required.")]
        [StringLength(100)]
        public string? Name { get; set; }

        [StringLength(250)]
        public string? Description { get; set; }

        [StringLength(80)]
        public string? SpecialtyTag { get; set; }

        [StringLength(50)]
        public string? IconClass { get; set; }

        public bool IsActive { get; set; } = true;

        public ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
    }
}
