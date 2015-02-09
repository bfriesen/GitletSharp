using System.Collections.Generic;
using System.Linq;

namespace GitletSharp.Core
{
    public static class Status
    {
        public static string Get()
        {
            var lines =
                new[] { "On branch " + Refs.HeadBranchName() }
                    .Concat(Listing("Untracked files: ", Untracked()))
                    .Concat(Listing("Unmerged paths: ", Index.ConflictedPaths()))
                    .Concat(Listing("Changes to be committed: ", ToBeCommitted()))
                    .Concat(Listing("Changes not staged for commit: ", NotStagedForCommit()));

            return string.Join("\n", lines);
        }

        private static string[] Untracked()
        {
            var indexToc = Index.Toc();
            var workingCopyPath = Files.WorkingCopyPath();

            return
                Directory.GetFiles(workingCopyPath)
                    .Select(f => Files.Relative(workingCopyPath, f))
                    .Where(path => !indexToc.ContainsKey(path))
                    .ToArray();
        }

        private static string[] ToBeCommitted()
        {
            var headHash = Refs.Hash("HEAD");
            var headToc = headHash == null ? new Dictionary<string, string>() : Objects.CommitToc(headHash);
            var ns = Diff.NameStatus(Diff.TocDiff(headToc, Index.Toc()));
            return ns.Select(item => item.Value.Value + " " + item.Key).ToArray();
        }

        private static string[] NotStagedForCommit()
        {
            var ns = Diff.NameStatus(Diff.GetDiff());
            return ns.Select(item => item.Value.Value + " " + item.Key).ToArray();
        }

        private static IEnumerable<string> Listing(string heading, string[] lines)
        {
            if (lines.Length > 0)
            {
                yield return heading;

                foreach (var line in lines)
                {
                    yield return line;
                }
            }
        }
    }
}