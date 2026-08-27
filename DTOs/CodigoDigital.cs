using System.ComponentModel.DataAnnotations;

namespace NicaplusApi.DTOs
{
    public class RegistrarCodigosDto
    {
        [Required]
        public int IdProducto { get; set; }
        public int? IdVariacion { get; set; }

        [Required(ErrorMessage = "Debe enviar al menos un código.")]
        [MinLength(1)]
        public List<string> Codigos { get; set; } = new();
    }

    public class CodigoDigitalResponseDto
    {
        public int Id { get; set; }
        public int IdProducto { get; set; }
        public int? IdVariacion { get; set; }
        public string Clave { get; set; } = string.Empty;
        public bool Vendido { get; set; }
        public string Estado { get; set; } = string.Empty;
        public DateTime? FechaVenta { get; set; }
        public int? IdVenta { get; set; }
    }
}