using System;
using System.ComponentModel.DataAnnotations;

namespace NicaplusApi.Models
{
    public class LogAuditoria
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int? IdUsuario { get; set; }

        public int? IdCliente { get; set; }

        [Required]
        public string TipoActor { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string Accion { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string TablaAfectada { get; set; } = string.Empty;

        [Required]
        public string Detalles { get; set; } = string.Empty;

        [Required]
        public DateTime FechaRegistro { get; set; }

        public LogAuditoria()
        {
            // CORREGIDO: Garantiza que cualquier instancia nueva tome la hora de Nicaragua por defecto
            var zonaNica = TimeZoneInfo.FindSystemTimeZoneById("Central America Standard Time");
            FechaRegistro = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zonaNica);
        }
    }
}