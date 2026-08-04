using System.ComponentModel.DataAnnotations;

namespace NicaplusApi.DTOs
{
    public class CrearCategoriaDto
    {
        [Required(ErrorMessage = "El nombre de la categoría es requerido.")]
        [StringLength(100, ErrorMessage = "El nombre no puede exceder los 100 caracteres.")]
        public string Nombre { get; set; } = null!;
    }

    public class CategoriaResponseDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = null!;
    }
}