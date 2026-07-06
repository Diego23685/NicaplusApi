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
        public string Tipo { get; set; } = string.Empty; // 🛠️ Solucionado: Inicializado con texto vacío

        [Required]
        [StringLength(50)]
        public string Concepto { get; set; } = string.Empty; // 🛠️ Solucionado: Inicializado con texto vacío

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Monto { get; set; }

        [StringLength(255)]
        public string Detalle { get; set; } = string.Empty; // Este ya estaba bien configurado

        public int? IdVenta { get; set; }
        public int? IdCompraProveedor { get; set; }

        [ForeignKey("IdVenta")]
        public Venta? Venta { get; set; } // Las propiedades con "?" sí permiten nulos por diseño, por eso no daban warning

        [ForeignKey("IdCompraProveedor")]
        public CompraProveedor? CompraProveedor { get; set; }
    }
}