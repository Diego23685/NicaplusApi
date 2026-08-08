using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace NicaplusApi.Models
{
    public class VariacionProducto
    {
        public int Id { get; set; }

        [Required]
        public int ProductoPadreId { get; set; }
        [JsonIgnore]
        public Producto? ProductoPadre { get; set; }

        public string SKU { get; set; } = string.Empty; // Ej: IP13-128-BLK
        
        // Atributos específicos
        public string Color { get; set; } = string.Empty; // Ej: "Azul Sierra"
        public string Almacenamiento { get; set; } = string.Empty; // Ej: "128GB", "256GB"
        public string RAM { get; set; } = string.Empty; // Ej: "6GB"
        public string Talla { get; set; } = string.Empty; // Ej: "M", "L"
        public string NombreVariacion { get; set; } = string.Empty; // Ej: "128GB / 6GB RAM - Negro"

        // Precios e Inventario específicos de la variación
        public decimal PrecioVenta { get; set; }
        public decimal PrecioCosto { get; set; }
        public int StockActual { get; set; }
        public int StockMinimo { get; set; } = 2;
        public string ImagenUrl { get; set; } = string.Empty;
        public string Estado { get; set; } = "Activo";
    }
}