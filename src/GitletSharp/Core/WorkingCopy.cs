using System;
using System.Collections.Generic;

namespace GitletSharp
{
    internal static class WorkingCopy
    {
        /// <summary>
        /// Takes a diff object (see the diff module for a
        /// description of the format) and applies the changes in it to the
        /// working copy.
        /// </summary>
        public static void Write(Dictionary<string, Diff> dif)
        {
            // `composeConflict()` takes the hashes of two versions of the
            // same file and returns a string that represents the two versions
            // as a conflicted file:
            // <pre><<<<<
            // version1
            // `======
            // version2
            // `>>>>></pre>
            // Note that Gitlet, unlike real Git, does not do a line by line
            // diff and mark only the conflicted parts of the file.  If a file
            // is in conflict, the whole body of the file is marked as one big
            // conflict.
            Func<string, string, string> composeConflict =
                (receiverFileHash, giverFileHash) =>
                    "<<<<<<\n" + Objects.Read(receiverFileHash) +
                    "\n======\n" + Objects.Read(giverFileHash) +
                    "\n>>>>>>\n";

            // Go through all the files that have changed, updating the
            // working copy for each.
            foreach (var item in dif)
            {
                if (item.Value.Status == Diff.FileStatus.ADD)
                {
                    Files.Write(Files.WorkingCopyPath(item.Key), Objects.Read(item.Value.Receiver ?? item.Value.Giver));
                }
                else if (item.Value.Status == Diff.FileStatus.CONFLICT)
                {
                    Files.Write(Files.WorkingCopyPath(item.Key), composeConflict(item.Value.Receiver, item.Value.Giver));
                }
                else if (item.Value.Status == Diff.FileStatus.MODIFY)
                {
                    Files.Write(Files.WorkingCopyPath(item.Key), Objects.Read(item.Value.Giver));
                }
                else if (item.Value.Status == Diff.FileStatus.DELETE)
                {
                    File.Delete(Files.WorkingCopyPath(item.Key));
                }
            }
        }
    }
}
