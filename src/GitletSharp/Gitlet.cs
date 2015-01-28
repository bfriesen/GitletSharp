using System;
using System.IO;
using System.Linq;

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
        public static void Rm(string path, RemoveOptions options)
        {
            Files.AssertInRepo();
            Config.AssertNotBare();

            options = options ?? new RemoveOptions();

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
        public static void Commit(CommitOptions options)
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

            // TODO: Finish implementing.
        }

        public static void UpdateIndex(string file, UpdateIndexOptions options)
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
            if (isOnDisk && (fileInfo.Attributes & FileAttributes.Directory) == FileAttributes.Directory)
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

        public static string WriteTree()
        {
            Files.AssertInRepo();
            return Objects.WriteTree(Files.NestFlatTree(Index.Toc()));
        }
    }
}
