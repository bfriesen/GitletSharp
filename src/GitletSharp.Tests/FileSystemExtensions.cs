using System.IO;
using System.Linq;

namespace GitletSharp.Tests
{
    public static class FileSystemExtensions
    {
        public static string Combine(this string path, params string[] childPaths)
        {
            return Path.Combine(new[] { path }.Concat(childPaths).ToArray());
        }

        public static bool DirectoryExists(this string path)
        {
            return System.IO.Directory.Exists(path);
        }

        public static bool FileExists(this string path)
        {
            return System.IO.File.Exists(path);
        }

        public static void DeleteDirectory(this string path)
        {
            System.IO.Directory.Delete(path, true);
        }

        public static void DeleteFile(this string path)
        {
            System.IO.File.Delete(path);
        }

        public static void CreateDirectory(this string path)
        {
            System.IO.Directory.CreateDirectory(path);
        }

        public static void WriteAllText(this string path, string contents)
        {
            System.IO.File.WriteAllText(path, contents);
        }

        public static string ReadAllText(this string path)
        {
            return
                System.IO.File.Exists(path)
                    ? System.IO.File.ReadAllText(path)
                    : null;
        }
    }
}