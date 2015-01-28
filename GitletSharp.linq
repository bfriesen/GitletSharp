<Query Kind="Program">
  <Namespace>System.Security.Cryptography</Namespace>
</Query>

void Main()
{
    Gitlet.Init(new InitOptions() { Bare = false });
    Gitlet.Add(_devPath + "number.txt");
    Gitlet.Commit(new CommitOptions());
}

private const string _devPath = @"C:\Temp\alpha\";

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
    
    /// <sumary>
    /// Adds files that match `path` to the index.
    /// </summary>
    public static void Add(string path)
    {
        Files.AssertInRepo();
        Config.AssertNotBare();
    
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

public class InitOptions
{
    public bool Bare { get; set; }
}

public class UpdateIndexOptions
{
    public UpdateType UpdateType { get; set; }
}

public enum UpdateType
{
    NotSet, Add, Rm
}

public class RemoveOptions
{
    public bool f { get; set; }
    public bool r { get; set; }
}

public class CommitOptions
{
    
}

#region Refs

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
}

#endregion

#region Objects

internal static class Objects
{
    public static string WriteTree(Directory dir)
    {
        var treeObject =
            string.Join(
                "\n",
                dir.Contents.Select(
                    item =>
                    {
                        var file = item as File;
                        if (file != null)
                        {
                            return "blob " + file.Contents + " " + file.Name;
                        }
                        
                        return "tree " + WriteTree((Directory)item) + " " + item.Name;
                    })) + "\n";
        
        return Write(treeObject.Dump("Objects.WriteTree:treeObject")).Dump("Objects.WriteTree:return value");
    }

    public static string Write(string content)
    {
        var hash = Util.Hash(content);
        Files.Write(Path.Combine(Files.GitletPath(), "objects", hash), content);
        return hash;
    }
}

#endregion

#region Index

internal static class Index
{
    public static bool HasFile(string path, int stage)
    {
        return Read().ContainsKey(new Key(path, stage));
    }

    public static Dictionary<Key, string> Read()
    {
        var indexFilePath = Path.Combine(Files.GitletPath(), "index");
        
        return
            (File.Exists(indexFilePath) ? File.ReadAllLines(indexFilePath) : new string[0])
            .ToDictionary(
                line => new Key(line.Split(' ')[0], int.Parse(line.Split(' ')[1])),
                line => line.Split(' ')[2]);
    }
    
    public static Dictionary<string, string> Toc()
    {
        var index = Read();
        return index.ToDictionary(item => item.Key.Path, item => item.Value);
    }
    
    public static bool IsFileInConflict(string path)
    {
        return HasFile(path, 2);
    }
    
    public static void WriteAdd(string path)
    {
        if (IsFileInConflict(path))
        {
            RmEntry(path, 1);
            RmEntry(path, 2);
            RmEntry(path, 3);
        }
        
        WriteEntry(path, 0, Files.Read(Files.WorkingCopyPath(path)));
    }
    
    public static void WriteRm(string path)
    {
        RmEntry(path, 0);
    }
    
    private static void WriteEntry(string path, int stage, string content)
    {
        var index = Read();
        index[new Key(path, stage)] = Objects.Write(content);
        Write(index);
    }
    
    private static void RmEntry(string path, int stage)
    {
        var index = Read();
        index.Remove(new Key(path, stage));
        Write(index);
    }
    
    private static void Write(Dictionary<Key, string> index)
    {
        var indexStr =
            string.Join(
                "\n",
                index.Select(item => item.Key.Path + " " + item.Key.Stage + " " + item.Value))
                + "\n";
        Files.Write(Path.Combine(Files.GitletPath(), "index"), indexStr);
    }
    
    public static string[] MatchingFiles(string pathSpec)
    {
        var searchPath = Files.PathFromRepoRoot(pathSpec);
        return Read().Keys.Select(Key => Key.Path).Where(path => Regex.IsMatch(path, "^" + pathSpec)).ToArray();
    }
    
