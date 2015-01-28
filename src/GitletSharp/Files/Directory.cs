using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GitletSharp
{
    internal class Directory : ITree
    {
        private readonly string _name;
        private readonly List<ITree> _contents;

        public Directory()
            : this(null, (IEnumerable<ITree>)null)
        {
        }

        public Directory(params ITree[] contents)
            : this(null, contents)
        {
        }

        public Directory(string name, params ITree[] contents)
            : this(name, (IEnumerable<ITree>)contents)
        {
        }

        public Directory(string name = null, IEnumerable<ITree> contents = null)
        {
            _name = name ?? "";
            _contents = (contents as List<ITree>) ?? (contents == null ? new List<ITree>() : contents.ToList());
        }

        public string Name { get { return _name; } }
        public IEnumerable<ITree> Contents { get { return _contents; } }

        public void Add(ITree tree)
        {
            _contents.Add(tree);
        }

        public Directory GetOrAddDirectory(string dirName)
        {
            var dir = _contents.OfType<Directory>().FirstOrDefault(item => item.Name == dirName);

            if (dir != null)
            {
                return dir;
            }

            dir = new Directory(dirName);
            Add(dir);
            return dir;
        }

        public static bool Exists(string path)
        {
            return System.IO.Directory.Exists(path);
        }

        public static void CreateDirectory(string path)
        {
            System.IO.Directory.CreateDirectory(path);
        }

        public static string[] GetFiles(string path)
        {
            return GetFilesEnumerable(path).ToArray();
        }

        private static IEnumerable<string> GetFilesEnumerable(string path)
        {
            var dir = new DirectoryInfo(path);

            if (dir.Name == ".gitlet")
            {
                yield break;
            }

            // If it's a file, not a directory, just return that file.
            if ((int)dir.Attributes != -1 && (dir.Attributes & FileAttributes.Directory) != FileAttributes.Directory)
            {
                yield return path;
                yield break;
            }

            foreach (var file in dir.GetFiles())
            {
                yield return file.FullName;
            }

            foreach (var subDir in dir.GetDirectories())
            {
                foreach (var subDirFile in GetFiles(subDir.FullName))
                {
                    yield return subDirFile;
                }
            }
        }
    }
}
