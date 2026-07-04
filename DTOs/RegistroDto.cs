using System.ComponentModel.DataAnnotations;

namespace NicaplusApi.DTOs
{
    public class RegistroDto
    {
        [Required]
        public string Nombre { get; set; } = string.Empty;

        [Required]
        public string Username { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;

        [Required]
        public int IdRol { get; set; }
    }
}