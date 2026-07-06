namespace NicaplusApi.Dtos
{
    public class DetalleVentaDto
    {
        public int IdProducto { get; set; }
        public int Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }
        public string? MetadataDigital { get; set; } // Para correos, IDs, etc.
        
        // ◄ NUEVA PROPIEDAD: Días personalizados desde la caja
        public int? DiasDuracionPersonalizados { get; set; } 
    }
}