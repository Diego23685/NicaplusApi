using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NicaplusApi.Models
{
    public class Venta
    {
        [Key]
        public int Id { get; set; }

        public DateTime FechaVenta { get; set; } = DateTime.UtcNow;

        [Required]
        public int IdUsuario { get; set; } // Quién vendió

        public int? IdCliente { get; set; } // A quién (puede ser cliente nulo/mostrador)

        [Column(TypeName = "decimal(18,2)")]
        public decimal Total { get; set; }

        [Required]
        [StringLength(50)]
        public string MetodoPago { get; set; } = "Efectivo"; // Efectivo, Transferencia

        [ForeignKey("IdUsuario")]
        public Usuario? Usuario { get; set; }

        [ForeignKey("IdCliente")]
        public Cliente? Cliente { get; set; }
        
        public ICollection<DetalleVenta> Detalles { get; set; } = new List<DetalleVenta>();
    }

    public class DetalleVenta
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int IdVenta { get; set; }

        [Required]
        public int IdProducto { get; set; }

        public int Cantidad { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal PrecioUnitario { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal SubTotal { get; set; }

        // Campo JSON para meter IDs de jugadores de Free Fire o números de recarga
        public string MetadataDigital { get; set; } = string.Empty; 

        [ForeignKey("IdVenta")]
        public Venta? Venta { get; set; }

        [ForeignKey("IdProducto")]
        public Producto? Producto { get; set; }
    }
}