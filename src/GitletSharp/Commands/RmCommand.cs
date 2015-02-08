using GitletSharp.Core;
using ManyConsole;

namespace GitletSharp
{
    internal class RmCommand : ConsoleCommand
    {
        public RmCommand()
        {
            IsCommand("rm", "Remove a file");

            HasAdditionalArguments(1, " <path>");

            HasOption<bool>("f|force=", "Not supported", f => Force = f);
            HasOption<bool>("r|recursive=", "Remove recursively", r => Recursive = r);

            HasOption("cd=", "Sets the current directory.", dir => Files.CurrentPath = dir);
        }

        public bool Force { get; private set; }
        public bool Recursive { get; private set; }

        public override int Run(string[] remainingArguments)
        {
            var path = remainingArguments[0];
            var options = new RmOptions { f = Force, r = Recursive };
            Gitlet.Rm(path, options);
            return 0;
        }
    }
}
