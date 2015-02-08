using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GitletSharp.Core
{
    internal static class Merge
    {
        public static string CommonAncestor(string aHash, string bHash)
        {
            var sorted = new[] { aHash, bHash }.ToList();
            sorted.Sort();
            aHash = sorted[0];
            bHash = sorted[1];
            var aAncestors = new[] { aHash }.Concat(Objects.Ancestors(aHash));
            var bAncestors = new[] { bHash }.Concat(Objects.Ancestors(bHash));
            return aAncestors.Intersect(bAncestors).First();
        }

        /// <summary>
        /// returns true if the repository is in the middle of a merge.
        /// </summary>
        /// <returns></returns>
        public static bool IsMergeInProgress()
        {
            return Refs.Hash("MERGE_HEAD") != null;
        }

        /// <summary>
        /// A fast forward is possible if the changes
        /// made to get to the `giverHash` commit already incorporate the
        /// changes made to get to the `receiverHash` commit.  So,
        /// `canFastForward()` returns true if the `receiverHash` commit is
        /// an ancestor of the `giverHash` commit.  It also returns true if
        /// there is no `receiverHash` commit because this indicates the
        /// repository has no commits, yet.
        /// </summary>
        public static bool CanFastForward(string receiverHash, string giverHash)
        {
            return receiverHash == null || Objects.IsAncestor(giverHash, receiverHash);
        }

        /// <summary>
        /// returns true if hash for local commit
        /// (`receiverHash`) is not ancestor of hash for fetched commit
        /// (`giverHash`).
        /// </summary>
        public static bool IsAForceFetch(string receiverHash, string giverHash)
        {
            return receiverHash != null && !Objects.IsAncestor(giverHash, receiverHash);
        }

        /// <summary>
        /// returns true if merging the commit for
        /// `giverHash` into the commit for `receiverHash` would produce
        /// conflicts.
        /// </summary>
        public static bool HasConflicts(string receiverHash, string giverHash)
        {
            var mergeDiff = Merge.MergeDiff(receiverHash, giverHash);
            return mergeDiff.Any(item => item.Value.Status == Diff.FileStatus.CONFLICT);
        }

        /// <summary>
        /// returns a diff that represents the changes to get
        /// from the `receiverHash` commit to the `giverHash` commit.
        /// Because this is a merge diff, the function uses the common
        /// ancestor of the `receiverHash` commit and `giverHash` commit to
        /// avoid trivial conflicts.
        /// </summary>
        private static Dictionary<string, Diff> MergeDiff(string receiverHash, string giverHash)
        {
            return
                Diff.TocDiff(
                    Objects.CommitToc(receiverHash),
                    Objects.CommitToc(giverHash),
                    Objects.CommitToc(Merge.CommonAncestor(receiverHash, giverHash)));
        }

        /// <summary>
        /// creates a message for the merge commit that
        /// will potentially be created when the `giverHash` commit is merged
        /// into the `receiverHash` commit.  It writes this message to
        /// `.gitlet/MERGE_MSG`.
        /// </summary>
        private static void WriteMergeMsg(string receiverHash, string giverHash, string @ref)
        {
            var msg = "Merge " + @ref + " into " + Refs.HeadBranchName();

            var mergeDiff = Merge.MergeDiff(receiverHash, giverHash);
            var conflicts = mergeDiff.Where(item => item.Value.Status == Diff.FileStatus.CONFLICT).ToList();

            if (conflicts.Any())
            {
                msg += "\nConflicts:\n" + string.Join("\n", conflicts);
            }

            Files.Write(Path.Combine(Files.GitletPath(), "MERGE_MSG"), msg);
        }

        /// <summary>
        /// merges the `giverHash` commit into the `receiverHash` commit and
        /// writes the merged content to the index.
        /// </summary>
        private static void WriteIndex(string receiverHash, string giverHash)
        {
            var mergeDiff = Merge.MergeDiff(receiverHash, giverHash);
            
            Index.Write(new Dictionary<Index.Key, string>());

            foreach (var item in mergeDiff)
            {
                if (item.Value.Status == Diff.FileStatus.CONFLICT)
                {
                    if (item.Value.Base != null) // (null if same filepath ADDED w dif content)
                    {
                      Index.WriteEntry(item.Key, 1, Objects.Read(item.Value.Base));
                    }

                    Index.WriteEntry(item.Key, 2, Objects.Read(item.Value.Receiver));
                    Index.WriteEntry(item.Key, 3, Objects.Read(item.Value.Giver));
                }
                else if (item.Value.Status == Diff.FileStatus.MODIFY)
                {
                    Index.WriteEntry(item.Key, 0, item.Value.Giver);
                }
                else if (item.Value.Status == Diff.FileStatus.ADD
                         || item.Value.Status == Diff.FileStatus.SAME)
                {
                    var content = Objects.Read(item.Value.Receiver ?? item.Value.Giver);
                    Index.WriteEntry(item.Key, 0, content);
                }
            }
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

        public static void WriteNonFastForwardMerge(string receiverHash, string giverHash, string giverRef)
        {
            // Write `giverHash` to `.gitlet/MERGE_HEAD`.  This file acts as a
            // record of `giverHash` and as the signal that the repository is
            // in the merging state.
            Refs.Write("MERGE_HEAD", giverHash);

            // Write a standard merge commit message that will be used when
            // the merge commit is created.
            Merge.WriteMergeMsg(receiverHash, giverHash, giverRef);

            // Merge the `receiverHash` commit with the `giverHash` commit and
            // write the content to the index.
            Merge.WriteIndex(receiverHash, giverHash);

            // If the repo is bare, it has no working copy, so there is no
            // more work to do.  If the repo is not bare...
            if (!Config.Read().Bare)
            {
                // ...merge the `receiverHash` commit with the `giverHash`
                // commit and write the content to the working copy.
                WorkingCopy.Write(Merge.MergeDiff(receiverHash, giverHash));
            }
        }
    }
}