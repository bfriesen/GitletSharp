using System;
using GitletSharp.Core;
using ManyConsole;

namespace GitletSharp
{
    internal class PullCommand : ConsoleCommand
    {
        public PullCommand()
        {
            IsCommand("pull", "Fetch from and integrate with another repository or a local branch");

            HasAdditionalArguments(2, " <repository> <branch>");

            HasOption("cd=", "Sets the current directory.", dir => Files.CurrentPath = dir);
        }

        public override int Run(string[] remainingArguments)
        {
            var repository = remainingArguments[0];
            var branch = remainingArguments[1];
            var message = Gitlet.Pull(repository, branch);
            Console.WriteLine(message);
            return 0;
        }
    }
}