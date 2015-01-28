using System.Security.Cryptography;
using System.Text;

namespace GitletSharp
{
    internal static class Util
    {
        private static readonly MD5 _md5 = MD5.Create();

        public static string Hash(string content)
        {
            var data = _md5.ComputeHash(Encoding.UTF8.GetBytes(content));

            var sb = new StringBuilder();

            foreach (var b in data)
            {
                sb.Append(b.ToString("x2"));
            }

            return sb.ToString();
        }
    }
}
