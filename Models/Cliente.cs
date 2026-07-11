using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NicaplusApi.Models
{
    public class Cliente
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(150)]
        public string Nombre { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string Telefono { get; set; } = string.Empty; // Destinado a WhatsApp

        [StringLength(150)]
        public string Email { get; set; } = string.Empty;

        [Required]
        public DateTime FechaRegistro { get; set; } = DateTime.Now;

        public string Observaciones { get; set; } = string.Empty;

        [StringLength(250)]
        public string Etiquetas { get; set; } = string.Empty; // Almacenado como CSV: "VIP, Moroso, Frecuente"

        public int PuntosAcumulados { get; set; } = 0;

        public string PasswordHash { get; set; } = string.Empty; // Almacena el hash de la contraseña
        public bool EmailConfirmado { get; set; } // Indica si el email ha sido confirmado
        public string? TokenConfirmacion { get; set; }

        public string? TokenRecuperacion { get; set; }

        public DateTime? UltimoAcceso { get; set; }

        public bool Activo { get; set; } = true;

        public DateTime? ExpiracionTokenConfirmacion { get; set; }

        public DateTime? ExpiracionTokenRecuperacion { get; set; }
        

    }
}