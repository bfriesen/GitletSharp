using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace GitletSharp
{
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
}