    public struct Key
    {
        public readonly string Path;
        public readonly int Stage;
        
        public Key(string path, int stage)
        {
            Path = path;
            Stage = stage;
        }
        
        public override bool Equals(object obj)
        {
            if (obj is Key)
            {
                var other = (Key)obj;
                return Equals(Path, other.Path) && Stage == other.Stage;
            }
            
            return false;
        }
        
        public override int GetHashCode()
        {
            return Tuple.Create(Path, Stage).GetHashCode();
        }
    }
}

#endregion

#region Diff

public static class Diff
{
    public static string[] AddedOrModifiedFiles()
    {
        /*var headToc = refs.hash("HEAD") ? objects.commitToc(refs.hash("HEAD")) : {};
        var wc = diff.nameStatus(diff.tocDiff(headToc, index.workingCopyToc()));
        return Object.keys(wc).filter(function(p) { return wc[p] !== diff.FILE_STATUS.DELETE; });*/
        
        // TODO: Implement
        return new string[0];
    }
}

#endregion

#region Config

internal class Config
{
    private bool? _fileMode;
    private bool _bare;
    private bool? _logAllRefUpdates;
    
    public string RepositoryFormatVersion { get; set; }
    public bool? FileMode { get { return _fileMode; } set { _fileMode = value; } }
    public bool Bare { get { return _bare; } set { _bare = value; } }
    public bool? LogAllRefUpdates { get { return _logAllRefUpdates; } set { _logAllRefUpdates = value; } }
    
    public Dictionary<string, Remote> Remotes { get; set; }
    public Dictionary<string, Branch> Branches { get; set; }
    
    public Config()
    {
        Remotes = new Dictionary<string, Remote>();
        Branches = new Dictionary<string, Branch>();
    }
    
    public static void AssertNotBare()
    {
        var config = Read();
        
        if (config.Bare)
        {
            throw new Exception("this operation must be run in a work tree");
        }
    }
    
    public static Config Read()
    {
        return Parse(Files.Read(Path.Combine(Files.GitletPath(), "config")));
    }
    
    private string ToFileFormat()
    {
        var sb = new StringBuilder();
        
        sb.Append("[core]");
        if (RepositoryFormatVersion != null) { sb.Append("\n    repositoryformatversion = ").Append(RepositoryFormatVersion); }
        if (FileMode != null) { sb.Append("\n    filemode = ").Append(FileMode.ToString().ToLower()); }
        sb.Append("\n    bare = ").Append(Bare.ToString().ToLower());
        if (LogAllRefUpdates != null) { sb.Append("\n    logallrefupdates = ").Append(LogAllRefUpdates.ToString().ToLower()); }
        
        foreach (var item in Remotes)
        {
            sb.Append("\n[remote \"").Append(item.Key).Append("\"]");
            if (item.Value.Url != null) { sb.Append("\n    url = ").Append(item.Value.Url); }
            if (item.Value.Fetch != null) { sb.Append("\n    fetch = ").Append(item.Value.Fetch); }
        }
        
        foreach (var item in Branches)
        {
            sb.Append("\n[branch \"").Append(item.Key).Append("\"]");
            if (item.Value.Remote != null) { sb.Append("\n    remote = ").Append(item.Value.Remote); }
            if (item.Value.Merge != null) { sb.Append("\n    merge = ").Append(item.Value.Merge); }
        }
        
        sb.Append("\n");
        
        return sb.ToString();
    }
    
