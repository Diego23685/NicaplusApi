using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NicaplusApi.Models
{
    public class CompraProveedor
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int IdProveedor { get; set; }

        [Required]
        public DateTime FechaCompra { get; set; } = DateTime.UtcNow;

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalCompra { get; set; }

        public int TiempoEntregaRealDias { get; set; } // Días reales que tardó en entregar para calcular Confiabilidad

        [ForeignKey("IdProveedor")]
        public Proveedor? Proveedor { get; set; }

        public ICollection<DetalleCompraProveedor> Detalles { get; set; } = new List<DetalleCompraProveedor>();
    }

    public class DetalleCompraProveedor
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int IdCompraProveedor { get; set; }

        [Required]
        public int IdProducto { get; set; }

        [Required]
        public int Cantidad { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal CostoUnitario { get; set; }

        public int GarantiaDiasPactada { get; set; } // Garantía provista en este lote específico

        [ForeignKey("IdCompraProveedor")]
        public CompraProveedor? Compra { get; set; }

        [ForeignKey("IdProducto")]
        public Producto? Producto { get; set; }
    }
}