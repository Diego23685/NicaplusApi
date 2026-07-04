// Models/Juego.cs
namespace NicaplusApi.Models
{
    public class Juego
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string ImagenUrl { get; set; } = string.Empty; // Para el look del catálogo
    }
}