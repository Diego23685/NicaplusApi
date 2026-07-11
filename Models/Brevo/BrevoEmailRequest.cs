namespace NicaplusApi.Models.Brevo
{
    public class BrevoEmailRequest
    {
        public BrevoSender Sender { get; set; } = new();

        public List<BrevoRecipient> To { get; set; } = new();

        public string Subject { get; set; } = string.Empty;

        public string HtmlContent { get; set; } = string.Empty;
    }
}