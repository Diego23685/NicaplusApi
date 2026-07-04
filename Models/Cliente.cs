using System.ComponentModel.DataAnnotations;

namespace NicaplusApi.Models
{
    public class Cliente
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(150)]
        public string Nombre { get; set; } = string.Empty;

        [StringLength(20)]
        public string Telefono { get; set; } = string.Empty; // Vital para redirigir a WhatsApp

        [StringLength(150)]
        public string Email { get; set; } = string.Empty;

        public int PuntosAcumulados { get; set; } = 0; // Para reportar el "mejor cliente"
    }
}