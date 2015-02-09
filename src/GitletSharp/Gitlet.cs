using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using GitletSharp.Core;

namespace GitletSharp
{
    public static class Gitlet
    {
        /// <summary>
        /// Initializes the current directory as a new repository.
        /// </summary>
        public static void Init(InitOptions options = null)
        {
            // Abort if already a repository.
            if (Files.InRepo())
            {
                throw new Exception("unsupported");
            }

            options = options ?? new InitOptions();

            // Create basic git directory structure.
            var gitletStructure =
                new Directory(
                    new File("HEAD", "ref: refs/heads/master\n"),
                    new File("config", "[core]\n    bare = " + options.Bare.ToString().ToLower() + "\n"),
                    new Directory("objects"),
                    new Directory("refs",
                        new Directory("heads")));

            // Create the standard git directory structure. If the repository
            // is not bare, / put the directories inside the `.gitlet` directory.
            // If the repository is bare, put them in the top level of the
            // repository.
            Files.WriteFilesFromTree(options.Bare ? gitletStructure : new Directory(".gitlet", gitletStructure), Files.CurrentPath);
        }

        /// <summary>
        /// Adds files that match `path` to the index.
        /// </summary>
        public static void Add(string path)
        {
            Files.AssertInRepo();
            Config.AssertNotBare();

            path = Files.Absolute(path);

            // Get the paths of all the files matching `path`.
            var addedFiles = Files.LsRecursive(path);

            // Abort if no files matched `path`.
            if (addedFiles.Length == 0)
            {
                throw new Exception(Files.PathFromRepoRoot(path) + " did not match any files");
            }

            // Otherwise, use the `UpdateIndex()` Git command to actually add
            // the files.
            foreach (var file in addedFiles)
            {
                UpdateIndex(file, new UpdateIndexOptions { UpdateType = UpdateType.Add });
            }
        }

        /// <summary>
        /// Removes files that match `path` from the index.
        /// </summary>
        public static void Rm(string path, RmOptions options)
        {
            Files.AssertInRepo();
            Config.AssertNotBare();

            options = options ?? new RmOptions();

            path = Files.Absolute(path);

            // Get the paths of all files in the index that match `path`.
            var filesToRm = Index.MatchingFiles(path);

            // Abort if `-f` was passed. The removal of files with changes is
            // not supported.
            if (options.f)
            {
                throw new Exception("unsupported");
            }

            // Abort if no files matched `path`.
            if (filesToRm.Length == 0)
            {
                throw new Exception(Files.PathFromRepoRoot(path) + " did not match any files");
            }

            // Abort if `path` is a directory and `-r` was not passed.
            var dir = new DirectoryInfo(path);

            if (dir.Exists && !options.r)
            {
                throw new Exception("not removing " + path + " recursively without -r");
            }

            // Get a list of all files that are to be removed and have also
            // been changed on disk.  If this list is not empty then abort.
            var changesToRm = Diff.AddedOrModifiedFiles().Intersect(filesToRm).ToArray();

            if (changesToRm.Length > 0)
            {
                throw new Exception("these files have changes:\n" + string.Join("\n", changesToRm) + "\n");
            }

            foreach (var file in filesToRm.Select(Files.WorkingCopyPath).Where(File.Exists))
            {
                File.Delete(file);
            }

            foreach (var file in filesToRm)
            {
                UpdateIndex(file, new UpdateIndexOptions { UpdateType = UpdateType.Rm });
            }
        }

