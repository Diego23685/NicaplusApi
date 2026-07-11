using System.ComponentModel.DataAnnotations;

namespace NicaplusApi.DTOs.Clientes
{
    public class ClienteActualizarPerfilDto
    {
        [Required]
        [StringLength(150)]
        public string Nombre { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string Telefono { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
    }
}