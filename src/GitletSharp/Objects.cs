using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace GitletSharp
{
    internal static class Objects
    {
        public static string WriteTree(Directory dir)
        {
            var treeObject =
                string.Join(
                    "\n",
                    dir.Contents.Select(
                        item =>
                        {
                            var file = item as File;
                            if (file != null)
                            {
                                return "blob " + file.Contents + " " + file.Name;
                            }

                            return "tree " + WriteTree((Directory)item) + " " + item.Name;
                        })) + "\n";

            return Write(treeObject);
        }

        public static string Write(string content)
        {
            var hash = Util.Hash(content);
            Files.Write(Path.Combine(Files.GitletPath(), "objects", hash), content);
            return hash;
        }

        public static string Read(string objectHash)
        {
            if (objectHash != null)
            {
                var objectPath = Path.Combine(Files.GitletPath(), "objects", objectHash);
                if (File.Exists(objectPath))
                {
                    return Files.Read(objectPath);
                }
            }

            return null;
        }

        public static string TreeHash(string str)
        {
            if (Objects.Type(str) == "commit")
            {
                return Regex.Split(str, @"\s")[1];
            }

            return null;
        }

        public static bool Exists(string objectHash)
        {
            return objectHash != null
                   && File.Exists(Path.Combine(Files.GitletPath(), "objects", objectHash));
        }

        public static string WriteCommit(string treeHash, string message, string[] parentHashes)
        {
            return Write("commit " + treeHash + "\n" +
                          string.Join("", parentHashes.Select(h => "parent " + h + "\n")) +
                          "Date:  " + DateTime.UtcNow.ToString("O") + "\n" +
                          "\n" +
                          "    " + message + "\n");
        }

        private static readonly Dictionary<string, string> _types = new Dictionary<string, string>
        {
            { "commit", "commit" },
            { "tree", "tree" },
            { "blob", "tree" },
        }; 

        public static string Type(string str)
        {
            string value;

            var key = str.Split(' ')[0];
            return _types.TryGetValue(key, out value) ? value : "blob";
        }
    }
}
