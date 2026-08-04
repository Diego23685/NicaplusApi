using System.ComponentModel.DataAnnotations;

namespace NicaplusApi.DTOs
{
    public class ActualizarPlantillaDto
    {
        [Required(ErrorMessage = "El texto de la plantilla es obligatorio.")]
        public string PlantillaTexto { get; set; } = null!;

        [Range(0, 365, ErrorMessage = "Los días de anticipación deben estar entre 0 y 365.")]
        public int DiasAnticipacion { get; set; }

        public bool Activo { get; set; }
    }

    public class ConfiguracionMensajeResponseDto
    {
        public int Id { get; set; }
        public string TipoMensaje { get; set; } = null!;
        public string PlantillaTexto { get; set; } = null!;
        public int DiasAnticipacion { get; set; }
        public bool Activo { get; set; }
    }
}