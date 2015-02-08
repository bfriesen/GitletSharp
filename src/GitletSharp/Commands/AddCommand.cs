using GitletSharp.Core;
using ManyConsole;

namespace GitletSharp
{
    internal class AddCommand : ConsoleCommand
    {
        public AddCommand()
        {
            IsCommand("add", "Add file(s) to the index");

            HasAdditionalArguments(1, " <path>");

            HasOption("cd=", "Sets the current directory.", dir => Files.CurrentPath = dir);
        }

        public override int Run(string[] remainingArguments)
        {
            var path = remainingArguments[0];
            Gitlet.Add(path);
            return 0;
        }
    }
}
