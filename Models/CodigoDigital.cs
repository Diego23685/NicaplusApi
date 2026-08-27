using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace NicaplusApi.Models
{
    public class CodigoDigital
    {
        public int Id { get; set; }

        [Required]
        public int IdProducto { get; set; }
        [JsonIgnore]
        public Producto? Producto { get; set; }

        public int? IdVariacion { get; set; } // Por si manejas variantes (ej. 1 mes, 3 meses)
        [JsonIgnore]
        public VariacionProducto? Variacion { get; set; }

        [Required]
        [StringLength(255)]
        public string Clave { get; set; } = string.Empty; // Ej: "XXXX-YYYY-ZZZZ"

        public bool Vendido { get; set; } = false;
        public string Estado { get; set; } = "Disponible"; // "Disponible", "Vendido", "Reservado"

        public DateTime? FechaVenta { get; set; }
        public int? IdVenta { get; set; }
        public int? IdClienteAsignado { get; set; }
    }
}