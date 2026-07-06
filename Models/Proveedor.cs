using System.ComponentModel.DataAnnotations;

namespace NicaplusApi.Models
{
    public class Proveedor
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(150)]
        public string RazonSocial { get; set; } = string.Empty;

        [StringLength(100)]
        public string Ruc { get; set; } = string.Empty; // Cédula o RUC comercial

        [StringLength(20)]
        public string Telefono { get; set; } = string.Empty;

        [StringLength(150)]
        public string Email { get; set; } = string.Empty;
    }
}