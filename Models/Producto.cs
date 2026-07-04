namespace NicaplusApi.Models
{
    public class Producto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public decimal PrecioVenta { get; set; }
        public decimal PrecioCosto { get; set; }
        public int StockActual { get; set; }
        public int StockMinimo { get; set; }
        public string ImagenUrl { get; set; } = string.Empty;
        public bool EsDigital { get; set; }
        public bool RequiereServicio { get; set; }
        public bool VisibleEnCatalogo { get; set; }

        // --- NUEVAS LLAVES FORÁNEAS ---
        public int? CategoriaId { get; set; }
        public Categoria? Categoria { get; set; }

        public int? JuegoId { get; set; }
        public Juego? Juego { get; set; }
    }
}