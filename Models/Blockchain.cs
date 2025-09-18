using Newtonsoft.Json;
using System.Security.Cryptography;
using System.Text;

namespace Blockchain.Models
{
    public class Blockchain
    {
        public List<Block> Chain { get; private set; }
        public List<Transaction> PendingTransactions { get; private set; }
        public int Difficulty { get; private set; } = 4;

        public Blockchain()
        {
            Chain = [];
            PendingTransactions = [];
            CreateGenesisBlock();
        }
        /// <summary>
        /// Создание Genesis-блока
        /// </summary>
        private void CreateGenesisBlock()
        {
            var genesisBlock = new Block
            {
                Index = 0,
                Timestamp = DateTime.UtcNow,
                Transactions = [],
                PreviousHash = "0",
                Proof = 100,
                Nonce = 0
            };

            genesisBlock.Hash = CalculateHash(genesisBlock);
            Chain.Add(genesisBlock);
        }

        /// <summary>
        /// Создание блока
        /// </summary>
        /// <param name="proof"></param>
        /// <param name="previousHash"></param>
        /// <returns></returns>
        public Block CreateBlock(int proof, string previousHash = null)
        {
            var block = new Block
            {
                Index = Chain.Count,
                Timestamp = DateTime.UtcNow,
                Transactions = [.. PendingTransactions],
                PreviousHash = previousHash ?? GetLastBlock().Hash,
                Proof = proof,
                Nonce = 0
            };

            block.Hash = CalculateHash(block);

            MineBlock(block);

            PendingTransactions.Clear();
            Chain.Add(block);

            return block;
        }
        /// <summary>
        /// Майнинг нового блока
        /// </summary>
        /// <param name="block"></param>
        public void MineBlock(Block block)
        {
            string target = new('0', Difficulty);

            while (!block.Hash.StartsWith(target) || string.IsNullOrEmpty(block.Hash))
            {
                block.Nonce++;
                block.Hash = CalculateHash(block);
            }
        }

        /// <summary>
        /// Создание транзакции
        /// </summary>
        /// <param name="transaction"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public int CreateTransaction(Transaction transaction)
        {
            if (!IsValidTransaction(transaction))
                throw new Exception("Invalid transaction");

            PendingTransactions.Add(transaction);
            return GetLastBlock().Index + 1;
        }

        /// <summary>
        /// Валидация транзакции
        /// </summary>
        /// <param name="transaction"></param>
        /// <returns></returns>
        private bool IsValidTransaction(Transaction transaction)
        {
            if (transaction.Amount <= 0)
                return false;

            if (transaction.Sender == transaction.Recipient)
                return false;

            if (transaction.Sender == "0")
                return true;

            decimal senderBalance = GetBalance(transaction.Sender);
            return senderBalance >= transaction.Amount;
        }

        /// <summary>
        /// Вычисление нулей хеша
        /// </summary>
        /// <param name="lastProof"></param>
        /// <returns></returns>
        public int ProofOfWork(int lastProof)
        {
            int proof = 0;
            while (!ValidProof(lastProof, proof))
            {
                proof++;
            }
            return proof;
        }

        /// <summary>
        /// Валидация вычисленных нулей
        /// </summary>
        /// <param name="lastProof"></param>
        /// <param name="proof"></param>
        /// <returns></returns>
        private bool ValidProof(int lastProof, int proof)
        {
            string guess = $"{lastProof}{proof}";
            byte[] guessBytes = Encoding.UTF8.GetBytes(guess);
            byte[] hashBytes = SHA256.HashData(guessBytes);
            string hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();

            return hash.StartsWith(new string('0', Difficulty));
        }

        /// <summary>
        /// Генерация хэш-суммы блока
        /// </summary>
        /// <param name="block"></param>
        /// <returns></returns>
        public static string CalculateHash(Block block)
        {
            string blockString = JsonConvert.SerializeObject(new
            {
                block.Index,
                block.Timestamp,
                block.Transactions,
                block.PreviousHash,
                block.Proof,
                block.Nonce
            });

            using (var sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(blockString));
                return BitConverter.ToString(bytes).Replace("-", "").ToLower();
            }
        }

        /// <summary>
        /// Получение последнего блока
        /// </summary>
        /// <returns></returns>
        public Block GetLastBlock()
        {
            return Chain[^1];
        }

        /// <summary>
        /// Получение баланса по адресу
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        public decimal GetBalance(string address)
        {
            decimal balance = 0;
            foreach (var block in Chain)
            {
                foreach (var transaction in block.Transactions)
                {
                    if (transaction.Recipient == address)
                        balance += transaction.Amount;

                    if (transaction.Sender == address)
                        balance -= transaction.Amount;
                }
            }
            
            return balance;
        }

        /// <summary>
        /// Получение истории транзакций
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        public List<Transaction> GetTransactionHistory(string address)
        {
            return [.. Chain
                .SelectMany(block => block.Transactions)
                .Where(t => t.Sender == address || t.Recipient == address)
                .OrderByDescending(t => t.Timestamp)];
        }

