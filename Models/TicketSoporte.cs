using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NicaplusApi.Models
{
    public class TicketSoporte
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int IdCliente { get; set; }

        [Required]
        [StringLength(100)]
        public string TipoTicket { get; set; } = string.Empty; // Garantía, Cambio de perfil, Cambio de contraseña, Cliente no puede ingresar, Reposición, Reembolso

        [Required]
        public string DescripcionFalla { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string Estado { get; set; } = "Pendiente"; // Pendiente, En proceso, Esperando proveedor, Resuelto

        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
        public DateTime? FechaResolucion { get; set; }

        public string NotasResolucion { get; set; } = string.Empty;

        [ForeignKey("IdCliente")]
        public Cliente? Cliente { get; set; }
    }
}