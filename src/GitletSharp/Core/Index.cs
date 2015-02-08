using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace GitletSharp
{
    internal static class Index
    {
        public static bool HasFile(string path, int stage)
        {
            return Read().ContainsKey(GetKey(path, stage));
        }

        public static Dictionary<Key, string> Read()
        {
            var indexFilePath = Path.Combine(Files.GitletPath(), "index");

            return
                (File.Exists(indexFilePath) ? File.ReadAllLines(indexFilePath) : new string[0])
                .ToDictionary(
                    line => new Key(line.Split(' ')[0], int.Parse(line.Split(' ')[1])),
                    line => line.Split(' ')[2]);
        }

        public static Dictionary<string, string> Toc()
        {
            var index = Read();
            return index.ToDictionary(item => item.Key.Path, item => item.Value);
        }

        public static bool IsFileInConflict(string path)
        {
            return HasFile(path, 2);
        }

        public static void WriteAdd(string path)
        {
            if (IsFileInConflict(path))
            {
                RmEntry(path, 1);
                RmEntry(path, 2);
                RmEntry(path, 3);
            }

            WriteEntry(path, 0, Files.Read(Files.WorkingCopyPath(path)));
        }

        public static void WriteRm(string path)
        {
            RmEntry(path, 0);
        }

        private static void WriteEntry(string path, int stage, string content)
        {
            var index = Read();
            index[GetKey(path, stage)] = Objects.Write(content);
            Write(index);
        }

        private static void RmEntry(string path, int stage)
        {
            var index = Read();
            index.Remove(GetKey(path, stage));
            Write(index);
        }

        private static void Write(Dictionary<Key, string> index)
        {
            var indexStr =
                string.Join(
                    "\n",
                    index.Select(item => item.Key.Path + " " + item.Key.Stage + " " + item.Value))
                    + "\n";
            Files.Write(Path.Combine(Files.GitletPath(), "index"), indexStr);
        }

        public static Dictionary<string, string> WorkingCopyToc()
        {
            return
                Index.Read().Keys
                    .Select(k => k.Path)
                    .Where(path => File.Exists(Files.WorkingCopyPath(path)))
                    .Aggregate(
                        new Dictionary<string, string>(),
                        (dictionary, path) =>
                        {
                            dictionary[path] = Util.Hash(Files.Read(Files.WorkingCopyPath(path)));
                            return dictionary;
                        });
        }

        public static string[] MatchingFiles(string pathSpec)
        {
            var searchPath = Files.PathFromRepoRoot(pathSpec).Replace(".", @"\.");
            return Read().Keys.Select(key => key.Path).Where(path => Regex.IsMatch(path, "^" + searchPath)).ToArray();
        }

        private static Key GetKey(string path, int stage)
        {
            return new Key(Files.PathFromRepoRoot(path), stage);
        }

        public struct Key
        {
            public readonly string Path;
            public readonly int Stage;

            public Key(string path, int stage)
            {
                Path = path;
                Stage = stage;
            }

            public override bool Equals(object obj)
            {
                if (obj is Key)
                {
                    var other = (Key)obj;
                    return Equals(Path, other.Path) && Stage == other.Stage;
                }

                return false;
            }

            public override int GetHashCode()
            {
                return Tuple.Create(Path, Stage).GetHashCode();
            }
        }

        public static string[] ConflictedPaths()
        {
            // TODO: Implement
            return new string[0];
        }
    }
}