        /// <summary>
        /// Creates a commit object that represents the current
        /// state of the index, writes the commit to the `objects` directory
        /// and points `HEAD` at the commit.
        /// </summary>
        public static string Commit(CommitOptions options)
        {
            Files.AssertInRepo();
            Config.AssertNotBare();

            // Write a tree object that represents the current state of the
            // index.
            var treeHash = WriteTree();

            var headDesc = Refs.IsHeadDetached() ? "detached HEAD" : Refs.HeadBranchName();

            // If the hash of the new tree is the same as the hash of the tree
            // that the `HEAD` commit points at, abort because there is
            // nothing new to commit.
            if (Refs.Hash("HEAD") != null &&
                treeHash == Objects.TreeHash(Objects.Read(Refs.Hash("HEAD"))))
            {
                throw new Exception("# On " + headDesc + "\nnothing to commit, working directory clean");
            }

            // Abort if the repository is in the merge state and there are
            // unresolved merge conflicts.
            string[] conflictedPaths = Index.ConflictedPaths();
            if (Core.Merge.IsMergeInProgress() && conflictedPaths.Length > 0)
            {
                throw new Exception(
                    string.Join("\n", conflictedPaths.Select(p => "U " + p))
                    + "\ncannot commit because you have unmerged files");
            }

            // Otherwise, do the commit.

            // If the repository is in the merge state, use a pre-written
            // merge commit message.  If the repository is not in the
            // merge state, use the message passed with `-m`.
            var m =
                Core.Merge.IsMergeInProgress()
                    ? Files.Read(Path.Combine(Files.GitletPath(), "MERGE_MSG"))
                    : options.m;

            // Write the new commit to the `objects` directory.
            var commitHash = Objects.WriteCommit(treeHash, m, Refs.CommitParentHashes());

            // Point `HEAD` at new commit.
            UpdateRef("HEAD", commitHash);

            // If `MERGE_HEAD` exists, the repository was in the merge
            // state. Remove `MERGE_HEAD` and `MERGE_MSG`to exit the merge
            // state.  Report that the merge is complete.
            if (Core.Merge.IsMergeInProgress())
            {
                File.Delete(Path.Combine(Files.GitletPath(), "MERGE_MSG"));
                Refs.Rm("MERGE_HEAD");
                return "Merge made by the three-way strategy";
            }

            // Repository was not in the merge state, so just report that
            // the commit is complete.
            return "[" + headDesc + " " + commitHash + "] " + m;
        }

        public static void Remote(string command, string name, string path)
        {
            Files.AssertInRepo();

            // Abort if `command` is not "add".  Only "add" is supported.
            if (command != "add")
            {
                throw new Exception("unsupported");
            }

            // Abort if repository already has a record for a remote called
            // `name`.
            if (Config.Read().Remotes.ContainsKey(name))
            {
                throw new Exception("remote " + name + " already exists");
            }

            // Otherwise, add remote record.
            var config = Config.Read();
            config.Remotes.Add(name, new Remote { Url = path });
            Config.Write(config);
        }

        /// <summary>
        /// records the commit that `branch` is at on `remote`.
        // It does not change the local branch.
        /// </summary>
        public static string Fetch(string remote, string branch)
        {
            Files.AssertInRepo();

            // Abort if a `remote` or `branch` not passed.
            if (remote == null || branch == null)
            {
                throw new Exception("unsupported");
            }

            // Abort if `remote` not recorded in config file.
            if (!Config.Read().Remotes.ContainsKey(remote))
            {
                throw new Exception(remote + " does not appear to be a git repository");
            }

            // Get the location of the remote.
            var remoteUrl = Files.Absolute(Config.Read().Remotes[remote].Url);

            // Turn the unqualified branch name into a qualified remote ref
            // eg `[branch] -> refs/remotes/[remote]/[branch]`
            var remoteRef = Refs.ToRemoteRef(remote, branch);

            // Go to the remote repository and get the hash of the commit
            // that `branch` is on.
            var newHash = Util.Remote<string>(remoteUrl)(() =>  Refs.Hash(branch));

            // Abort if `branch` did not exist on the remote.
            if (newHash == null)
            {
                throw new Exception("couldn't find remote ref " + branch);
            }

            // Otherwise, perform the fetch.

            // Note down the hash of the commit this repository currently
            // thinks the remote branch is on.
            var oldHash = Refs.Hash(remoteRef);

            // Get all the objects in the remote `objects` directory and
            // write them.  to the local `objects` directory.  (This is an
            // inefficient way of getting all the objects required to
            // recreate locally the commit the remote branch is on.)
            var remoteObjects = Util.Remote<string[]>(remoteUrl)(Objects.AllObjects);
            foreach (var remoteObject in remoteObjects)
            {
                Objects.Write(remoteObject);
            }

            // Set the contents of the file at
            // `.gitlet/refs/remotes/[remote]/[branch]` to `newHash`, the
            // hash of the commit that the remote branch is on.
            Gitlet.UpdateRef(remoteRef, newHash);

            // Record the hash of the commit that the remote branch is on
            // in `FETCH_HEAD`.  (The user can call `gitlet merge
            // FETCH_HEAD` to merge the remote version of the branch into
            // their local branch.  For more details, see
            // [gitlet.merge()](#section-93).)
            Refs.Write("FETCH_HEAD", newHash + " branch " + branch + " of " + remoteUrl);

            // Report the result of the fetch.
            return
                string.Format(
                    "From {0}\nCount {1}\n{2} -> {3}/{2}{4}\n",
                    remoteUrl,
                    remoteObjects.Length,
                    branch,
                    remote,
                    Core.Merge.IsAForceFetch(oldHash, newHash) ? " (forced)" : "");
        }

