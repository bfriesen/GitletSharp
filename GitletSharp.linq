<Query Kind="Program" />

void Main()
{
    Gitlet.Init(new InitOptions() { Bare = false });
    Gitlet.Add(_devPath);
}

private const string _devPath = @"C:\Temp\gitlet_sandbox";

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
        var addedFiles = Files.lsRecursive(path);
        
        // Abort if no files matched `path`.
        if (addedFiles.Length == 0)
        {
            throw new Exception(path + " did not match any files"); // TODO: make 'path' relative to repo root
        }
        
        // Otherwise, use the `UpdateIndex()` Git command to actually add
        // the files.
        foreach (var file in addedFiles)
        {
            UpdateIndex(file, new UpdateIndexOptions { Add = true });
        }
    }
    
    public static void UpdateIndex(string file, UpdateIndexOptions options)
    {
        // TODO: Implement
        ("Adding '" + file + "' to index...").Dump();
    }
}

public class InitOptions
{
    public bool Bare { get; set; }
}

public class UpdateIndexOptions
{
    public bool Add { get; set; }
}

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
            var dir = (Directory)tree;
            
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            
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
    
    public static string GitletPath(string dir = null)
    {
        dir = dir ?? _path;
    
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
            else if (dirInfo.Parent != null)
            {
                return GitletPath(Path.Combine(dir, ".."));
            }
        }
        
        return null;
    }
    
    public static string[] lsRecursive(string path)
    {
        return Directory.GetFiles(path);
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
    private readonly IEnumerable<ITree> _contents;

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
        _contents = contents ?? new ITree[0];
    }

    public string Name { get { return _name; } }
    public IEnumerable<ITree> Contents { get { return _contents; } }
    
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
    
    public static void WriteAllText(string path, string contents)
    {
        System.IO.File.WriteAllText(path, contents);
    }
}

#endregion