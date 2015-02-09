using GitletSharp.Core;

namespace GitletSharp.Tests
{
    public static class Steps
    {
        public static void Init()
        {
            Gitlet.Init();
        }

        public static void FirstAdd()
        {
            Files.CurrentPath.Combine("number.txt").WriteAllText("first!");
            Gitlet.Add("number.txt");
        }
    }
}