namespace NicaplusApi.DTOs
{
    public class NotificacionRenovacionDto
    {
        public int IdSuscripcion { get; set; }
        public string NombreServicio { get; set; } = null!;
        public string ClienteNombre { get; set; } = string.Empty;
        public DateTime FechaVencimiento { get; set; }
        public int DiasRestantes { get; set; }
        public string Tipo { get; set; } = "Renovación";
    }

    public class NotificacionTicketDto
    {
        public int IdTicket { get; set; }
        public string TipoTicket { get; set; } = null!;
        public string ClienteNombre { get; set; } = string.Empty;
        public DateTime FechaCreacion { get; set; }
        public string Tipo { get; set; } = "Ticket";
    }

    public class NotificacionStockBajoDto
    {
        public int IdProducto { get; set; }
        public string NombreProducto { get; set; } = null!;
        public int StockActual { get; set; }
        public int StockMinimo { get; set; }
        public string Tipo { get; set; } = "Inventario";
    }

    public class NotificacionGarantiaDto
    {
        public int IdGarantia { get; set; }
        public string ClienteNombre { get; set; } = string.Empty;
        public string Motivo { get; set; } = null!;
        public DateTime FechaRepo { get; set; }
        public string Tipo { get; set; } = "Garantía";
    }

    public class SummaryNotificacionesResponseDto
    {
        public int TotalAlertas { get; set; }
        public List<NotificacionRenovacionDto> Renovaciones { get; set; } = new();
        public List<NotificacionTicketDto> Tickets { get; set; } = new();
        public List<NotificacionStockBajoDto> StockBajo { get; set; } = new();
        public List<NotificacionGarantiaDto> Garantias { get; set; } = new();
    }
}