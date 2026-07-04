using System.ComponentModel.DataAnnotations;

namespace NicaplusApi.Models
{
    public class Rol
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string NombreRol { get; set; } = string.Empty; // Admin, Técnico, Cajero
    }
}