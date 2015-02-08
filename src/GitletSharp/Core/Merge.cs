using System.Collections.Generic;

namespace GitletSharp
{
    internal static class Merge
    {
        public static bool IsMergeInProgress()
        {
            // TODO: Implement
            return false;
        }

        public static bool IsAForceFetch(string oldHash, string newHash)
        {
            // TODO: Implement
            return false;
        }

        /// <summary>
        /// Fast forwarding means making the
        /// current branch reflect the commit that `giverHash` points at.  No
        /// new commit is created.
        /// </summary>
        public static void WriteFastForwardMerge(string receiverHash, string giverHash)
        {
            // Point head at `giverHash`.
            Refs.Write(Refs.ToLocalRef(Refs.HeadBranchName()), giverHash);

            // Make the index mirror the content of `giverHash`.
            Index.Write(Index.TocToIndex(Objects.CommitToc(giverHash)));

            // If the repo is bare, it has no working copy, so there is no
            // more work to do.  If the repo is not bare...
            if (!Config.Read().Bare)
            {
                // ...Get an object that maps from file paths in the
                // `receiverHash` commit to hashes of the files' content.  If
                // `recevierHash` is undefined, the repository has no commits,
                // yet, and the mapping object is empty.
                var receiverToc =
                    receiverHash == null
                        ? new Dictionary<string, string>()
                        : Objects.CommitToc(receiverHash);

                // ...and write the content of the files to the working copy.
                WorkingCopy.Write(Diff.TocDiff(receiverToc, Objects.CommitToc(giverHash)));
            }
        }
    }
}