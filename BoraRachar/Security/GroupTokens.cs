using System.Security.Cryptography;
using System.Text;

namespace BoraRachar.Security
{
    public static class GroupTokens
    {
        public const string HeaderName = "X-Group-Token";
        public static string Generate() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        public static bool Matches(string token, string? storedHash)
        {
            if (token.Length != 64 || storedHash is null || storedHash.Length != 64) return false;
            try
            {
                return CryptographicOperations.FixedTimeEquals(
                    Convert.FromHexString(Hash(token)), Convert.FromHexString(storedHash));
            }
            catch (FormatException) { return false; }
        }
    }
}
