using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NicaplusApi.Models
{
    public class CuentaPorPagar
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int IdProveedor { get; set; }
        [ForeignKey("IdProveedor")]
        public Proveedor? Proveedor { get; set; }

        [Required]
        [StringLength(100)]
        public string NumeroFactura { get; set; } = string.Empty;

        [Required]
        public decimal MontoTotal { get; set; }

        [Required]
        public decimal SaldoPendiente { get; set; }

        [Required]
        public DateTime FechaRegistro { get; set; }

        [Required]
        public DateTime FechaVencimiento { get; set; }

        [Required]
        [StringLength(20)]
        public string Estado { get; set; } = "Pendiente";
    }
}