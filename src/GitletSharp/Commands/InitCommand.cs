using ManyConsole;

namespace GitletSharp
{
    public class InitCommand : ConsoleCommand
    {
        public InitCommand()
        {
            IsCommand("init", "Initializes directory as a new repository.");

            HasOption<bool>("bare=", "Whether the repository should be bare", bare => Bare = bare);
            HasOption("cd=", "Sets the current directory.", dir => Files.CurrentPath = dir);
        }

        public bool Bare { get; private set; }

        public override int Run(string[] remainingArguments)
        {
            Gitlet.Init(new InitOptions { Bare = Bare });
            return 0;
        }
    }
}
