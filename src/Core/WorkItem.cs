using System.Text;
using System.Numerics;

namespace LotteryBitcoinMiner.Core
{
    public class WorkItem
    {
        public string JobId { get; set; } = "";
        public string PreviousBlockHash { get; set; } = "";
        public string CoinbaseTransaction { get; set; } = "";
        public string[] MerkleTree { get; set; } = Array.Empty<string>();
        public string Version { get; set; } = "";
        public string Bits { get; set; } = "";
        public string Time { get; set; } = "";
        public uint StartNonce { get; set; }
        public uint EndNonce { get; set; }
        public byte[] Target { get; set; } = new byte[32];
        public double Difficulty { get; set; } = 1.0;
        
        public byte[] PoolShareTarget { get; set; } = new byte[32];
        public double PoolShareDifficulty { get; set; } = 1.0;
        public string ExtraNonce2 { get; set; } = "";
        
        // Computed values
        public string MerkleRoot { get; set; } = "";
        public byte[] BlockHeader { get; private set; } = new byte[80];

        public WorkItem()
        {
        }

        public WorkItem
            (
                string jobId,
                string previousBlockHash,
                string coinbaseTransaction,
                string[] merkleTree,
                string version,
                string bits,
                string time
            )
        {
            JobId = jobId;
            PreviousBlockHash = previousBlockHash;
            CoinbaseTransaction = coinbaseTransaction;
            MerkleTree = merkleTree;
            Version = version;
            Bits = bits;
            Time = time;
            
            ComputeMerkleRoot();
            ComputeTarget();
        }

        public void ComputeMerkleRoot()
        {
            if (string.IsNullOrEmpty(CoinbaseTransaction))
            {
                MerkleRoot = "";
                return;
            }

            var transactions = new List<string> { CoinbaseTransaction };
            transactions.AddRange(MerkleTree);

            // Compute Merkle root
            var currentLevel = transactions.Select(tx => HexStringToBytes(tx)).ToList();
            
            while (currentLevel.Count > 1)
            {
                var nextLevel = new List<byte[]>();
                
                for (int i = 0; i < currentLevel.Count; i += 2)
                {
                    byte[] left = currentLevel[i];
                    byte[] right = (i + 1 < currentLevel.Count) ? currentLevel[i + 1] : currentLevel[i];
                    
                    var combined = new byte[left.Length + right.Length];
                    Array.Copy(left, 0, combined, 0, left.Length);
                    Array.Copy(right, 0, combined, left.Length, right.Length);
                    
                    var hash = SHA256Hasher.ComputeDoubleSHA256(combined);
                    nextLevel.Add(hash);
                }
                
                currentLevel = nextLevel;
            }

            MerkleRoot = currentLevel.Count > 0 ? BytesToHexString(currentLevel[0]) : "";
        }

        public void ComputeTarget()
        {
            if (string.IsNullOrEmpty(Bits))
            {
                Target = new byte[32];
                Difficulty = 1.0;
                return;
            }

            // Convert bits to target using correct Bitcoin algorithm
            uint bits = Convert.ToUInt32(Bits, 16);
            uint exponent = bits >> 24;
            uint mantissa = bits & 0x00ffffff;

            Target = new byte[32];
            
            if (exponent <= 3)
            {
                mantissa >>= (int)(8 * (3 - exponent));
                Target[29] = (byte)(mantissa >> 16);
                Target[30] = (byte)(mantissa >> 8);
                Target[31] = (byte)mantissa;
            }
            else if (exponent < 32)
            {
                int offset = (int)(32 - exponent);
                if (offset >= 0 && offset <= 29)
                {
                    Target[offset] = (byte)(mantissa >> 16);
                    if (offset + 1 < 32) Target[offset + 1] = (byte)(mantissa >> 8);
                    if (offset + 2 < 32) Target[offset + 2] = (byte)mantissa;
                }
            }

            // Calculate difficulty using corrected method
            Difficulty = CalculateDifficultyFromTarget(Target);
        }

        public void UpdatePoolShareTargetAndDificulty(double poolDifficulty)
        {
            if (poolDifficulty <= 0)
                return;

            // Calculate target from pool difficulty using correct max target
            // Target = max_target / difficulty
            var maxTargetBytes = new byte[32];
            maxTargetBytes[4] = 0xFF;
            maxTargetBytes[5] = 0xFF;
            // Rest are zeros

            // Use BigInteger for precise calculation
            var maxTarget = new BigInteger(maxTargetBytes, isUnsigned: true, isBigEndian: true);
            var poolTarget = maxTarget / (BigInteger)poolDifficulty;

            // Convert back to byte array
            PoolShareTarget = new byte[32];
            var targetBytes = poolTarget.ToByteArray(isUnsigned: true, isBigEndian: true);
            
            // Copy bytes, ensuring we don't exceed 32 bytes
            int copyLength = Math.Min(targetBytes.Length, 32);
            int offset = 32 - copyLength;
            Array.Copy(targetBytes, 0, PoolShareTarget, offset, copyLength);

            // Update difficulty to match pool difficulty
            PoolShareDifficulty = poolDifficulty;
        }