    public static Config Parse(string configText)
    {
        var sectionsRegex = new Regex(@"^[ \t]*\[(?<name>[^ \]]+)(?:\]|[ \t]+""(?<label>[^""]*)""\][ \t]*)", RegexOptions.Multiline);
        var settingsRegex = new Regex(@"^[ \t]*(?:(?<name>[^ \t=]+)[ \t]*=[ \t]*(?<value>[^\r\n]*)|(?<section>\[))", RegexOptions.Multiline);
        
        var sections =
            sectionsRegex.Matches(configText).Cast<Match>()
                .Select(sectionMatch =>
                    new Section(
                        sectionMatch.Groups["name"].Value,
                        sectionMatch.Groups["label"].Success ? sectionMatch.Groups["label"].Value : null,
                        settingsRegex.Matches(configText.Substring(sectionMatch.Index + sectionMatch.Length)).Cast<Match>()
                            .TakeWhile(settingMatch => !settingMatch.Groups["section"].Success)
                            .Select(settingMatch =>
                                new Setting(
                                    settingMatch.Groups["name"].Value,
                                    settingMatch.Groups["value"].Value))));
        
        var config = new Config();
    
        foreach (var section in sections)
        {
            switch (section.Name)
            {
                case "core":
                    ProcessCoreSettings(config, section);
                    break;
                case "remote":
                    ProcessRemoteSettings(config, section);
                    break;
                case "branch":
                    ProcessBranchSettings(config, section);
                    break;
            }
        }
        
        return config;
    }
    
    private static void ProcessCoreSettings(Config config, Section section)
    {
        foreach (var setting in section.Settings)
        {
            switch (setting.Name)
            {
                case "repositoryformatversion":
                    config.RepositoryFormatVersion = setting.Value;
                    break;
                case "filemode":
                    ParseBool(setting.Value, ref config._fileMode);
                    break;
                case "bare":
                    ParseBool(setting.Value, ref config._bare);
                    break;
                case "logallrefupdates":
                    ParseBool(setting.Value, ref config._logAllRefUpdates);
                    break;
            }
        }
    }
    
    private static void ProcessRemoteSettings(Config config, Section section)
    {
        var remote = new Remote();
                    
        foreach (var setting in section.Settings)
        {
            switch (setting.Name)
            {
                case "url":
                    remote.Url = setting.Value;
                    break;
                case "fetch":
                    remote.Fetch = setting.Value;
                    break;
            }
        }
        
        config.Remotes.Add(section.Label, remote);
    }
    
    private static void ProcessBranchSettings(Config config, Section section)
    {
        var branch = new Branch();
                    
        foreach (var setting in section.Settings)
        {
            switch (setting.Name)
            {
                case "remote":
                    branch.Remote = setting.Value;
                    break;
                case "merge":
                    branch.Merge = setting.Value;
                    break;
            }
        }
        
        config.Branches.Add(section.Label, branch);
    }

    private static void ParseBool(string stringValue, ref bool value)
    {
        switch (stringValue)
        {
            case "true":
                value = true;
                break;
            case "false":
                value = false;
                break;
        }
    }

    private static void ParseBool(string stringValue, ref bool? value)
    {
        switch (stringValue)
        {
            case "true":
                value = true;
                break;
            case "false":
                value = false;
                break;
        }
    }

    private class Section
    {
        public Section(string name, string label, IEnumerable<Setting> settings)
        {
            Name = name;
            Label = label;
            Settings = settings;
        }
        
        public string Name { get; private set; }
        public string Label { get; private set; }
        public IEnumerable<Setting> Settings { get; private set; }
    }
    
    private class Setting
    {
        public Setting(string name, string value)
        {
            Name = name; 
            Value = value;
        }
    
        public string Name { get; private set; }
        public string Value { get; private set; }
    }
}

internal class Remote
{
    public string Url { get; set; }
    public string Fetch { get; set; }
}

internal class Branch
{
    public string Remote { get; set; }
    public string Merge { get; set; }
    
    public Remote GetRemote(Config config)
    {
        Remote remote;
    
        if (config.Remotes.TryGetValue(Remote, out remote))
        {
            return remote;
        }
        
        return null;
    }
}

#endregion

#region Util

internal static class Util
{
    private static readonly MD5 md5 = MD5.Create();

