using System.Text;

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

            // Convert bits to target
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
            else
            {
                int offset = (int)(32 - exponent);
                if (offset >= 0 && offset < 29)
                {
                    Target[offset] = (byte)(mantissa >> 16);
                    Target[offset + 1] = (byte)(mantissa >> 8);
                    Target[offset + 2] = (byte)mantissa;
                }
            }

            // Calculate difficulty
            var maxTarget = new byte[32];
            maxTarget[0] = 0x00;
            maxTarget[1] = 0x00;
            maxTarget[2] = 0x00;
            maxTarget[3] = 0xFF;
            maxTarget[4] = 0xFF;
            // Rest are zeros

            Difficulty = CalculateDifficulty(maxTarget, Target);
        }

        public void UpdatePoolShareTargetAndDificulty(double poolDifficulty)
        {
            if (poolDifficulty <= 0)
                return;

            // Calculate target from pool difficulty
            // Target = max_target / difficulty
            var maxTarget = new byte[32];
            maxTarget[0] = 0x00;
            maxTarget[1] = 0x00;
            maxTarget[2] = 0x00;
            maxTarget[3] = 0xFF;
            maxTarget[4] = 0xFF;
            // Rest are zeros

            // Convert max target to big integer value
            double maxTargetValue = 0;
            for (int i = 0; i < 32; i++)
            {
                maxTargetValue = maxTargetValue * 256 + maxTarget[i];
            }

            // Calculate new target value
            double newTargetValue = maxTargetValue / poolDifficulty;

            // Convert back to byte array
            PoolShareTarget = new byte[32];
            double tempValue = newTargetValue;
            
            // Fill target bytes from least significant to most significant
            for (int i = 31; i >= 0; i--)
            {
                PoolShareTarget[i] = (byte)(tempValue % 256);
                tempValue = Math.Floor(tempValue / 256);
                if (tempValue == 0) break;
            }

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
            // Calculate the difficulty of a given hash
            // Difficulty = max_target / hash_as_number
            
            // Convert hash to a big integer-like value for comparison
            double hashValue = 0;
            double maxTargetValue = 0;
            
            // Use the maximum target (difficulty 1)
            var maxTarget = new byte[32];
            maxTarget[0] = 0x00;
            maxTarget[1] = 0x00;
            maxTarget[2] = 0x00;
            maxTarget[3] = 0xFF;
            maxTarget[4] = 0xFF;
            // Rest are zeros
            
            // Calculate values (simplified approach for display purposes)
            for (int i = 0; i < 8; i++) // Use first 8 bytes for calculation
            {
                hashValue = hashValue * 256 + hash[i];
                maxTargetValue = maxTargetValue * 256 + maxTarget[i];
            }
            
            if (hashValue == 0) return double.MaxValue; // Theoretical maximum
            
            return maxTargetValue / hashValue;
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

        private static double CalculateDifficulty(byte[] maxTarget, byte[] currentTarget)
        {
            // Simple difficulty calculation
            // In reality, this would be more complex
            double maxValue = 0;
            double currentValue = 0;

            for (int i = 0; i < 32; i++)
            {
                maxValue = maxValue * 256 + maxTarget[i];
                currentValue = currentValue * 256 + currentTarget[i];
            }

            return currentValue > 0 ? maxValue / currentValue : 1.0;
        }

        public override string ToString()
        {
            return $"Job: {JobId}, Difficulty: {Difficulty:F2}, Target: {BytesToHexString(Target)}";
        }
    }
}
