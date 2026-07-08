using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NicaplusApi.Models
{
    public class Renovacion
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int IdSuscripcion { get; set; }

        [Required]
        public int IdCliente { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Monto { get; set; }

        public DateTime FechaPago { get; set; } = DateTime.UtcNow;

        public DateTime FechaAnterior { get; set; }

        public DateTime NuevaFechaVencimiento { get; set; }

        public string MetodoPago { get; set; } = string.Empty;

        public string Observacion { get; set; } = string.Empty;


        [ForeignKey("IdSuscripcion")]
        public Suscripcion? Suscripcion { get; set; }


        [ForeignKey("IdCliente")]
        public Cliente? Cliente { get; set; }
    }
}