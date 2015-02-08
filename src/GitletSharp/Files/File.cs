using System.IO;
using System.Linq;

namespace GitletSharp
{
    internal class File : ITree
    {
        private readonly string _name;
        private readonly string _contents;

        public File(string name, string contents = null)
        {
            _name = name;
            _contents = contents ?? "";
        }

        public string Name { get { return _name; } }
        public string Contents { get { return _contents; } }

        public static bool Exists(string path)
        {
            return System.IO.File.Exists(path);
        }

        public static string ReadAllText(string path)
        {
            return System.IO.File.ReadAllText(path);
        }

        public static string[] ReadAllLines(string path)
        {
            return System.IO.File.ReadAllLines(path).Where(s => s != "").ToArray();
        }

        public static void WriteAllText(string path, string contents)
        {
            var directory = Path.GetDirectoryName(path);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            System.IO.File.WriteAllText(path, contents);
        }

        public static void Delete(string path)
        {
            System.IO.File.Delete(path);
        }
    }
}
