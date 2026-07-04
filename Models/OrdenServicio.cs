using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NicaplusApi.Models
{
    public class OrdenServicio
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int IdCliente { get; set; }

        public int? IdUsuario { get; set; } // Técnico asignado (puede iniciar sin técnico)

        [Required]
        [StringLength(100)]
        public string Dispositivo { get; set; } = string.Empty; // Ej: Laptop Dell, PS4 Pro

        [Required]
        public string Diagnostico { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string Estado { get; set; } = "Recibido"; // Recibido, En Revisión, Listo, Entregado

        public DateTime FechaIngreso { get; set; } = DateTime.UtcNow;
        
        public DateTime? FechaEntrega { get; set; }

        public string Notas { get; set; } = string.Empty;

        [ForeignKey("IdCliente")]
        public Cliente? Cliente { get; set; }

        [ForeignKey("IdUsuario")]
        public Usuario? Tecnico { get; set; }
    }
}