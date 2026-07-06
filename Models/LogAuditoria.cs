using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NicaplusApi.Models
{
    public class LogAuditoria
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int IdUsuario { get; set; } // ¿Quién lo hizo?

        [Required]
        [StringLength(50)]
        public string Accion { get; set; } = string.Empty; // "CREAR", "EDITAR", "BORRAR", "LOGIN"

        [Required]
        [StringLength(100)]
        public string TablaAfectada { get; set; } = string.Empty; // "CLIENTE", "VENTA", "PRODUCTO"

        [Required]
        public string Detalles { get; set; } = string.Empty; // "Julian creó cliente X"

        [Required]
        public DateTime FechaRegistro { get; set; } = DateTime.UtcNow; // Ajustado a UTC para consistencia
    }
}