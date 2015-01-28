using ManyConsole;

namespace GitletSharp
{
    public class CommitCommand : ConsoleCommand
    {
        public CommitCommand()
        {
            IsCommand("commit", "Commits changes.");

            HasOption("cd=", "Sets the current directory.", dir => Files.CurrentPath = dir);
        }

        public override int Run(string[] remainingArguments)
        {
            var options = new CommitOptions();
            Gitlet.Commit(options);
            return 0;
        }
    }
}
