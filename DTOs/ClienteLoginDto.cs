using System.ComponentModel.DataAnnotations;

namespace NicaplusApi.DTOs.Clientes
{
    public class ClienteLoginDto
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;
    }
}