using System.Runtime.CompilerServices;
using System.Security.Cryptography;

namespace LotteryBitcoinMiner.Core
{
    public static class SHA256Hasher
    {
        // SHA256 constants
        private static readonly uint[] K = {
            0x428a2f98, 0x71374491, 0xb5c0fbcf, 0xe9b5dba5, 0x3956c25b, 0x59f111f1, 0x923f82a4, 0xab1c5ed5,
            0xd807aa98, 0x12835b01, 0x243185be, 0x550c7dc3, 0x72be5d74, 0x80deb1fe, 0x9bdc06a7, 0xc19bf174,
            0xe49b69c1, 0xefbe4786, 0x0fc19dc6, 0x240ca1cc, 0x2de92c6f, 0x4a7484aa, 0x5cb0a9dc, 0x76f988da,
            0x983e5152, 0xa831c66d, 0xb00327c8, 0xbf597fc7, 0xc6e00bf3, 0xd5a79147, 0x06ca6351, 0x14292967,
            0x27b70a85, 0x2e1b2138, 0x4d2c6dfc, 0x53380d13, 0x650a7354, 0x766a0abb, 0x81c2c92e, 0x92722c85,
            0xa2bfe8a1, 0xa81a664b, 0xc24b8b70, 0xc76c51a3, 0xd192e819, 0xd6990624, 0xf40e3585, 0x106aa070,
            0x19a4c116, 0x1e376c08, 0x2748774c, 0x34b0bcb5, 0x391c0cb3, 0x4ed8aa4a, 0x5b9cca4f, 0x682e6ff3,
            0x748f82ee, 0x78a5636f, 0x84c87814, 0x8cc70208, 0x90befffa, 0xa4506ceb, 0xbef9a3f7, 0xc67178f2
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint RotateRight(uint value, int count)
        {
            return (value >> count) | (value << (32 - count));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Ch(uint x, uint y, uint z)
        {
            return (x & y) ^ (~x & z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Maj(uint x, uint y, uint z)
        {
            return (x & y) ^ (x & z) ^ (y & z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Sigma0(uint x)
        {
            return RotateRight(x, 2) ^ RotateRight(x, 13) ^ RotateRight(x, 22);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Sigma1(uint x)
        {
            return RotateRight(x, 6) ^ RotateRight(x, 11) ^ RotateRight(x, 25);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint sigma0(uint x)
        {
            return RotateRight(x, 7) ^ RotateRight(x, 18) ^ (x >> 3);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint sigma1(uint x)
        {
            return RotateRight(x, 17) ^ RotateRight(x, 19) ^ (x >> 10);
        }

        public static byte[] ComputeHash(byte[] data)
        {
            // Initial hash values
            uint h0 = 0x6a09e667;
            uint h1 = 0xbb67ae85;
            uint h2 = 0x3c6ef372;
            uint h3 = 0xa54ff53a;
            uint h4 = 0x510e527f;
            uint h5 = 0x9b05688c;
            uint h6 = 0x1f83d9ab;
            uint h7 = 0x5be0cd19;

            // Pre-processing
            var paddedData = PadMessage(data);
            
            // Process message in 512-bit chunks
            for (int chunkStart = 0; chunkStart < paddedData.Length; chunkStart += 64)
            {
                var w = new uint[64];
                
                // Break chunk into sixteen 32-bit big-endian words
                for (int i = 0; i < 16; i++)
                {
                    w[i] = ((uint)paddedData[chunkStart + i * 4] << 24) |
                           ((uint)paddedData[chunkStart + i * 4 + 1] << 16) |
                           ((uint)paddedData[chunkStart + i * 4 + 2] << 8) |
                           ((uint)paddedData[chunkStart + i * 4 + 3]);
                }

                // Extend the sixteen 32-bit words into sixty-four 32-bit words
                for (int i = 16; i < 64; i++)
                {
                    w[i] = sigma1(w[i - 2]) + w[i - 7] + sigma0(w[i - 15]) + w[i - 16];
                }

                // Initialize working variables
                uint a = h0, b = h1, c = h2, d = h3, e = h4, f = h5, g = h6, h = h7;

                // Main loop
                for (int i = 0; i < 64; i++)
                {
                    uint temp1 = h + Sigma1(e) + Ch(e, f, g) + K[i] + w[i];
                    uint temp2 = Sigma0(a) + Maj(a, b, c);
                    h = g;
                    g = f;
                    f = e;
                    e = d + temp1;
                    d = c;
                    c = b;
                    b = a;
                    a = temp1 + temp2;
                }

                // Add this chunk's hash to result so far
                h0 += a;
                h1 += b;
                h2 += c;
                h3 += d;
                h4 += e;
                h5 += f;
                h6 += g;
                h7 += h;
            }

            // Produce the final hash value as a 256-bit number (big-endian)
            var result = new byte[32];
            WriteUInt32BigEndian(result, 0, h0);
            WriteUInt32BigEndian(result, 4, h1);
            WriteUInt32BigEndian(result, 8, h2);
            WriteUInt32BigEndian(result, 12, h3);
            WriteUInt32BigEndian(result, 16, h4);
            WriteUInt32BigEndian(result, 20, h5);
            WriteUInt32BigEndian(result, 24, h6);
            WriteUInt32BigEndian(result, 28, h7);

            return result;
        }

        public static byte[] ComputeDoubleSHA256(byte[] data)
        {
            var firstHash = ComputeHash(data);
            return ComputeHash(firstHash);
        }

        private static byte[] PadMessage(byte[] message)
        {
            long originalLength = message.Length;
            long bitLength = originalLength * 8;

            // Calculate padding length
            int paddingLength = (int)(56 - (originalLength % 64));
            if (paddingLength <= 0)
                paddingLength += 64;

            var paddedMessage = new byte[originalLength + paddingLength + 8];
            
            // Copy original message
            Array.Copy(message, paddedMessage, originalLength);
            
            // Add padding bit
            paddedMessage[originalLength] = 0x80;
            
            // Add length as 64-bit big-endian integer
            for (int i = 0; i < 8; i++)
            {
                paddedMessage[paddedMessage.Length - 8 + i] = (byte)(bitLength >> (56 - i * 8));
            }

            return paddedMessage;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteUInt32BigEndian(byte[] buffer, int offset, uint value)
        {
            buffer[offset] = (byte)(value >> 24);
            buffer[offset + 1] = (byte)(value >> 16);
            buffer[offset + 2] = (byte)(value >> 8);
            buffer[offset + 3] = (byte)value;
        }

        // Optimized method for Bitcoin mining (block header hashing)
        public static byte[] HashBlockHeader(byte[] blockHeader)
        {
            if (blockHeader.Length != 80)
                throw new ArgumentException("Block header must be exactly 80 bytes");

            return ComputeDoubleSHA256(blockHeader);
        }

        // Convert hash to hex string (little-endian for Bitcoin)
        public static string HashToHexString(byte[] hash)
        {
            var reversed = new byte[hash.Length];
            for (int i = 0; i < hash.Length; i++)
            {
                reversed[i] = hash[hash.Length - 1 - i];
            }
            return Convert.ToHexString(reversed).ToLowerInvariant();
        }
    }
}
