using System.ComponentModel.DataAnnotations;

namespace NicaplusApi.DTOs
{
    public class CrearJuegoDto
    {
        [Required(ErrorMessage = "El nombre del juego o categoría es obligatorio.")]
        [StringLength(100, ErrorMessage = "El nombre no puede exceder los 100 caracteres.")]
        public string Nombre { get; set; } = string.Empty;

        public string ImagenUrl { get; set; } = string.Empty;
    }

    public class ActualizarJuegoDto
    {
        [Required(ErrorMessage = "El nombre del juego o categoría es obligatorio.")]
        [StringLength(100, ErrorMessage = "El nombre no puede exceder los 100 caracteres.")]
        public string Nombre { get; set; } = string.Empty;

        public string ImagenUrl { get; set; } = string.Empty;
    }

    public class JuegoResponseDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = null!;
        public string ImagenUrl { get; set; } = string.Empty;
        public int CantidadProductosAsociados { get; set; }
    }
}