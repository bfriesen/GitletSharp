using System.IO;
using System.Linq;

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
    }
}
