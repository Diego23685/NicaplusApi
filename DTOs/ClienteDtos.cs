using System.ComponentModel.DataAnnotations;

namespace NicaplusApi.DTOs
{
    public class CrearClienteDto
    {
        [Required(ErrorMessage = "El nombre es obligatorio.")]
        [StringLength(150)]
        public string Nombre { get; set; } = null!;

        [Required(ErrorMessage = "El teléfono es obligatorio.")]
        [StringLength(20)]
        public string Telefono { get; set; } = null!;

        [StringLength(150)]
        [EmailAddress(ErrorMessage = "El formato de correo no es válido.")]
        public string? Email { get; set; }

        public string? Observaciones { get; set; }
        public string? Etiquetas { get; set; }
    }

    public class ClienteResponseDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = null!;
        public string Telefono { get; set; } = null!;
        public string Email { get; set; } = null!;
        public DateTime FechaRegistro { get; set; }
        public string Observaciones { get; set; } = null!;
        public string Etiquetas { get; set; } = null!;
        public int PuntosAcumulados { get; set; }
        public bool Activo { get; set; }
        public DateTime? UltimoAcceso { get; set; }
    }
}