using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

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
            if (Files.InRepo()) { return; }

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

            foreach (var file in filesToRm.Select(Files.WorkingCopyPath).Where(file => File.Exists(file)))
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
            if (Merge.IsMergeInProgress() && conflictedPaths.Length > 0)
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
                Merge.IsMergeInProgress()
                    ? Files.Read(Path.Combine(Files.GitletPath(), "MERGE_MSG"))
                    : options.m;

            // Write the new commit to the `objects` directory.
            var commitHash = Objects.WriteCommit(treeHash, m, Refs.CommitParentHashes());

            // Point `HEAD` at new commit.
            UpdateRef("HEAD", commitHash);

            // If `MERGE_HEAD` exists, the repository was in the merge
            // state. Remove `MERGE_HEAD` and `MERGE_MSG`to exit the merge
            // state.  Report that the merge is complete.
            if (Merge.IsMergeInProgress())
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
