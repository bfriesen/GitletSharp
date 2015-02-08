using System;
using GitletSharp.Core;
using ManyConsole;

namespace GitletSharp
{
    internal class CloneCommand : ConsoleCommand
    {
        public CloneCommand()
        {
            IsCommand("clone", "Clone a repository into a new directory");

            HasAdditionalArguments(2, " <repository> [<directory>]");

            HasOption<bool>("bare=", "Whether the repository should be bare", bare => Bare = bare);
            HasOption("cd=", "Sets the current directory.", dir => Files.CurrentPath = dir);
        }

        public bool Bare { get; private set; }

        public override int Run(string[] remainingArguments)
        {
            var remotePath = remainingArguments[0];
            var targetPath = remainingArguments[1];

            var message = Gitlet.Clone(remotePath, targetPath, new CloneOptions { Bare = Bare });
            Console.WriteLine(message);
            return 0;
        }
    }
}