    public static string Hash(string content)
    {
        var data = md5.ComputeHash(Encoding.UTF8.GetBytes(content));
        
        var sb = new StringBuilder();

        for (int i = 0; i < data.Length; i++)
        {
            sb.Append(data[i].ToString("x2"));
        }

        return sb.ToString();
    }
}

#endregion

#region File

internal static class Files
{
    private static readonly string _path = _devPath;
    
    public static string CurrentPath { get { return _path; } }

    public static bool InRepo()
    {
        return GitletPath() != null;
    }
    
    public static void AssertInRepo()
    {
        if (!InRepo())
        {
            throw new Exception("Not in gitlet repository.");
        }
    }
    
    public static string PathFromRepoRoot(string path)
    {
        return Relative(WorkingCopyPath(), path);
    }
    
    private static string Relative(string folder, string filespec)
    {
        var dir = new DirectoryInfo(filespec);
        
        // Folders must end in a slash
        if ((dir.Attributes & FileAttributes.Directory) == FileAttributes.Directory
            && !filespec.EndsWith(Path.DirectorySeparatorChar.ToString()))
        {
            filespec += Path.DirectorySeparatorChar;
        }
    
        Uri pathUri = new Uri(filespec);
        
        // Folders must end in a slash
        if (!folder.EndsWith(Path.DirectorySeparatorChar.ToString()))
        {
            folder += Path.DirectorySeparatorChar;
        }
        
        Uri folderUri = new Uri(Path.GetFullPath(folder));
        
        var relative = Uri.UnescapeDataString(folderUri.MakeRelativeUri(pathUri).ToString().Replace('/', Path.DirectorySeparatorChar));
        return string.IsNullOrEmpty(relative) ? ".\\" : relative;
    }
    
    public static void Write(string file, string content)
    {
        WriteFilesFromTree(
            Files.Relative(Files.CurrentPath, file).Split(Path.DirectorySeparatorChar)
                .Reverse()
                .Aggregate(
                    (ITree)new File(file, content),
                    (child, dir) => new Directory(dir, child)),
            Path.DirectorySeparatorChar.ToString());
    }
    
    public static void WriteFilesFromTree(ITree tree, string prefix)
    {
        var path = Path.Combine(prefix, tree.Name);
    
        var file = tree as File;
        if (file != null)
        {
            File.WriteAllText(path, file.Contents);
        }
        else
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            
            var dir = (Directory)tree;
            
            foreach (var item in dir.Contents)
            {
                WriteFilesFromTree(item, path);
            }
        }
    }
    
    public static string Read(string path)
    {
        if (File.Exists(path))
        {
            return File.ReadAllText(path);
        }
        
        return null;
    }
    
    public static string GitletPath()
    {
        var dir = _path;
    
        var dirInfo = new DirectoryInfo(dir);
        
        if (dirInfo.Exists)
        {
            var potentialConfigFile = Path.Combine(dir, "config");
            var potentialGitletPath = Path.Combine(dir, ".gitlet");
            
            if (File.Exists(potentialConfigFile))
            {
                var config = File.ReadAllText(potentialConfigFile);
                
                if (config.Contains("[core]"))
                {
                    return dir;
                }
            }
            else if (Directory.Exists(potentialGitletPath))
            {
                return potentialGitletPath;
            }
        }
        
        return null;
    }
    
    public static string WorkingCopyPath(string path = null)
    {
        return Path.Combine(GitletPath(), "..", path ?? "");
    }
    
    public static string[] LsRecursive(string path)
    {
        return Directory.GetFiles(path);
    }
    
    public static Directory NestFlatTree(Dictionary<string, string> pathToContentMap)
    {
        var root = new Directory();
        
        foreach (var item in pathToContentMap)
        {
            var split = Files.Relative(Files.CurrentPath, item.Key).Split(Path.DirectorySeparatorChar);
            
            var dir = root;
            
            foreach (var dirName in split.Take(split.Length - 1))
            {
                dir = dir.GetOrAddDirectory(dirName);
            }
            
            dir.Add(new File(split[split.Length - 1], item.Value));
        }
        
        return root;
    }
}