        public void PrepareBlockHeader(uint nonce)
        {
            if (BlockHeader.Length != 80)
                BlockHeader = new byte[80];

            // Version (4 bytes)
            var versionBytes = HexStringToBytes(Version);
            Array.Copy(versionBytes, 0, BlockHeader, 0, 4);

            // Previous block hash (32 bytes)
            var prevHashBytes = HexStringToBytes(PreviousBlockHash);
            Array.Reverse(prevHashBytes); // Bitcoin uses little-endian
            Array.Copy(prevHashBytes, 0, BlockHeader, 4, 32);

            // Merkle root (32 bytes)
            var merkleBytes = HexStringToBytes(MerkleRoot);
            Array.Reverse(merkleBytes); // Bitcoin uses little-endian
            Array.Copy(merkleBytes, 0, BlockHeader, 36, 32);

            // Time (4 bytes)
            var timeBytes = HexStringToBytes(Time);
            Array.Copy(timeBytes, 0, BlockHeader, 68, 4);

            // Bits (4 bytes)
            var bitsBytes = HexStringToBytes(Bits);
            Array.Copy(bitsBytes, 0, BlockHeader, 72, 4);

            // Nonce (4 bytes)
            BlockHeader[76] = (byte)nonce;
            BlockHeader[77] = (byte)(nonce >> 8);
            BlockHeader[78] = (byte)(nonce >> 16);
            BlockHeader[79] = (byte)(nonce >> 24);
        }

        public bool IsValidHash(byte[] hash)
        {
            // Compare hash with target (both in big-endian)
            for (int i = 0; i < 32; i++)
            {
                if (hash[i] < PoolShareTarget[i])
                    return true;
                if (hash[i] > PoolShareTarget[i])
                    return false;
            }
            return true; // Equal is also valid
        }

        public double CalculateHashDifficulty(byte[] hash)
        {
            // Calculate the difficulty of a given hash using correct max target
            // Difficulty = max_target / hash_as_number
            
            // Bitcoin max target: 0x00000000FFFF0000000000000000000000000000000000000000000000000000
            var maxTargetBytes = new byte[32];
            maxTargetBytes[4] = 0xFF;
            maxTargetBytes[5] = 0xFF;
            // Rest are zeros
            
            // Use BigInteger for precise calculation
            var maxTarget = new BigInteger(maxTargetBytes, isUnsigned: true, isBigEndian: true);
            var hashValue = new BigInteger(hash, isUnsigned: true, isBigEndian: true);
            
            if (hashValue == 0) return double.MaxValue; // Theoretical maximum
            
            // Calculate difficulty = max_target / hash_value
            var difficulty = (double)maxTarget / (double)hashValue;
            return difficulty;
        }

        private static byte[] HexStringToBytes(string hex)
        {
            if (string.IsNullOrEmpty(hex))
                return Array.Empty<byte>();

            hex = hex.Replace(" ", "").Replace("-", "");
            if (hex.Length % 2 != 0)
                hex = "0" + hex;

            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }
            return bytes;
        }

        private static string BytesToHexString(byte[] bytes)
        {
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        private static double CalculateDifficultyFromTarget(byte[] target)
        {
            // Bitcoin max target: 0x00000000FFFF0000000000000000000000000000000000000000000000000000
            var maxTargetBytes = new byte[32];
            maxTargetBytes[4] = 0xFF;
            maxTargetBytes[5] = 0xFF;
            // Rest are zeros (positions 0-3 and 6-31)
            
            // Use BigInteger for precise calculation
            var maxTarget = new BigInteger(maxTargetBytes, isUnsigned: true, isBigEndian: true);
            var currentTarget = new BigInteger(target, isUnsigned: true, isBigEndian: true);
            
            if (currentTarget == 0) return double.MaxValue;
            
            // Calculate difficulty = max_target / current_target
            var difficulty = (double)maxTarget / (double)currentTarget;
            return difficulty;
        }

        private static double CalculateDifficulty(byte[] maxTarget, byte[] currentTarget)
        {
            // Legacy method - kept for compatibility
            // Use BigInteger for more precise calculation
            var maxTargetBig = new BigInteger(maxTarget, isUnsigned: true, isBigEndian: true);
            var currentTargetBig = new BigInteger(currentTarget, isUnsigned: true, isBigEndian: true);
            
            if (currentTargetBig == 0) return 1.0;
            
            return (double)maxTargetBig / (double)currentTargetBig;
        }

        public override string ToString()
        {
            return $"Job: {JobId}, Difficulty: {Difficulty:F2}, Target: {BytesToHexString(Target)}";
        }
    }
}
