namespace NicaplusApi.DTOs
{
    public class ReportePersonalizadoDto
    {
        public string Rango { get; set; } = string.Empty;
        public int VentasTotales { get; set; }
        public FinanzasResumenDto Finanzas { get; set; } = new();
        public List<TopProductoDto> TopProductos { get; set; } = new();
        public List<TransaccionResumenDto> Transacciones { get; set; } = new();
    }

    public class FinanzasResumenDto
    {
        public decimal Efectivo { get; set; }
        public decimal Transferencia { get; set; }
        public decimal Tarjeta { get; set; }
        public decimal TotalFacturado { get; set; }
        public decimal GastosOperativos { get; set; }
        public decimal InversionCompras { get; set; }
        public decimal BalanceCajaReal { get; set; }
    }

    public class TopProductoDto
    {
        public string Producto { get; set; } = string.Empty;
        public int Cantidad { get; set; }
        public decimal Subtotal { get; set; }
    }

    public class TransaccionResumenDto
{
    public int Id { get; set; }
    public int? IdCliente { get; set; }          
    public string Cliente { get; set; } = string.Empty; 
    public string Fecha { get; set; } = string.Empty;
    public string Operador { get; set; } = string.Empty;
    public string MetodoPago { get; set; } = string.Empty;
    public decimal Total { get; set; }
}

    public class DeudorDto
    {
        public string Nombre { get; set; } = string.Empty;
        public string Telefono { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public decimal Saldo { get; set; }
        public DateTime FechaVencimiento { get; set; }
    }
}