internal class Section
{
    private readonly string _name;
    private readonly string _label;
    private readonly IEnumerable<Setting> _settings;
    
    public Section(string name, string label, IEnumerable<Setting> settings)
    {
        _name = name;
        _label = label;
        _settings = settings;
    }
    
    public string Name { get { return _name; } }
    public string Label { get { return _label; } }
    public IEnumerable<Setting> Settings { get { return _settings; } }
}

internal class Setting
{
    private readonly string _name;
    private readonly string _value;
    
    public Setting(string name, string value)
    {
        _name = name; 
        _value = value;
    }

    public string Name { get { return _name; } }
    public string Value { get { return _value; } }
}

internal interface ITree
{
    string Name { get; }
}

internal class Directory : ITree
{
    private readonly string _name;
    private readonly List<ITree> _contents;

    public Directory()
        : this(null, (IEnumerable<ITree>)null)
    {
    }

    public Directory(params ITree[] contents)
        : this(null, contents)
    {
    }

    public Directory(string name, params ITree[] contents)
        : this(name, (IEnumerable<ITree>)contents)
    {
    }
    
    public Directory(string name = null, IEnumerable<ITree> contents = null)
    {
        _name = name ?? "";
        _contents = (contents as List<ITree>) ?? (contents == null ? new List<ITree>() : contents.ToList());
    }

    public string Name { get { return _name; } }
    public IEnumerable<ITree> Contents { get { return _contents; } }
    
    public void Add(ITree tree)
    {
        _contents.Add(tree);
    }
    
    public Directory GetOrAddDirectory(string dirName)
    {
        var dir = _contents.OfType<Directory>().FirstOrDefault(item => item.Name == dirName);
        
        if (dir != null)
        {
            return dir;
        }
        
        dir = new Directory(dirName);
        Add(dir);
        return dir;
    }
    
    public static bool Exists(string path)
    {
        return System.IO.Directory.Exists(path);
    }
    
    public static void CreateDirectory(string path)
    {
        System.IO.Directory.CreateDirectory(path);
    }
    
    public static string[] GetFiles(string path)
    {
        return GetFilesEnumerable(path).ToArray();
    }
    
    private static IEnumerable<string> GetFilesEnumerable(string path)
    {
        var dir = new DirectoryInfo(path);
        
        if (dir.Name == ".gitlet")
        {
            yield break;
        }
        
        // If it's a file, not a directory, just return that file.
        if ((dir.Attributes & FileAttributes.Directory) != FileAttributes.Directory)
        {
            yield return path;
            yield break;
        }
        
        foreach (var file in System.IO.Directory.GetFiles(path))
        {
            yield return file;
        }
        
        foreach (var subDir in System.IO.Directory.GetDirectories(path))
        {
            foreach (var subDirFile in GetFiles(subDir))
            {
                yield return subDirFile;
            }
        }
    }
}

internal class File : ITree
{
    private readonly string _name;
    private readonly string _contents;

    public File(string name, string contents = null)
    {
        _name = name;
        _contents = contents ?? "";
    }

    public string Name { get { return _name; } }
    public string Contents { get { return _contents; } }
    
    public static bool Exists(string path)
    {
        return System.IO.File.Exists(path);
    }
    
    public static string ReadAllText(string path)
    {
        return System.IO.File.ReadAllText(path);
    }
    
    public static string[] ReadAllLines(string path)
    {
        return System.IO.File.ReadAllLines(path).Where(s => s != "").ToArray();
    }
    
    public static void WriteAllText(string path, string contents)
    {
        System.IO.File.WriteAllText(path, contents);
    }
    
    public static void Delete(string path)
    {
        System.IO.File.Delete(path);
    }
}

#endregion