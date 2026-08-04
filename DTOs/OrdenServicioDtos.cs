using System.ComponentModel.DataAnnotations;

namespace NicaplusApi.DTOs
{
    public class CrearOrdenServicioDto
    {
        [Required(ErrorMessage = "El cliente es obligatorio.")]
        public int IdCliente { get; set; }

        public int? IdUsuario { get; set; } // Técnico asignado opcional

        [Required(ErrorMessage = "El nombre del dispositivo es obligatorio.")]
        [StringLength(100)]
        public string Dispositivo { get; set; } = string.Empty;

        [Required(ErrorMessage = "El diagnóstico o falla inicial es obligatorio.")]
        public string Diagnostico { get; set; } = string.Empty;

        public string Notas { get; set; } = string.Empty;
    }

    public class ActualizarEstadoOrdenDto
    {
        [Required(ErrorMessage = "El nuevo estado es obligatorio.")]
        public string NuevoEstado { get; set; } = string.Empty;

        public string? Notas { get; set; }
    }

    public class EntregaOrdenDto
    {
        [Required(ErrorMessage = "El diagnóstico final es obligatorio.")]
        public string DiagnosticoFinal { get; set; } = string.Empty;

        [Required(ErrorMessage = "Las herramientas utilizadas son obligatorias.")]
        public string HerramientasUsed { get; set; } = string.Empty;

        [Range(0, double.MaxValue, ErrorMessage = "El costo de reparación no puede ser negativo.")]
        public decimal CostoReparacion { get; set; }

        public string MetodoPago { get; set; } = "Efectivo";

        public int IdProductoServicio { get; set; } = 3;
    }

    public class OrdenServicioResponseDto
    {
        public int Id { get; set; }
        public int IdCliente { get; set; }
        public string ClienteNombre { get; set; } = null!;
        public string ClienteTelefono { get; set; } = string.Empty;
        public int? IdUsuario { get; set; }
        public string TecnicoNombre { get; set; } = string.Empty;
        public string Dispositivo { get; set; } = null!;
        public string Diagnostico { get; set; } = null!;
        public string Estado { get; set; } = null!;
        public DateTime FechaIngreso { get; set; }
        public DateTime? FechaEntrega { get; set; }
        public string Notas { get; set; } = string.Empty;
    }
}