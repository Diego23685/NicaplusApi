using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NicaplusApi.Models
{
    public class MovimientoCaja
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public DateTime Fecha { get; set; } = DateTime.UtcNow;

        [Required]
        [StringLength(50)]
        public string Tipo { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string Concepto { get; set; } = string.Empty;

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Monto { get; set; }

        [StringLength(255)]
        public string Detalle { get; set; } = string.Empty;

        public int? IdVenta { get; set; }
        public int? IdCompraProveedor { get; set; }
        public int? IdRenovacion { get; set; }

        [ForeignKey("IdVenta")]
        public Venta? Venta { get; set; }

        [ForeignKey("IdCompraProveedor")]
        public CompraProveedor? CompraProveedor { get; set; }

        [ForeignKey("IdRenovacion")]
        public Renovacion? Renovacion { get; set; } // ◄ AGREGADO: Propiedad de navegación faltante
    }
}