namespace NicaplusApi.Models.Requests
{
    public class CancelarSuscripcionRequest
    {
        public int IdSuscripcion { get; set; }

        public string Motivo { get; set; } = "";
    }
}