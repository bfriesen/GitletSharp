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

        public static string[] AllObjects()
        {
            return
                Directory.GetFiles(Path.Combine(Files.GitletPath(), "objects"))
                    .Select(Objects.Read)
                    .ToArray();
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

        /// <summary>
        /// takes a tree hash and finds the corresponding tree
        /// object.  It reads the connected graph of tree objects into a
        /// nested JS object, like:<br/>
        /// `{ file1: "hash(1)", src: { file2:  "hash(2)" }`
        /// </summary>
        public static Directory FileTree(string treeHash)
        {
            Func<string, Directory, Directory> fileTree = null;

            fileTree = (hash, dir) =>
            {
                foreach (var line in Read(hash).Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var lineTokens = line.Split(' ');

                    var name = lineTokens[2];

                    var nodeValue =
                        lineTokens[0] == "tree"
                            ? (ITree)fileTree(lineTokens[1], new Directory(name))
                            : new File(name, lineTokens[1]);

                    dir.Add(nodeValue);
                }

                return dir;
            };

            return fileTree(treeHash, new Directory());
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

        /// <summary>
        /// Takes the hash of a commit and reads the content
        /// stored in the tree on the commit.  It turns that tree into a
        /// table of content that maps filenames to hashes of the files'
        /// content, like: `{ "file1": hash(1), "a/file2": "hash(2)" }`
        /// </summary>
        public static Dictionary<string, string> CommitToc(string hash)
        {
            return Files.FlattenNestedTree(Objects.FileTree(Objects.TreeHash(Objects.Read(hash))));
        }
    }
}
