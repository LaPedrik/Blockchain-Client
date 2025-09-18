using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Blockchain.Models
{
    public class Transaction
    {
        public string TransactionId { get; set; }
        public string Sender { get; set; }
        public string Recipient { get; set; }
        public decimal Amount { get; set; }
        public DateTime Timestamp { get; set; }
        public string Signature { get; set; }
        public string PublicKey { get; set; }

        public Transaction()
        {
            TransactionId = Guid.NewGuid().ToString();
            Timestamp = DateTime.UtcNow;
        }

        public string GetDataToSign()
        {
            return $"{Sender}{Recipient}{Amount.ToString(CultureInfo.InvariantCulture)}{Timestamp:O}";
        }

        public bool VerifySignature()
        {
            if (string.IsNullOrEmpty(Signature) || string.IsNullOrEmpty(PublicKey))
                return false;

            try
            {
                using (var rsa = RSA.Create())
                {
                    rsa.ImportRSAPublicKey(Convert.FromBase64String(PublicKey), out _);
                    byte[] dataBytes = Encoding.UTF8.GetBytes(GetDataToSign());
                    byte[] signatureBytes = Convert.FromBase64String(Signature);

                    return rsa.VerifyData(dataBytes, signatureBytes,
                        HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
