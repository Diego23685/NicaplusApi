using System.ComponentModel.DataAnnotations;

namespace NicaplusApi.DTOs.Clientes
{
    public class SolicitarRecuperacionDto
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
    }
}