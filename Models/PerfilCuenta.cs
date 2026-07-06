using System.ComponentModel.DataAnnotations.Schema;

namespace NicaplusApi.Models
{
    public class PerfilCuenta
    {
        public int Id { get; set; }
        public int IdProducto { get; set; } // Vinculado a la cuenta base (Netflix, HBO, etc.)
        public string NombrePerfil { get; set; } = string.Empty; // Ej: "Perfil 1", "Perfil 2"
        public string PIN { get; set; } = string.Empty; // Código de acceso de pantalla
        public string CorreoCuenta { get; set; } = string.Empty; // Correo base de la cuenta
        public string PasswordCuenta { get; set; } = string.Empty; // Contraseña base de la cuenta
        public bool Ocupado { get; set; } = false;
        public int? IdClienteAsignado { get; set; } // Nullable si está libre
        
        [ForeignKey("IdProducto")]
        public Producto? Producto { get; set; }
    }
}