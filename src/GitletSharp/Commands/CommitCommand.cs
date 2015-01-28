using System;
using ManyConsole;

namespace GitletSharp
{
    internal class CommitCommand : ConsoleCommand
    {
        public CommitCommand()
        {
            IsCommand("commit", "Commits changes.");

            HasRequiredOption("m|message=", "the commit message", m => Message = m);

            HasOption("cd=", "Sets the current directory.", dir => Files.CurrentPath = dir);
        }

        public string Message { get; private set; }

        public override int Run(string[] remainingArguments)
        {
            var options = new CommitOptions { m = Message };
            var message = Gitlet.Commit(options);
            Console.WriteLine(message);
            return 0;
        }
    }
}
