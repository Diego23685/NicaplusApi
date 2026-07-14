using System;
using System.ComponentModel.DataAnnotations;

namespace NicaplusApi.Models
{
    public class LogAuditoria
    {
        [Key]
        public int Id { get; set; }

        // Si la tabla permite nulos, quitamos [Required]. Si no los permite, cambia a: public int IdUsuario { get; set; }
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

        // Detalles suele ser el principal causante de nulos si una acción no registró parámetros. Lo hacemos anulable.
        public string? Detalles { get; set; }

        [Required]
        public DateTime FechaRegistro { get; set; }

        public LogAuditoria()
        {
            var zonaNica = TimeZoneInfo.FindSystemTimeZoneById("Central America Standard Time");
            FechaRegistro = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zonaNica);
        }
    }
}