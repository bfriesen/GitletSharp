using System;
using System.Security.Cryptography;
using System.Text;

namespace GitletSharp.Core
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

        /// <summary>
        /// allows execution of a command on a remote
        // repository.  It returns an anonymous function that takes another
        // function `fn`.  When the anonymous function is run, it switches
        // to `remotePath`, executes `fn`, then switches back to the
        // original directory.
        /// </summary>
        public static Func<Func<T>, T> Remote<T>(string remotePath)
        {
            return func =>
            {
                var current = Files.CurrentPath;
                Files.CurrentPath = remotePath;
                var result = func();
                Files.CurrentPath = current;
                return result;
            };
        }
    }
}
