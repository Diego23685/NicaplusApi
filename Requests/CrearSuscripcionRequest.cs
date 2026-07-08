public class CrearSuscripcionRequest
{
    public int IdCliente {get;set;}

    public int IdProducto {get;set;}

    public string NombreServicio {get;set;} = string.Empty;

    public decimal Precio {get;set;}

    public string MetodoPago {get;set;} = "Efectivo";

    public DateTime FechaVencimiento {get;set;}

    public string DetallesCredenciales {get;set;} = string.Empty;
}