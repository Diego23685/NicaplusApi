// Models/Suscripcion.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NicaplusApi.Models
{
    public class Suscripcion
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int IdCliente { get; set; }

        [Required]
        [StringLength(100)]
        public string NombreServicio { get; set; } = string.Empty; // Ej: Netflix 1 Perfil, Pase de Juego 30D, Licencia Antivirus, Suscripción VIP

        [Required]
        [StringLength(50)]
        public string TipoSuscripcion { get; set; } = "Digital"; // Digital, Mantenimiento, Licencia, VIP

        // Opcional: Vincularlo a un producto existente de tu catálogo
        public int? IdProducto { get; set; }

        // Opcional: Si viene de un mantenimiento de taller
        public int? IdOrdenServicio { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal CostoRenovacion { get; set; }

        [Required]
        public DateTime FechaInicio { get; set; } = DateTime.UtcNow;

        [Required]
        public DateTime FechaVencimiento { get; set; }

        [Required]
        [StringLength(30)]
        public string Estado { get; set; } = "Activa"; // Activa, Vencida, Cancelada

        // Campo para credenciales o notas específicas (Ej: "Correo: user@mail.com | Pin: 1234")
        public string DetallesCredenciales { get; set; } = string.Empty;

        [ForeignKey("IdCliente")]
        public Cliente? Cliente { get; set; }

        [ForeignKey("IdProducto")]
        public Producto? Producto { get; set; }

        [ForeignKey("IdOrdenServicio")]
        public OrdenServicio? OrdenServicio { get; set; }

        public int? IdPerfilCuenta { get; set; }

        public PerfilCuenta? PerfilCuenta { get; set; }
        public ICollection<Venta> Ventas { get; set; } = new List<Venta>();
        
    }
}