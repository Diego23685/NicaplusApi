using System.ComponentModel.DataAnnotations;

namespace NicaplusApi.DTOs
{
    public class CrearProveedorDto
    {
        [Required(ErrorMessage = "La razón social del proveedor es obligatoria.")]
        [StringLength(150, ErrorMessage = "La razón social no puede exceder los 150 caracteres.")]
        public string RazonSocial { get; set; } = string.Empty;

        [StringLength(100, ErrorMessage = "El RUC/Identificación no puede exceder 100 caracteres.")]
        public string Ruc { get; set; } = string.Empty;

        [StringLength(20, ErrorMessage = "El teléfono no puede exceder 20 caracteres.")]
        public string Telefono { get; set; } = string.Empty;

        [EmailAddress(ErrorMessage = "Formato de correo electrónico inválido.")]
        [StringLength(150, ErrorMessage = "El correo no puede exceder 150 caracteres.")]
        public string Email { get; set; } = string.Empty;
    }

    public class ActualizarProveedorDto : CrearProveedorDto
    {
        [Required(ErrorMessage = "El ID del proveedor es obligatorio.")]
        public int Id { get; set; }
    }

    public class DetalleCompraInputDto
    {
        [Required(ErrorMessage = "El ID del producto es obligatorio.")]
        public int IdProducto { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "La cantidad debe ser de al menos 1 unidad.")]
        public int Cantidad { get; set; }

        [Range(0.01, double.MaxValue, ErrorMessage = "El costo unitario debe ser mayor a cero.")]
        public decimal CostoUnitario { get; set; }

        public int GarantiaDiasPactada { get; set; } = 0;
    }

    public class RegistrarCompraProveedorDto
    {
        [Required(ErrorMessage = "El ID del proveedor es obligatorio.")]
        public int IdProveedor { get; set; }

        [Required(ErrorMessage = "Debe incluir al menos un producto en la compra.")]
        public List<DetalleCompraInputDto> Detalles { get; set; } = new();

        [Range(0.01, double.MaxValue, ErrorMessage = "El total de la compra debe ser mayor a cero.")]
        public decimal TotalCompra { get; set; }
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