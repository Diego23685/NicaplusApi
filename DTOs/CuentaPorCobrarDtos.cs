using System.ComponentModel.DataAnnotations;

namespace NicaplusApi.DTOs
{
    public class CrearCuentaPorCobrarDto
    {
        [Required(ErrorMessage = "El cliente es requerido.")]
        public int IdCliente { get; set; }

        public int IdVenta { get; set; } // Puede ser 0 si es una deuda externa manual

        [Range(0.01, double.MaxValue, ErrorMessage = "El monto debe ser mayor a 0.")]
        public decimal MontoTotal { get; set; }

        public DateTime? FechaEmision { get; set; }

        [Required(ErrorMessage = "La fecha de vencimiento es requerida.")]
        public DateTime FechaVencimiento { get; set; }
    }

    public class RegistrarAbonoDto
    {
        [Range(0.01, double.MaxValue, ErrorMessage = "El abono debe ser mayor a 0.")]
        public decimal MontoAbono { get; set; }

        public string MetodoPago { get; set; } = "Efectivo";
    }

    public class CuentaPorCobrarResponseDto
    {
        public int Id { get; set; }
        public int IdCliente { get; set; }
        public string NombreCliente { get; set; } = null!;
        public string TelefonoCliente { get; set; } = null!;
        public int IdVenta { get; set; }
        public decimal MontoTotal { get; set; }
        public decimal SaldoPendiente { get; set; }
        public DateTime FechaEmision { get; set; }
        public DateTime FechaVencimiento { get; set; }
        public string Estado { get; set; } = null!;
        public bool EsVencida { get; set; }
    }
}