        /// <summary>
        /// Проверяет валидность цепочки блоков
        /// </summary>
        /// <param name="chain">Цепочка блоков для проверки</param>
        /// <returns>True если цепочка валидна, иначе False</returns>
        /// <remarks>
        /// Проверяет:
        /// 1. Корректность хэша каждого блока
        /// 2. Соответствие PreviousHash каждого блока хэшу предыдущего блока
        /// 3. Последовательность индексов блоков
        /// </remarks>
        public bool ValidateChain(List<Block> chain)
        {
            for (int i = 1; i < chain.Count; i++)
            {
                Block currentBlock = chain[i];
                Block previousBlock = chain[i - 1];

                if (currentBlock.Hash != CalculateHash(currentBlock))
                    return false;

                if (currentBlock.PreviousHash != previousBlock.Hash)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Добавляет транзакцию в список ожидающих обработки после валидации
        /// </summary>
        /// <param name="transaction">Транзакция для добавления</param>
        /// <remarks>
        /// Транзакция будет добавлена только если она проходит все проверки:
        /// - Корректность суммы
        /// - Валидность подписи
        /// - Достаточность баланса отправителя
        /// </remarks>
        public void AddPendingTransaction(Transaction transaction)
        {
            if (ValidateTransaction(transaction))
            {
                PendingTransactions.Add(transaction);
            }
        }

        /// <summary>
        /// Проверяет валидность транзакции
        /// </summary>
        /// <param name="transaction">Транзакция для проверки</param>
        /// <returns>True если транзакция валидна, иначе False</returns>
        /// <remarks>
        /// Выполняет следующие проверки:
        /// 1. Сумма должна быть положительной
        /// 2. Отправитель и получатель не должны совпадать
        /// 3. Для системных транзакций (майнинг) пропускает проверку подписи
        /// 4. Проверяет цифровую подпись транзакции
        /// 5. Проверяет достаточность баланса отправителя
        /// </remarks>
        public bool ValidateTransaction(Transaction transaction)
        {
            if (transaction.Amount <= 0) return false;
            if (string.IsNullOrEmpty(transaction.Sender) || string.IsNullOrEmpty(transaction.Recipient)) return false;
            if (transaction.Sender == transaction.Recipient) return false;

            if (transaction.Sender == "0") return true;

            if (!transaction.VerifySignature()) return false;

            decimal senderBalance = GetBalance(transaction.Sender);
            return senderBalance >= transaction.Amount;
        }

        /// <summary>
        /// Майнит блок с текущими ожидающими транзакциями и добавляет награду майнеру
        /// </summary>
        /// <param name="minerAddress">Адрес майнера для получения награды</param>
        /// <returns>Новый добытый блок</returns>
        /// <remarks>
        /// 1. Добавляет транзакцию-награду майнеру (1.0 монета)
        /// 2. Выполняет Proof-of-Work для нахождения валидного доказательства
        /// 3. Создает новый блок с транзакциями
        /// 4. Очищает список ожидающих транзакций
        /// </remarks>
        public Block MinePendingTransactions(string minerAddress)
        {
            var rewardTransaction = new Transaction
            {
                Sender = "0",
                Recipient = minerAddress,
                Amount = 1.0m, // Награда за блок
                Timestamp = DateTime.UtcNow
            };
            PendingTransactions.Add(rewardTransaction);

            int proof = ProofOfWork(GetLastBlock().Proof);
            var newBlock = CreateBlock(proof);

            return newBlock;
        }

        /// <summary>
        /// Проверяет валидность нового блока перед добавлением в цепочку
        /// </summary>
        /// <param name="newBlock">Новый блок для проверки</param>
        /// <returns>True если блок валиден, иначе False</returns>
        /// <remarks>
        /// Проверяет:
        /// 1. Последовательность индексов (должен быть следующим после последнего блока)
        /// 2. Соответствие PreviousHash хэшу предыдущего блока
        /// 3. Корректность хэша текущего блока
        /// 4. Валидность Proof-of-Work
        /// </remarks>
        public bool ValidateNewBlock(Block newBlock)
        {
            var lastBlock = GetLastBlock();

            // Проверяем последовательность
            if (newBlock.Index != lastBlock.Index + 1) return false;

            // Проверяем хэш предыдущего блока
            if (newBlock.PreviousHash != lastBlock.Hash) return false;

            // Проверяем хэш текущего блока
            if (newBlock.Hash != CalculateHash(newBlock)) return false;

            // Проверяем proof-of-work
            if (!ValidProof(lastBlock.Proof, newBlock.Proof)) return false;

            return true;
        }

        /// <summary>
        /// Добавляет новый блок в цепочку после успешной валидации
        /// </summary>
        /// <param name="block">Блок для добавления</param>
        /// <remarks>
        /// Перед добавлением проверяет блок на валидность.
        /// Удаляет транзакции из блока из списка ожидающих транзакций.
        /// </remarks>
        public void AddBlock(Block block)
        {
            if (ValidateNewBlock(block))
            {
                Chain.Add(block);
                PendingTransactions.RemoveAll(t => block.Transactions.Contains(t));
            }
        }

        /// <summary>
        /// Заменяет текущую цепочку на новую если она длиннее и валидна
        /// </summary>
        /// <param name="newChain">Новая цепочка блоков</param>
        /// <remarks>
        /// Выполняет замену только если:
        /// 1. Новая цепочка длиннее текущей
        /// 2. Новая цепочка прошла полную валидацию
        /// 3. Очищает ожидающие транзакции, которые уже включены в новую цепочку
        /// </remarks>
        public void ReplaceChain(List<Block> newChain)
        {
            if (newChain.Count > Chain.Count && ValidateChain(newChain))
            {
                Chain = newChain;
                // Очищаем pending transactions которые уже есть в новой цепи
                PendingTransactions.RemoveAll(pt =>
                    newChain.Any(block => block.Transactions.Any(t => t.TransactionId == pt.TransactionId)));
            }
        }
    }
}
