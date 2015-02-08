using System;
using GitletSharp.Core;
using ManyConsole;

namespace GitletSharp
{
    internal class LogCommand : ConsoleCommand
    {
        public LogCommand()
        {
            IsCommand("log", "Show commit logs");

            HasOption("cd=", "Sets the current directory.", dir => Files.CurrentPath = dir);
        }

        public override int Run(string[] remainingArguments)
        {
            var options = new LogOptions();
            string message = Gitlet.Log(options);
            Console.WriteLine(message);
            return 0;
        }
    }
}
