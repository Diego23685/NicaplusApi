using System.ComponentModel.DataAnnotations;

namespace NicaplusApi.DTOs
{
    public class CrearProveedorDto
    {
        [Required(ErrorMessage = "La razón social del proveedor es obligatoria.")]
        [StringLength(150)]
        public string RazonSocial { get; set; } = string.Empty;

        [StringLength(100)]
        public string? Ruc { get; set; }

        [StringLength(20)]
        public string? Telefono { get; set; }

        [RegularExpression(@"^$|^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$", ErrorMessage = "Formato de correo electrónico inválido.")]
        [StringLength(150)]
        public string? Email { get; set; }
    }

    public class ActualizarProveedorDto : CrearProveedorDto
    {
        [Required]
        public int Id { get; set; }
    }

    public class DetalleCompraInputDto
    {
        [Required]
        public int IdProducto { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "La cantidad debe ser de al menos 1 unidad.")]
        public int Cantidad { get; set; }

        [Range(0.01, double.MaxValue, ErrorMessage = "El costo unitario debe ser mayor a cero.")]
        public decimal CostoUnitario { get; set; }

        // NUEVO: Permite actualizar el precio de venta en el catálogo directamente
        [Range(0, double.MaxValue)]
        public decimal? NuevoPrecioVenta { get; set; }

        public int GarantiaDiasPactada { get; set; } = 0;
    }

    public class RegistrarCompraProveedorDto
    {
        [Required]
        public int IdProveedor { get; set; }

        [Required]
        [MinLength(1)]
        public List<DetalleCompraInputDto> Detalles { get; set; } = new();

        [Range(0.01, double.MaxValue)]
        public decimal TotalCompra { get; set; }
    }

    public class CompraResumenDto
    {
        public int Id { get; set; }
        public int IdProveedor { get; set; }
        public string ProveedorNombre { get; set; } = string.Empty;
        public string FechaCompra { get; set; } = string.Empty;
        public decimal TotalCompra { get; set; }
        public List<DetalleCompraResumenDto> Detalles { get; set; } = new();
    }

    public class DetalleCompraResumenDto
    {
        public int Id { get; set; }
        public int IdProducto { get; set; }
        public string ProductoNombre { get; set; } = string.Empty;
        public int Cantidad { get; set; }
        public decimal CostoUnitario { get; set; }
        public decimal SubTotal { get; set; }
        public int GarantiaDiasPactada { get; set; }
    }

    public class RendimientoProveedorResponseDto
    {
        public int Id { get; set; }
        public string RazonSocial { get; set; } = null!;
        public string Telefono { get; set; } = string.Empty;
        public int TotalOrdenes { get; set; }
        public decimal TotalInvertido { get; set; }
        public decimal MargenGananciaHistorico { get; set; }
        public double TiempoRespuestaPromedio { get; set; }
        public double ScoreConfiabilidad { get; set; }
    }
}