using System.ComponentModel.DataAnnotations;

namespace NicaplusApi.DTOs
{
    public class RegistrarRenovacionDto
    {
        [Required(ErrorMessage = "El ID de la suscripción es obligatorio.")]
        public int IdSuscripcion { get; set; }

        [Range(0.01, double.MaxValue, ErrorMessage = "El monto de la renovación debe ser mayor a cero.")]
        public decimal Monto { get; set; }

        public DateTime? FechaPago { get; set; }

        [Required(ErrorMessage = "El método de pago es obligatorio.")]
        [StringLength(50, ErrorMessage = "El método de pago no puede exceder 50 caracteres.")]
        public string MetodoPago { get; set; } = string.Empty;

        [StringLength(250, ErrorMessage = "La observación no puede exceder los 250 caracteres.")]
        public string Observacion { get; set; } = string.Empty;
    }

    public class CancelarSuscripcionDto
    {
        [Required(ErrorMessage = "El ID de la suscripción es obligatorio.")]
        public int IdSuscripcion { get; set; }

        [Required(ErrorMessage = "Debe indicar el motivo de la cancelación.")]
        [StringLength(200, ErrorMessage = "El motivo no puede exceder los 200 caracteres.")]
        public string Motivo { get; set; } = string.Empty;
    }

    public class RenovacionResponseDto
    {
        public int Id { get; set; }
        public int IdSuscripcion { get; set; }
        public int IdCliente { get; set; }
        public string Cliente { get; set; } = string.Empty;
        public string Servicio { get; set; } = string.Empty;
        public decimal Monto { get; set; }
        public DateTime FechaPago { get; set; }
        public DateTime FechaAnterior { get; set; }
        public DateTime NuevaFechaVencimiento { get; set; }
        public string MetodoPago { get; set; } = string.Empty;
        public string Observacion { get; set; } = string.Empty;
    }
}