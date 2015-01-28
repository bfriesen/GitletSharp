using System.IO;
using System.Text.RegularExpressions;

namespace GitletSharp
{
    internal static class Refs
    {
        /// <summary>
        // Returns true if `HEAD` contains a commit hash, rather than the ref of a branch.
        /// </summary>
        public static bool IsHeadDetached()
        {
            var head = Files.Read(Path.Combine(Files.GitletPath(), "HEAD"));
            return !head.Contains("refs");
        }

        public static string HeadBranchName()
        {
            if (!IsHeadDetached())
            {
                var head = Files.Read(Path.Combine(Files.GitletPath(), "HEAD"));
                return Regex.Match(head, @"refs/heads/(.+)").Groups[1].Value;
            }

            return null;
        }

        public static string TerminalRef(string @ref)
        {
            // If `ref` is "HEAD" and head is pointing at a branch, return the
            // branch.
            if (@ref == "HEAD" && !IsHeadDetached())
            {
                var head = Files.Read(Path.Combine(Files.GitletPath(), "HEAD"));
                return Regex.Match(head, "ref: (refs/heads/.+)").Groups[1].Value;
            }

            // If ref is qualified, return it.
            if (IsRef(@ref))
            {
                return @ref;
            }

            // Otherwise, assume ref is an unqualified local ref (like
            // `master`) and turn it into a qualified ref (like
            // `refs/heads/master`)
            return ToLocalRef(@ref);
        }

        public static string Hash(string refOrHash)
        {
            /*if (objects.exists(refOrHash)) {
                return refOrHash;
            } else {
                var terminalRef = refs.terminalRef(refOrHash);
                if (terminalRef === "FETCH_HEAD") {
                    return refs.fetchHeadBranchToMerge(refs.headBranchName());
                } else if (refs.exists(terminalRef)) {
                    return files.read(files.gitletPath(terminalRef));
                }
            }*/

            if (Objects.Exists(refOrHash))
            {
                return refOrHash;
            }

            var terminalRef = Refs.TerminalRef(refOrHash);
            if (terminalRef == "FETCH_HEAD")
            {
                return Refs.FetchHeadBranchToMerge(Refs.HeadBranchName());
            }

            if (Refs.Exists(terminalRef))
            {
                return Files.Read(Path.Combine(Files.GitletPath(), terminalRef));
            }

            return null;
        }

        private static bool Exists(string @ref)
        {
            return Refs.IsRef(@ref) && File.Exists(Path.Combine(Files.GitletPath(), @ref));
        }

        private static string FetchHeadBranchToMerge(string branchName)
        {
            // TODO: Implement
            throw new System.NotImplementedException();
        }

        public static bool IsRef(string @ref)
        {
            return
                @ref != null
                && (Regex.IsMatch(@ref, "^refs/heads/[A-Za-z-]+$")
                    || Regex.IsMatch(@ref, "^refs/remotes/[A-Za-z-]+/[A-Za-z-]+$")
                    || @ref == "HEAD" || @ref == "FETCH_HEAD" || @ref == "MERGE_HEAD");
        }

        private static string ToLocalRef(string name)
        {
            return "refs/heads/" + name;
        }

        public static string[] CommitParentHashes()
        {
            var headHash = Refs.Hash("HEAD");

            // If the repository is in the middle of a merge, return the
            // hashes of the two commits being merged.
            if (Merge.IsMergeInProgress())
            {
                return new[] { headHash, Refs.Hash("MERGE_HEAD") };
            }

            // If this repository has no commits, return an empty array.
            if (headHash == null)
            {
                return new string[0];
            }

            // Otherwise, return the hash of the commit that `HEAD` is
            // currently pointing at.

            return new[] { headHash };
        }

        public static void Write(string @ref, string content)
        {
            if (Refs.IsRef(@ref))
            {
                Files.Write(Path.Combine(Files.GitletPath(), @ref), content);
            }
        }

        public static void Rm(string @ref)
        {
            if (Refs.IsRef(@ref))
            {
                File.Delete(Path.Combine(Files.GitletPath(), @ref));
            }
        }
    }
}
