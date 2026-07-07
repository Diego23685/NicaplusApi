using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NicaplusApi.Models
{
    public class GarantiaTicket
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int IdCliente { get; set; }

        [Required]
        public int IdUsuarioResponsable { get; set; } // Técnico o administrador que ejecuta el cambio

        [Required]
        public string Motivo { get; set; } = string.Empty; // Ej: "Caída masiva de perfiles", "Cambio de contraseña sin avisar"

        [Required]
        public DateTime FechaRepo { get; set; } = DateTime.UtcNow;

        [Required]
        [StringLength(200)]
        public string CuentaAnterior { get; set; } = string.Empty; // Credenciales o perfil revocado

        [Required]
        [StringLength(200)]
        public string CuentaNueva { get; set; } = string.Empty; // Nuevos accesos asignados

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal CostoReposicion { get; set; } // Cuánto le costó al negocio reponer este perfil (Pérdida)

        [ForeignKey("IdCliente")]
        public Cliente? Cliente { get; set; }

        [ForeignKey("IdUsuarioResponsable")]
        public Usuario? Responsable { get; set; }

        public int? IdProducto { get; set; }

        [ForeignKey("IdProducto")]
        public Producto? Producto { get; set; }

        // En Models/GarantiaTicket.cs
        public string Estado { get; set; } = "Pendiente"; // Valor por defecto
    }
}