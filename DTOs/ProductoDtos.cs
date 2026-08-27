using System.ComponentModel.DataAnnotations;

namespace NicaplusApi.DTOs
{
    public class CrearProductoDto
    {
        [Required(ErrorMessage = "El nombre del producto es obligatorio.")]
        [StringLength(150, ErrorMessage = "El nombre no puede exceder los 150 caracteres.")]
        public string Nombre { get; set; } = string.Empty;

        public string Descripcion { get; set; } = string.Empty;

        [Range(0.01, double.MaxValue, ErrorMessage = "El precio de venta debe ser mayor a cero.")]
        public decimal PrecioVenta { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "El precio de costo no puede ser negativo.")]
        public decimal PrecioCosto { get; set; }

        public int StockActual { get; set; } = 0;
        public int StockMinimo { get; set; } = 0;
        public string ImagenUrl { get; set; } = string.Empty;

        public bool EsDigital { get; set; }
        public bool EsCodigoDigital { get; set; } = false;
        public bool ControlaStock { get; set; } = true;
        public bool RequiereServicio { get; set; }
        public bool VisibleEnCatalogo { get; set; } = true;
        public bool EsSuscripcion { get; set; }

        [Range(1, 365, ErrorMessage = "Los días de duración deben ser al menos 1.")]
        public int DiasDuracion { get; set; } = 30;

        public int GarantiaDias { get; set; } = 0;
        public string Proveedor { get; set; } = string.Empty;
        public string Estado { get; set; } = "Activo";

        public int? CategoriaId { get; set; }
        public int? JuegoId { get; set; }
        public bool TieneVariaciones { get; set; } = false;
        public List<VariacionProductoDto> Variaciones { get; set; } = new List<VariacionProductoDto>();
    }

    public class ActualizarProductoDto : CrearProductoDto
    {
        [Required(ErrorMessage = "El ID del producto es obligatorio.")]
        public int Id { get; set; }
    }

    public class ProductoCatalogoResponseDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = null!;
        public string Descripcion { get; set; } = string.Empty;
        public decimal PrecioVenta { get; set; }
        public string ImagenUrl { get; set; } = string.Empty;
        public bool EsDigital { get; set; }
        public bool EsCodigoDigital { get; set; }
        public string? MetadataDigital { get; set; }
        public bool EsSuscripcion { get; set; }
        public int DiasDuracion { get; set; }
        public int StockActual { get; set; }
        public bool VisibleEnCatalogo { get; set; }
        public string? CategoriaNombre { get; set; }
        public string? JuegoNombre { get; set; }
        public bool TieneVariaciones { get; set; } = false;
        public List<VariacionProductoDto> Variaciones { get; set; } = new List<VariacionProductoDto>();
    }

    public class ProductoAdminResponseDto : ProductoCatalogoResponseDto
    {
        public decimal PrecioCosto { get; set; }
        public int StockMinimo { get; set; }
        public bool ControlaStock { get; set; }
        public bool RequiereServicio { get; set; }
        public int GarantiaDias { get; set; }
        public string Proveedor { get; set; } = string.Empty;
        public string Estado { get; set; } = null!;
        public int? CategoriaId { get; set; }
        public int? JuegoId { get; set; }
        public int? PrimerPerfilId { get; set; }
    }

    public class VariacionProductoDto
    {
        public int Id { get; set; }
        public int ProductoPadreId { get; set; }
        public string SKU { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public string Almacenamiento { get; set; } = string.Empty;
        public string RAM { get; set; } = string.Empty;
        public string Talla { get; set; } = string.Empty;
        public string NombreVariacion { get; set; } = string.Empty;
        public decimal PrecioVenta { get; set; }
        public decimal PrecioCosto { get; set; }
        public int StockActual { get; set; }
        public int StockMinimo { get; set; }
        public string ImagenUrl { get; set; } = string.Empty;
        public string Estado { get; set; } = "Activo";
    }
}