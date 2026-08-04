namespace NicaplusApi.DTOs
{
    public class PerfilClienteResponseDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = null!;
        public string Telefono { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public DateTime FechaRegistro { get; set; }
        public int PuntosAcumulados { get; set; }
        public string Etiquetas { get; set; } = string.Empty;
    }

    public class DetalleCompraDto
    {
        public int IdProducto { get; set; }
        public string NombreProducto { get; set; } = null!;
        public int Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }
        public decimal SubTotal { get; set; }
    }

    public class CompraClienteResponseDto
    {
        public int Id { get; set; }
        public DateTime FechaVenta { get; set; }
        public decimal Total { get; set; }
        public string MetodoPago { get; set; } = null!;
        public List<DetalleCompraDto> Productos { get; set; } = new();
    }

    public class PerfilCuentaAsignadaDto
    {
        public string NombrePerfil { get; set; } = string.Empty;
        public string PIN { get; set; } = string.Empty;
        public string CorreoCuenta { get; set; } = string.Empty;
        public string PasswordCuenta { get; set; } = string.Empty;
    }

    public class SuscripcionClienteResponseDto
    {
        public int Id { get; set; }
        public string NombreServicio { get; set; } = null!;
        public string TipoSuscripcion { get; set; } = null!;
        public DateTime FechaInicio { get; set; }
        public DateTime FechaVencimiento { get; set; }
        public string Estado { get; set; } = null!;
        public decimal CostoRenovacion { get; set; }
        public string? NombreProducto { get; set; }
        public PerfilCuentaAsignadaDto? Perfil { get; set; }
    }

    public class ProximaRenovacionDto
    {
        public int Id { get; set; }
        public string NombreServicio { get; set; } = null!;
        public DateTime FechaVencimiento { get; set; }
        public decimal CostoRenovacion { get; set; }
    }

    public class ResumenCompraDto
    {
        public int Id { get; set; }
        public DateTime FechaVenta { get; set; }
        public decimal Total { get; set; }
    }

    public class DashboardClienteResponseDto
    {
        public string NombreCliente { get; set; } = null!;
        public string EmailCliente { get; set; } = string.Empty;
        public int PuntosAcumulados { get; set; }
        public int TotalCompras { get; set; }
        public int SuscripcionesActivas { get; set; }
        public int TicketsAbiertos { get; set; }
        public int GarantiasActivas { get; set; }
        public ProximaRenovacionDto? ProximaRenovacion { get; set; }
        public List<ResumenCompraDto> UltimasCompras { get; set; } = new();
    }
}