        public static string Merge(string @ref)
        {
            Files.AssertInRepo();
            Config.AssertNotBare();

            // Get the `receiverHash`, the hash of the commit that the
            // current branch is on.
            var receiverHash = Refs.Hash("HEAD");

            // Get the `giverHash`, the hash for the commit to merge into the
            // receiver commit.
            var giverHash = Refs.Hash(@ref);

            // Abort if head is detached.  Merging into a detached head is not
            // supported.
            if (Refs.IsHeadDetached())
            {
                throw new Exception("unsupported");
            }

            // Abort if `ref` did not resolve to a hash, or if that hash is
            // not for a commit object.
            if (giverHash == null || Objects.Type(Objects.Read(giverHash)) != "commit")
            {
                throw new Exception(@ref +": expected commit type");
            }

            // Do not merge if the current branch - the receiver - already has
            // the giver's changes.  This is the case if the receiver and
            // giver are the same commit, or if the giver is an ancestor of
            // the receiver.
            if (Objects.IsUpToDate(receiverHash, giverHash))
            {
                return "Already up-to-date";
            }

            // Get a list of files changed in the working copy.  Get a list
            // of the files that are different in the receiver and giver. If
            // any files appear in both lists then abort.
            var paths = Diff.ChangedFilesCommitWouldOverwrite(giverHash);
            if (paths.Length > 0)
            {
                throw new Exception("local changes would be lost\n" + string.Join("\n", paths) + "\n");
            }

            // If the receiver is an ancestor of the giver, a fast forward
            // is performed.  This is possible because there is already a
            // commit that incorporates all of the giver's changes into the
            // receiver.
            if (Core.Merge.CanFastForward(receiverHash, giverHash))
            {
                // Fast forwarding means making the current branch reflect the
                // commit that `giverHash` points at.  The branch is pointed
                // at `giverHash`.  The index is set to match the contents of
                // the commit that `giverHash` points at.  The working copy is
                // set to match the contents of that commit.
                Core.Merge.WriteFastForwardMerge(receiverHash, giverHash);
                return "Fast-forward";
            }

            // If the receiver is not an ancestor of the giver, a merge
            // commit must be created.

            // The repository is put into the merge state.  The
            // `MERGE_HEAD` file is written and its contents set to
            // `giverHash`.  The `MERGE_MSG` file is written and its
            // contents set to a boilerplate merge commit message.  A
            // merge diff is created that will turn the contents of
            // receiver into the contents of giver.  This contains the
            // path of every file that is different and whether it was
            // added, removed or modified, or is in conflict.  Added files
            // are added to the index and working copy.  Removed files are
            // removed from the index and working copy.  Modified files
            // are modified in the index and working copy.  Files that are
            // in conflict are written to the working copy to include the
            // receiver and giver versions.  Both the receiver and giver
            // versions are written to the index.
            Core.Merge.WriteNonFastForwardMerge(receiverHash, giverHash, @ref);

            // If there are any conflicted files, a message is shown to
            // say that the user must sort them out before the merge can
            // be completed.
            if (Core.Merge.HasConflicts(receiverHash, giverHash))
            {
                return "Automatic merge failed. Fix conflicts and commit the result.";
            }

            return Gitlet.Commit(new CommitOptions());
        }

        /// <summary>
        /// Fetches the commit that `branch` is on at `remote`.
        /// It merges that commit into the current branch.
        /// </summary>
        public static string Pull(string remote, string branch)
        {
            Files.AssertInRepo();
            Config.AssertNotBare();
            Gitlet.Fetch(remote, branch);
            return Gitlet.Merge("FETCH_HEAD");
        }

        public static string Clone(string remotePath, string targetPath, CloneOptions options = null)
        {
            options = options ?? new CloneOptions();

            // Abort if a `remotePath` or `targetPath` not passed.
            if (remotePath == null) { throw new ArgumentNullException("remotePath"); }
            if (targetPath == null) { throw new ArgumentNullException("targetPath"); }

            // Abort if `remotePath` does not exist, or is not a Gitlet
            // repository. !util.remote(remotePath)(files.inRepo))
            if (!Directory.Exists(remotePath) || !Util.Remote<bool>(remotePath)(Files.InRepo))
            {
                throw new Exception("repository " + remotePath + " does not exist");
            }

            // Abort if `targetPath` exists and is not empty.
            targetPath = Files.Absolute(targetPath);
            var targetDir = new DirectoryInfo(targetPath);
            if (targetDir.Exists && targetDir.GetFileSystemInfos().Length > 0)
            {
                throw new Exception(targetPath + " already exists and is not empty");
            }

            // Otherwise, do the clone.
            remotePath = Files.Absolute(remotePath);

            // If `targetPath` doesn't exist, create it.
            if (!targetDir.Exists)
            {
                targetDir.Create();
            }

            Util.Remote<object>(targetPath)(() =>
            {
                // Initialize the directory as a Gitlet repository.
                Gitlet.Init(options);

                // Set up `remotePath` as a remote called "origin".
                Gitlet.Remote("add", "origin", Files.Relative(Files.CurrentPath, remotePath));

                // Get the hash of the commit that master is pointing at on
                // the remote repository.
                var remoteHeadHash = Util.Remote<string>(remotePath)(() => Refs.Hash("master"));

                // If the remote repo has any commits, that hash will exist.
                // The new repository records the commit that the passed
                // `branch` is at on the remote.  It then sets master on the
                // new repository to point at that commit.
                if (remoteHeadHash != null)
                {
                    Gitlet.Fetch("origin", "master");
                    Core.Merge.WriteFastForwardMerge(null, remoteHeadHash);
                }

                return null;
            });

            // Report the result of the clone.
            return "Cloning into " + targetPath;
        }

