using System.ComponentModel.DataAnnotations;

namespace NicaplusApi.DTOs.Clientes
{
    public class RestablecerPasswordDto
    {
        [Required]
        public string Token { get; set; } = string.Empty;

        [Required]
        [MinLength(6)]
        public string NuevaPassword { get; set; } = string.Empty;
    }
}