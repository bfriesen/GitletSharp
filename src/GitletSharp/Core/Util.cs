using System.Security.Cryptography;
using System.Text;

namespace GitletSharp
{
    internal static class Util
    {
        private static readonly SHA1 _sha1 = SHA1.Create();

        public static string Hash(string content)
        {
            var data = _sha1.ComputeHash(Encoding.UTF8.GetBytes(content));

            var sb = new StringBuilder();

            foreach (var b in data)
            {
                sb.Append(b.ToString("x2"));
            }

            return sb.ToString();
        }
    }
}
