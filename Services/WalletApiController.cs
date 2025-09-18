using Blockchain.Models;
using Blockchain.Models.Requests;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Blockchain.Services
{
    [ApiController]
    [Route("api")]
    public class WalletApiController : ControllerBase
    {
        private readonly Blockchain.Models.Blockchain _blockchain;
        private readonly ILogger<WalletApiController> _logger;

        public WalletApiController(Blockchain.Models.Blockchain blockchain, ILogger<WalletApiController> logger)
        {
            _logger = logger;
            _blockchain = blockchain;
        }

        [HttpGet("status")]
        public IActionResult GetStatus()
        {
            return Ok(new { Status = "Online", Version = "1.0" });
        }

        [HttpPost("wallet/connect")]
        public IActionResult ConnectWallet([FromBody] ConnectRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.WalletAddress))
                {
                    return BadRequest(new { error = "Wallet address is required" });
                }

                _logger.LogInformation($"Wallet connected: {request.WalletAddress} (ID: {request.WalletId})");

                return Ok(new
                {
                    message = "Wallet connected successfully",
                    walletId = request.WalletId,
                    walletAddress = request.WalletAddress,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error connecting wallet");
                return StatusCode(500, new { error = ex.Message });
            }
        }
        [HttpGet("wallet/balance")]
        public IActionResult GetBalance([FromQuery] string? walletAddress)
        {
            try
            {
                if (string.IsNullOrEmpty(walletAddress))
                {
                    return BadRequest(new { error = "Wallet address is required" });
                }
                decimal balance = _blockchain.GetBalance(walletAddress);
                return Ok(new
                {
                    walletAddress = walletAddress,
                    balance = balance,
                    status = "success"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
        [HttpPost("wallet/mine")]
        public IActionResult MineBlock([FromBody] MiningRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.MinerAddress))
                {
                    return BadRequest(new { error = "Miner address is required" });
                }

                var lastBlock = _blockchain.GetLastBlock();

                int proof = _blockchain.ProofOfWork(lastBlock.Proof);

                var rewardTransaction = new Transaction
                {
                    Sender = "0",
                    Recipient = request.MinerAddress,
                    Amount = 1.0m,
                    Timestamp = DateTime.UtcNow
                };

                _blockchain.CreateTransaction(rewardTransaction);

                var newBlock = _blockchain.CreateBlock(proof);

                _logger.LogInformation($"New block mined: {newBlock.Index}");

                return Ok(new
                {
                    message = "New block mined successfully",
                    block = new
                    {
                        index = newBlock.Index,
                        timestamp = newBlock.Timestamp,
                        proof = newBlock.Proof,
                        previousHash = newBlock.PreviousHash,
                        hash = newBlock.Hash,
                        transactions = newBlock.Transactions
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error mining block");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("wallet/transaction")]
        public IActionResult Transaction([FromBody] TransactionRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Sender) || string.IsNullOrEmpty(request.Recipient))
                {
                    return BadRequest(new { error = "Sender and recipient are required" });
                }

                if (request.Amount <= 0)
                {
                    return BadRequest(new { error = "Amount must be greater than 0" });
                }
                decimal senderBalance = _blockchain.GetBalance(request.Sender);
                if (senderBalance < request.Amount)
                {
                    return BadRequest(new
                    {
                        error = "Insufficient balance",
                        currentBalance = senderBalance,
                        requiredAmount = request.Amount
                    });
                }
                var transaction = new Transaction
                {
                    Sender = request.Sender,
                    Recipient = request.Recipient,
                    Amount = request.Amount,
                    Timestamp = request.Timestamp,
                    Signature = request.Signature,
                    PublicKey = request.PublicKey
                };

                if (!string.IsNullOrEmpty(request.Signature) && !string.IsNullOrEmpty(request.PublicKey))
                {
                    if (!transaction.VerifySignature())
                    {
                        return BadRequest(new { error = "Invalid transaction signature" });
                    }
                }

                int blockIndex = _blockchain.CreateTransaction(transaction);

                _logger.LogInformation($"New transaction: {request.Sender} -> {request.Recipient} ({request.Amount})");

                return Ok(new
                {
                    message = "Transaction created successfully",
                    transaction = new
                    {
                        id = transaction.TransactionId,
                        sender = transaction.Sender,
                        recipient = transaction.Recipient,
                        amount = transaction.Amount,
                        timestamp = transaction.Timestamp
                    },
                    blockIndex = blockIndex,
                    status = "pending"
                });
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Error send transaction");
                return StatusCode(500, new { error = ex.Message});
            }
        }

        [HttpGet("wallet/transactions")]
        public IActionResult GetTransactionHistory([FromQuery] string walletAddress)
        {
            try
            {
                if (string.IsNullOrEmpty(walletAddress))
                {
                    return BadRequest(new { error = "Wallet address is required" });
                }

                var transactions = _blockchain.GetTransactionHistory(walletAddress);

                return Ok(new
                {
                    walletAddress = walletAddress,
                    transactions = transactions.Select(t => new
                    {
                        id = t.TransactionId,
                        sender = t.Sender,
                        recipient = t.Recipient,
                        amount = t.Amount,
                        timestamp = t.Timestamp,
                        type = t.Sender == walletAddress ? "outgoing" : "incoming"
                    }),
                    totalCount = transactions.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting transaction history");
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}