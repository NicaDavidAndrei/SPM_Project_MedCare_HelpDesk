using System.ComponentModel.DataAnnotations;

namespace MedCare_HelpDesk.Models
{
    /// <summary>
    /// Represents a hospital ward/department (e.g., ATI / ICU, Emergency / UPU, Surgery).
    /// </summary>
    public class Department
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Ward name is required.")]
        [StringLength(100)]
        public string? Name { get; set; }

        [StringLength(15)]
        public string? WardCode { get; set; }

        [StringLength(30)]
        public string? WardPhoneExtension { get; set; }

        public bool IsCriticalCare { get; set; } = false;

        public bool IsActive { get; set; } = true;

        public ICollection<User> Users { get; set; } = new List<User>();
        public ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
    }
}
