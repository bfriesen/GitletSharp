using ManyConsole;

namespace GitletSharp
{
    internal class RemoteCommand : ConsoleCommand
    {
        public RemoteCommand()
        {
            IsCommand("remote", "Manage set of tracked repositories");

            HasAdditionalArguments(3, " add <name> <url>");

            HasOption("cd=", "Sets the current directory.", dir => Files.CurrentPath = dir);
        }

        public override int Run(string[] remainingArguments)
        {
            var command = remainingArguments[0];
            var name = remainingArguments[1];
            var url = remainingArguments[2];
            Gitlet.Remote(command, name, url);
            return 0;
        }
    }
}
