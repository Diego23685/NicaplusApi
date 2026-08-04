using System.ComponentModel.DataAnnotations;

namespace NicaplusApi.DTOs
{
    public class CrearCuentaPorPagarDto
    {
        [Required(ErrorMessage = "El proveedor es obligatorio.")]
        public int IdProveedor { get; set; }

        [Required(ErrorMessage = "El número de factura es obligatorio.")]
        [StringLength(100)]
        public string NumeroFactura { get; set; } = string.Empty;

        [Range(0.01, double.MaxValue, ErrorMessage = "El monto total debe ser mayor a 0.")]
        public decimal MontoTotal { get; set; }

        public DateTime? FechaRegistro { get; set; }

        [Required(ErrorMessage = "La fecha de vencimiento es obligatoria.")]
        public DateTime FechaVencimiento { get; set; }
    }

    public class RegistrarAbonoProveedorDto
    {
        [Range(0.01, double.MaxValue, ErrorMessage = "El abono debe ser mayor a 0.")]
        public decimal MontoAbono { get; set; }

        public string MetodoPago { get; set; } = "Efectivo";
    }

    public class CuentaPorPagarResponseDto
    {
        public int Id { get; set; }
        public int IdProveedor { get; set; }
        public string RazonSocialProveedor { get; set; } = null!;
        public string RucProveedor { get; set; } = string.Empty;
        public string NumeroFactura { get; set; } = null!;
        public decimal MontoTotal { get; set; }
        public decimal SaldoPendiente { get; set; }
        public DateTime FechaRegistro { get; set; }
        public DateTime FechaVencimiento { get; set; }
        public string Estado { get; set; } = null!;
        public bool EsVencida { get; set; }
    }
}