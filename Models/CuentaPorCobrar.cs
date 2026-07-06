using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NicaplusApi.Models
{
    public class CuentaPorCobrar
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int IdCliente { get; set; }
        [ForeignKey("IdCliente")]
        public Cliente? Cliente { get; set; }

        [Required]
        public int IdVenta { get; set; } // Venta original que generó la deuda

        [Required]
        public decimal MontoTotal { get; set; }

        [Required]
        public decimal SaldoPendiente { get; set; } // Lo que resta por pagar

        [Required]
        public DateTime FechaEmision { get; set; }

        [Required]
        public DateTime FechaVencimiento { get; set; }

        [Required]
        [StringLength(20)]
        public string Estado { get; set; } = "Pendiente"; // Pendiente, Pagado, Vencido
    }
}