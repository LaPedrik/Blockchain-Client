namespace Blockchain.Models.Requests
{
    public class TransactionRequest
    {
        public string Sender { get; set; }
        public string Recipient { get; set; }
        public decimal Amount { get; set; }
        public string Signature { get; set; }
        public string PublicKey { get; set; } 
        public DateTime Timestamp { get; set; }
    }
}
