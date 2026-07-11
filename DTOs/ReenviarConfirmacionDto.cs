using System.ComponentModel.DataAnnotations;

namespace NicaplusApi.DTOs.Clientes
{
    public class ReenviarConfirmacionDto
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
    }
}