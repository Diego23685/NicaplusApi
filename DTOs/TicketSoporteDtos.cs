using System.ComponentModel.DataAnnotations;

namespace NicaplusApi.DTOs
{
    public class CrearTicketDto
    {
        [Required(ErrorMessage = "El cliente es obligatorio.")]
        public int IdCliente { get; set; }

        [Required(ErrorMessage = "El tipo de ticket es obligatorio.")]
        [StringLength(50)]
        public string TipoTicket { get; set; } = string.Empty; // Ej: Garantía, Caída de Cuenta, Soporte Técnico

        [Required(ErrorMessage = "La descripción del fallo es obligatoria.")]
        public string DescripcionFalla { get; set; } = string.Empty;
    }

    public class CambiarEstadoTicketDto
    {
        [Required(ErrorMessage = "El nuevo estado es obligatorio.")]
        public string NuevoEstado { get; set; } = string.Empty;

        public string? NotasResolucion { get; set; }
    }

    public class TicketSoporteResponseDto
    {
        public int Id { get; set; }
        public int IdCliente { get; set; }
        public string TipoTicket { get; set; } = string.Empty;
        public string DescripcionFalla { get; set; } = string.Empty;
        public string Estado { get; set; } = string.Empty;
        public DateTime FechaCreacion { get; set; }
        public DateTime? FechaResolucion { get; set; }
        public string? NotasResolucion { get; set; }
        public string ClienteNombre { get; set; } = string.Empty;
        public string ClienteTelefono { get; set; } = string.Empty;
    }
}