using System.ComponentModel.DataAnnotations;

namespace NicaplusApi.Models
{
    public class ConfiguracionMensaje
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string TipoDisparador { get; set; } = string.Empty; // "RecordatorioRenovacion", "TallerListo", "EntregaAccesos", "EnvioComprobante"

        [Required]
        public int DiasAnticipacion { get; set; } = 3; // Aquí el usuario configura si quiere 3, 5, etc. (Solo aplica para recordatorios)

        [Required]
        public string PlantillaTexto { get; set; } = string.Empty; // El texto con las variables {cliente}, {servicio}, etc.

        public bool Activo { get; set; } = true;
    }
}