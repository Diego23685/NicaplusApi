using System.ComponentModel.DataAnnotations;

namespace NicaplusApi.DTOs
{
    public class RequestCuentaCompletaDto
    {
        [Required(ErrorMessage = "El ID del producto base es obligatorio.")]
        public int IdProducto { get; set; }

        [Required(ErrorMessage = "El correo de la cuenta es obligatorio.")]
        [EmailAddress(ErrorMessage = "Formato de correo inválido.")]
        public string CorreoCuenta { get; set; } = string.Empty;

        [Required(ErrorMessage = "La contraseña de la cuenta es obligatoria.")]
        public string PasswordCuenta { get; set; } = string.Empty;

        [Range(1, 20, ErrorMessage = "La cantidad de perfiles debe ser entre 1 y 20.")]
        public int CantidadPerfiles { get; set; } = 5;
    }

    public class CrearPerfilCuentaDto
    {
        [Required(ErrorMessage = "El ID del producto es obligatorio.")]
        public int IdProducto { get; set; }

        [Required(ErrorMessage = "El nombre del perfil es obligatorio.")]
        public string NombrePerfil { get; set; } = string.Empty;

        public string PIN { get; set; } = string.Empty;

        [Required(ErrorMessage = "El correo de la cuenta es obligatorio.")]
        public string CorreoCuenta { get; set; } = string.Empty;

        [Required(ErrorMessage = "La contraseña es obligatoria.")]
        public string PasswordCuenta { get; set; } = string.Empty;

        public string? AccountGroupKey { get; set; }
    }

    public class ActualizarPerfilCuentaDto
    {
        [Required(ErrorMessage = "El nombre del perfil es obligatorio.")]
        public string NombrePerfil { get; set; } = string.Empty;

        public string PIN { get; set; } = string.Empty;

        [Required(ErrorMessage = "El correo de la cuenta es obligatorio.")]
        public string CorreoCuenta { get; set; } = string.Empty;

        [Required(ErrorMessage = "La contraseña es obligatoria.")]
        public string PasswordCuenta { get; set; } = string.Empty;
    }

    public class PerfilCuentaResponseDto
    {
        public int Id { get; set; }
        public int IdProducto { get; set; }
        public string NombrePerfil { get; set; } = null!;
        public string PIN { get; set; } = string.Empty;
        public string CorreoCuenta { get; set; } = null!;
        public string PasswordCuenta { get; set; } = null!;
        public bool Ocupado { get; set; }
        public int? IdClienteAsignado { get; set; }
        public string NombreCliente { get; set; } = "Disponible";
        public string EstadoPerfil { get; set; } = "Disponible";
        public string AccountGroupKey { get; set; } = string.Empty;
        public DateTime? FechaAsignacion { get; set; }
    }
}