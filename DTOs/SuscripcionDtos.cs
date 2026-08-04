using System.ComponentModel.DataAnnotations;

namespace NicaplusApi.DTOs
{
    public class CrearSuscripcionDto
    {
        [Required(ErrorMessage = "El cliente es obligatorio.")]
        public int IdCliente { get; set; }

        [Required(ErrorMessage = "El nombre del servicio es obligatorio.")]
        [StringLength(100)]
        public string NombreServicio { get; set; } = string.Empty;

        [StringLength(50)]
        public string TipoSuscripcion { get; set; } = "Digital";

        public int? IdProducto { get; set; }
        public int? IdOrdenServicio { get; set; }
        public int? IdPerfilCuenta { get; set; }

        [Required]
        [Range(0.01, 999999.99, ErrorMessage = "El costo de renovación debe ser mayor a 0.")]
        public decimal CostoRenovacion { get; set; }

        public DateTime? FechaInicio { get; set; }
        public DateTime? FechaVencimiento { get; set; }

        public string DetallesCredenciales { get; set; } = string.Empty;
    }

    public class ActualizarSuscripcionDto
    {
        [Required]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string NombreServicio { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string TipoSuscripcion { get; set; } = "Digital";

        [Required]
        public decimal CostoRenovacion { get; set; }

        [Required]
        public DateTime FechaVencimiento { get; set; }

        [Required]
        [StringLength(30)]
        public string Estado { get; set; } = "Activa";

        public string DetallesCredenciales { get; set; } = string.Empty;
    }

    public class AlertaSuscripcionDto
    {
        public int Id { get; set; }
        public string NombreServicio { get; set; } = string.Empty;
        public DateTime FechaInicio { get; set; }
        public DateTime FechaVencimiento { get; set; }
        public decimal CostoRenovacion { get; set; }
        public string Estado { get; set; } = string.Empty;
        public string DetallesCredenciales { get; set; } = string.Empty;
        public int DiasRestantes { get; set; }
        public string AlertaFiltro { get; set; } = string.Empty;
        public ClienteAlertaDto? Cliente { get; set; }
    }

    public class ClienteAlertaDto
    {
        public string Nombre { get; set; } = string.Empty;
        public string Telefono { get; set; } = string.Empty;
    }
}