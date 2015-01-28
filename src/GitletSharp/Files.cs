using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GitletSharp
{
    internal static class Files
    {
        private static readonly string DirectorySeparatorString = Path.DirectorySeparatorChar.ToString();

        private static string _path = new DirectoryInfo(System.IO.Directory.GetCurrentDirectory()).FullName;

        static Files()
        {
            CurrentPath = System.IO.Directory.GetCurrentDirectory();
        }

        public static string CurrentPath
        {
            get { return _path; }
            set
            {
                if (!value.EndsWith(DirectorySeparatorString))
                {
                    value = value + "\\";
                }

                _path = value;
            }
        }

        public static bool InRepo()
        {
            return GitletPath() != null;
        }

        public static void AssertInRepo()
        {
            if (!InRepo())
            {
                throw new Exception("Not in gitlet repository.");
            }
        }

        public static string PathFromRepoRoot(string path)
        {
            return Relative(WorkingCopyPath(), path);
        }

        private static string Relative(string folder, string filespec)
        {
            if (filespec.StartsWith("."))
            {
                filespec = _path + filespec;
            }

            var dir = new DirectoryInfo(filespec);

            // Folders must end in a slash
            if ((dir.Attributes & FileAttributes.Directory) == FileAttributes.Directory
                && !filespec.EndsWith(DirectorySeparatorString))
            {
                filespec += DirectorySeparatorString;
            }

            Uri pathUri = new Uri(filespec);

            // Folders must end in a slash
            if (!folder.EndsWith(DirectorySeparatorString))
            {
                folder += DirectorySeparatorString;
            }

            Uri folderUri = new Uri(Path.GetFullPath(folder));

            var relative = Uri.UnescapeDataString(folderUri.MakeRelativeUri(pathUri).ToString().Replace('/', Path.DirectorySeparatorChar));
            return string.IsNullOrEmpty(relative) ? ".\\" : relative;
        }

        public static void Write(string file, string content)
        {
            WriteFilesFromTree(
                Files.Relative(Files.CurrentPath, file).Split(new[] { Path.DirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries)
                    .Reverse()
                    .Aggregate(
                        (ITree)new File(file, content),
                        (child, dir) => new Directory(dir, child)),
                DirectorySeparatorString);
        }

        public static void WriteFilesFromTree(ITree tree, string prefix)
        {
            var path = Path.Combine(prefix, tree.Name);

            var file = tree as File;
            if (file != null)
            {
                File.WriteAllText(path, file.Contents);
            }
            else
            {
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }

                var dir = (Directory)tree;

                foreach (var item in dir.Contents)
                {
                    WriteFilesFromTree(item, path);
                }
            }
        }

        public static string Read(string path)
        {
            if (File.Exists(path))
            {
                return File.ReadAllText(path);
            }

            return null;
        }

        public static string GitletPath()
        {
            var dir = _path;

            var dirInfo = new DirectoryInfo(dir);

            if (dirInfo.Exists)
            {
                var potentialConfigFile = Path.Combine(dir, "config");
                var potentialGitletPath = Path.Combine(dir, ".gitlet");

                if (File.Exists(potentialConfigFile))
                {
                    var config = File.ReadAllText(potentialConfigFile);

                    if (config.Contains("[core]"))
                    {
                        return dir;
                    }
                }
                else if (Directory.Exists(potentialGitletPath))
                {
                    return potentialGitletPath;
                }
            }

            return null;
        }

        public static string WorkingCopyPath(string path = null)
        {
            return Path.Combine(GitletPath(), "..", path ?? "");
        }

        public static string[] LsRecursive(string path)
        {
            return Directory.GetFiles(path);
        }

        public static Directory NestFlatTree(Dictionary<string, string> pathToContentMap)
        {
            var root = new Directory();

            foreach (var item in pathToContentMap)
            {
                var split = Files.Relative(Files.CurrentPath, item.Key).Split(Path.DirectorySeparatorChar);

                var dir = root;

                foreach (var dirName in split.Take(split.Length - 1))
                {
                    dir = dir.GetOrAddDirectory(dirName);
                }

                dir.Add(new File(split[split.Length - 1], item.Value));
            }

            return root;
        }

        public static string Absolute(string path)
        {
            if (path.StartsWith("."))
            {
                return _path + path;
            }

            return path;
        }
    }
}
