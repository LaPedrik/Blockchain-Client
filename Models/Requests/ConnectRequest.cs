namespace Blockchain.Models.Requests
{
    public class ConnectRequest
    {
        public Guid WalletId { get; set; }
        public string WalletAddress { get; set; }
    }
}
