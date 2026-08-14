using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NicaplusApi.Models
{
    public class OrdenServicio
    {
        [Key]
        public int Id { get; set; }

        public int? IdCliente { get; set; }

        public int? IdUsuario { get; set; }

        [Required]
        [StringLength(100)]
        public string Dispositivo { get; set; } = string.Empty;

        [Required]
        public string Diagnostico { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string Estado { get; set; } = "Recibido";

        public DateTime FechaIngreso { get; set; } = DateTime.UtcNow;

        public DateTime? FechaEntrega { get; set; }

        public string Notas { get; set; } = string.Empty;

        [ForeignKey("IdCliente")]
        public Cliente? Cliente { get; set; }

        [ForeignKey("IdUsuario")]
        public Usuario? Tecnico { get; set; }
    }
}