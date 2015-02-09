using System;
using GitletSharp.Core;
using ManyConsole;

namespace GitletSharp
{
    public class StatusCommand : ConsoleCommand
    {
        public StatusCommand()
        {
            IsCommand("status", "Show the working tree status");

            HasOption("cd=", "Sets the current directory.", dir => Files.CurrentPath = dir);
        }

        public override int Run(string[] remainingArguments)
        {
            var message = Gitlet.Status();
            Console.WriteLine(message);
            return 0;
        }
    }
}