using System.ComponentModel.DataAnnotations;

namespace MedCare_HelpDesk.Models
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Institutional email is required.")]
        [EmailAddress]
        public string? Email { get; set; }

        [Required(ErrorMessage = "Password is required.")]
        [DataType(DataType.Password)]
        public string? Password { get; set; }

        public bool RememberMe { get; set; }
    }
}
