namespace Blockchain.Models
{
    public class Block
    {
        public int Index { get; set; }
        public DateTime Timestamp { get; set; }
        public List<Transaction> Transactions { get; set; } = new();
        public string PreviousHash { get; set; }
        public int Proof { get; set; }
        public int Nonce { get; set; }
        public string Hash { get; set; }
    }
}
