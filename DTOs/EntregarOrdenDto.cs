// Models/Dtos/EntregaOrdenDto.cs
namespace NicaplusApi.Models.Dtos
{
    public class EntregaOrdenDto
    {
        public string DiagnosticoFinal { get; set; } = string.Empty;
        public string HerramientasUsed { get; set; } = string.Empty;
        public decimal CostoReparacion { get; set; }
    }
}