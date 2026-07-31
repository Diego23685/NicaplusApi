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
        public decimal PrecioCosto { get; set; } 
        public int StockActual { get; set; }
        public int StockMinimo { get; set; }
        public string ImagenUrl { get; set; } = string.Empty;
        public bool EsDigital { get; set; }
        
        // --- CONTROL DE INVENTARIO PARA INTANGIBLES ---
        public bool ControlaStock { get; set; } = true; // Por defecto es true

        public bool RequiereServicio { get; set; }
        public bool VisibleEnCatalogo { get; set; }
        public bool EsSuscripcion { get; set; } 
        public int DiasDuracion { get; set; } = 30; 

        public int GarantiaDias { get; set; } = 0; 
        public string Proveedor { get; set; } = string.Empty; 
        public string Estado { get; set; } = "Activo"; 

        public int? CategoriaId { get; set; }
        public Categoria? Categoria { get; set; }
        public int? JuegoId { get; set; }
        public Juego? Juego { get; set; }
    }
}