        public static string Log(LogOptions options)
        {
            var sb = new StringBuilder();

            var commitHash = Refs.Hash("HEAD");

            while (commitHash != null)
            {
                var commit = Objects.Read(commitHash);

                sb.AppendLine(commit);

                var match = Regex.Match(commit, @"^parent (?<commitHash>[0-9a-fA-F]{40})$", RegexOptions.Multiline);
                commitHash = match.Success ? match.Groups["commitHash"].Value : null;
            }

            return sb.ToString();
        }

        private static void UpdateRef(string refToUpdate, string refToUpdateTo)
        {
            Files.AssertInRepo();

            // Get the hash that `refToUpdateTo` points at.
            var hash = Refs.Hash(refToUpdateTo);

            // Abort if `refToUpdateTo` does not point at a hash.
            if (!Objects.Exists(hash))
            {
                throw new Exception(refToUpdateTo + " not a valid SHA1");
            }

            // Abort if `refToUpdate` does not match the syntax of a ref.
            if (!Refs.IsRef(refToUpdate))
            {
                throw new Exception("cannot lock the ref " + refToUpdate);
            }

            // Abort if `hash` points to an object in the `objects` directory
            // that is not a commit.
            if (Objects.Type(Objects.Read(hash)) != "commit")
            {
                var branch = Refs.TerminalRef(refToUpdate);
                throw new Exception(branch + " cannot refer to non-commit object " + hash + "\n");
            }

            // Otherwise, set the contents of the file that the ref represents
            // to `hash`.
            Refs.Write(Refs.TerminalRef(refToUpdate), hash);
        }

        private static void UpdateIndex(string file, UpdateIndexOptions options)
        {
            Files.AssertInRepo();
            Config.AssertNotBare();

            options = options ?? new UpdateIndexOptions();

            var fileInfo = new FileInfo(file);

            var pathFromRoot = Files.PathFromRepoRoot(file);
            var isOnDisk = fileInfo.Exists;
            var isInIndex = Index.HasFile(file, 0);

            // Abort if `file` is a directory.  `UpdateIndex()` only handles
            // single files.
            if (isOnDisk && (int)fileInfo.Attributes != -1 && (fileInfo.Attributes & FileAttributes.Directory) == FileAttributes.Directory)
            {
                throw new Exception(pathFromRoot + " is a directory - add files inside");
            }

            if (options.UpdateType == UpdateType.Rm && !isOnDisk && isInIndex)
            {
                // Abort if file is being removed and is in conflict.  Gitlet
                // doesn't support this.
                if (Index.IsFileInConflict(file))
                {
                    throw new Exception("unsupported");
                }

                Index.WriteRm(file);
                return;
            }

            // If file is being removed, is not on disk and not in the index,
            // there is no work to do.
            if (options.UpdateType == UpdateType.Rm && !isOnDisk && !isInIndex)
            {
                return;
            }

            // Abort if the file is on disk and not in the index and the
            // `--add` was not passed.
            if (options.UpdateType == UpdateType.NotSet && isOnDisk && !isInIndex)
            {
                throw new Exception("cannot add " + pathFromRoot + " to index - use --add option");
            }

            // If file is on disk and either `-add` was passed or the file is
            // in the index, add the file's current content to the index.
            if (isOnDisk && (options.UpdateType == UpdateType.Add || isInIndex))
            {
                Index.WriteAdd(file);
                return;
            }

            if (options.UpdateType != UpdateType.Rm && !isOnDisk)
            {
                throw new Exception(pathFromRoot + " does not exist and --remove not passed");
            }
        }

        private static string WriteTree()
        {
            Files.AssertInRepo();
            return Objects.WriteTree(Files.NestFlatTree(Index.Toc()));
        }
    }
}
