using System.IO;
using System.Text.RegularExpressions;

namespace GitletSharp
{
    internal static class Refs
    {
        /// <summary>
        // Returns true if `HEAD` contains a commit hash, rather than the ref of a branch.
        /// </summary>
        public static bool IsHeadDetached()
        {
            var head = Files.Read(Path.Combine(Files.GitletPath(), "HEAD"));
            return !head.Contains("refs");
        }

        public static string HeadBranchName()
        {
            if (!IsHeadDetached())
            {
                var head = Files.Read(Path.Combine(Files.GitletPath(), "HEAD"));
                return Regex.Match(head, @"refs/heads/(.+)").Groups[1].Value;
            }

            return null;
        }
    }
}
