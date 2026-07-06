// Models/Producto.cs
using System.ComponentModel.DataAnnotations;

namespace NicaplusApi.Models
{
    public class Producto
    {
        public int Id { get; set; }
        
        [Required]
        public string Nombre { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public decimal PrecioVenta { get; set; }
        public decimal PrecioCosto { get; set; } // Representa el "Precio compra"
        public int StockActual { get; set; }
        public int StockMinimo { get; set; }
        public string ImagenUrl { get; set; } = string.Empty;
        public bool EsDigital { get; set; }
        public bool RequiereServicio { get; set; }
        public bool VisibleEnCatalogo { get; set; }
        public bool EsSuscripcion { get; set; } 
        public int DiasDuracion { get; set; } = 30; // "Duración"

        // --- NUEVOS CAMPOS REQUERIDOS ---
        public int GarantiaDias { get; set; } = 0; // "Garantía" en días (Ej: 30 días)
        public string Proveedor { get; set; } = string.Empty; // "Proveedor" (Ej: "Proveedor VIP Latino")
        public string Estado { get; set; } = "Activo"; // "Estado" (Activo, Pausado, Agotado)

        public int? CategoriaId { get; set; }
        public Categoria? Categoria { get; set; }
        public int? JuegoId { get; set; }
        public Juego? Juego { get; set; }
    }
}