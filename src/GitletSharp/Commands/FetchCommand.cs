using System;
using ManyConsole;

namespace GitletSharp
{
    internal class FetchCommand : ConsoleCommand
    {
        public FetchCommand()
        {
            IsCommand("fetch", "Download objects and refs from another repository");

            HasAdditionalArguments(1, " <repository>");

            HasOption("cd=", "Sets the current directory.", dir => Files.CurrentPath = dir);
        }

        public override int Run(string[] remainingArguments)
        {
            var remote = remainingArguments[0];

            var message = Gitlet.Fetch(remote, null);
            Console.WriteLine(message);
            return 0;
        }
    }
}