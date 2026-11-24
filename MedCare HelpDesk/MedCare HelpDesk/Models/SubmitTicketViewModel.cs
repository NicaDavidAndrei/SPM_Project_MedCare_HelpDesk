using System.ComponentModel.DataAnnotations;

namespace MedCare_HelpDesk.Models
{
    public class SubmitTicketViewModel
    {
        [Required(ErrorMessage = "Please provide a brief issue title.")]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please select a hospital ward.")]
        public int DepartmentId { get; set; }

        [Required(ErrorMessage = "Please select a problem category.")]
        public int CategoryId { get; set; }

        [Required]
        public string Priority { get; set; } = "Medium";

        [StringLength(80)]
        public string? EquipmentTag { get; set; }

        [Required(ErrorMessage = "Please provide technical details (no patient PII).")]
        public string Description { get; set; } = string.Empty;
    }
}
