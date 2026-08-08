using System.ComponentModel.DataAnnotations;

namespace NicaplusApi.DTOs
{
    public class CrearVentaDto
    {
        public int? IdCliente { get; set; }

        public DateTime? FechaVenta { get; set; }

        [Required(ErrorMessage = "El método de pago es obligatorio.")]
        [StringLength(30)]
        public string MetodoPago { get; set; } = "Efectivo";

        public DateTime? FechaVencimientoCreditoManual { get; set; }

        [Required(ErrorMessage = "La venta debe incluir al menos un detalle.")]
        [MinLength(1, ErrorMessage = "Debe agregar al menos un producto a la venta.")]
        public List<CrearDetalleVentaDto> Detalles { get; set; } = new();
    }

    public class CrearDetalleVentaDto
    {
        [Required]
        public int IdProducto { get; set; }
        public int? IdVariacion { get; set; }

        [Range(1, 1000, ErrorMessage = "La cantidad debe ser mayor a 0.")]
        public int Cantidad { get; set; }

        [Range(0, 999999.99, ErrorMessage = "El precio unitario no es válido.")]
        public decimal PrecioUnitario { get; set; }

        [Range(0, 999999.99, ErrorMessage = "El descuento no es válido.")]
        public decimal Descuento { get; set; } = 0m;

        public string MetadataDigital { get; set; } = string.Empty;
    }

    public class ActualizarVentaDto
    {
        [Required]
        public int Id { get; set; }

        public int? IdCliente { get; set; }

        [Required]
        public string MetodoPago { get; set; } = "Efectivo";

        public DateTime? FechaVencimientoCreditoManual { get; set; }

        [Required]
        [MinLength(1)]
        public List<CrearDetalleVentaDto> Detalles { get; set; } = new();
    }

    public class VentaResumenDto
    {
        public int Id { get; set; }
        public string FechaVenta { get; set; } = string.Empty;
        public int? IdCliente { get; set; }
        public string ClienteNombre { get; set; } = string.Empty;
        public string Operador { get; set; } = string.Empty;
        public string MetodoPago { get; set; } = string.Empty;
        public decimal Total { get; set; }
        public List<DetalleVentaResumenDto> Detalles { get; set; } = new();
    }

    public class DetalleVentaResumenDto
    {
        public int Id { get; set; }
        public int IdProducto { get; set; }
        public string ProductoNombre { get; set; } = string.Empty;
        public int Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }
        public decimal Descuento { get; set; }
        public decimal SubTotal { get; set; }
        public string MetadataDigital { get; set; } = string.Empty;
    }
}