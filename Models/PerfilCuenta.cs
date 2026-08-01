// Models/PerfilCuenta.cs
using System.ComponentModel.DataAnnotations.Schema;

namespace NicaplusApi.Models
{
    public class PerfilCuenta
    {
        public int Id { get; set; }
        
        // AHORA: Apunta al producto comercial general (ej: "Netflix Perfil 30 días")
        public int IdProducto { get; set; } 
        
        public string NombrePerfil { get; set; } = string.Empty; // Ej: "Perfil 1", "Perfil 2"
        public string PIN { get; set; } = string.Empty; 
        
        // Datos de la cuenta técnica contenedora
        public string CorreoCuenta { get; set; } = string.Empty; 
        public string PasswordCuenta { get; set; } = string.Empty; 
        
        public bool Ocupado { get; set; } = false;
        public int? IdClienteAsignado { get; set; } 
        public string EstadoPerfil { get; set; } = "Disponible"; // Disponible, Asignado, En Revisión
        
        public DateTime? FechaAsignacion { get; set; }
        public DateTime? FechaLiberacion { get; set; }
        
        // NUEVO: Identificador único de lote para saber qué perfiles pertenecen a la misma cuenta física
        public string AccountGroupKey { get; set; } = string.Empty; 

        [ForeignKey("IdProducto")]
        public Producto? Producto { get; set; }
        public ICollection<Suscripcion> Suscripciones { get; set; } = new List<Suscripcion>();
    }
}