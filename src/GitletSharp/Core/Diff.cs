using System;
using System.Collections.Generic;
using System.Linq;

namespace GitletSharp.Core
{
    internal class Diff
    {
        public FileStatus Status { get; set; }
        public string Receiver { get; set; }
        public string Giver { get; set; }
        public string Base { get; set; }

        public class FileStatus
        {
            public static readonly FileStatus ADD = new FileStatus("A");
            public static readonly FileStatus MODIFY = new FileStatus("M");
            public static readonly FileStatus DELETE = new FileStatus("D");
            public static readonly FileStatus SAME = new FileStatus("SAME");
            public static readonly FileStatus CONFLICT = new FileStatus("CONFLICT");

            private readonly string _value;

            private FileStatus(string value)
            {
                _value = value;
            }

            public string Value
            {
                get { return _value; }
            }


        }

        public static new Dictionary<string, FileStatus> NameStatus(Dictionary<string, Diff> dif)
        {
            return
                dif.Where(item => item.Value.Status != FileStatus.SAME)
                .Aggregate(
                    new Dictionary<string, FileStatus>(),
                    (nameStatus, item) =>
                    {
                        nameStatus[item.Key] = item.Value.Status;
                        return nameStatus;
                    });
        }

        public static Dictionary<string, Diff> TocDiff(
            Dictionary<string, string> receiver,
            Dictionary<string, string> giver,
            Dictionary<string, string> @base = null)
        {
            // If `base` was not passed, use `receiver` as the base.
            @base = @base ?? receiver;

            Func<string, FileStatus> fileStatus = path =>
            {
                string r, b, g;

                var receiverPresent = receiver.TryGetValue(path, out r);
                var basePresent = @base.TryGetValue(path, out b);
                var giverPresent = giver.TryGetValue(path, out g);

                if (receiverPresent && giverPresent && r != g)
                {
                    if (r != b && g != b)
                    {
                        return FileStatus.CONFLICT;
                    }

                    return FileStatus.MODIFY;
                }

                if (r == g)
                {
                    return FileStatus.SAME;
                }

                if ((!receiverPresent && !basePresent && giverPresent) ||
                    (receiverPresent && !basePresent && !giverPresent))
                {
                    return FileStatus.ADD;
                }

                if ((receiverPresent && basePresent && !giverPresent) ||
                    (!receiverPresent && basePresent && giverPresent))
                {
                    return FileStatus.DELETE;
                }

                throw new Exception("This should never happen.");
            };

            // Get an array of all the paths in all the versions.
            var paths = receiver.Keys.Concat(@base.Keys).Concat(giver.Keys);

            return
                paths.Distinct()
                    .Aggregate(
                        new Dictionary<string, Diff>(),
                        (dictionary, path) =>
                        {
                            string r, b, g;

                            receiver.TryGetValue(path, out r);
                            @base.TryGetValue(path, out b);
                            giver.TryGetValue(path, out g);

                            dictionary[path] = new Diff
                            {
                                Status = fileStatus(path),
                                Receiver = r,
                                Giver = g,
                                Base = b,
                            };

                            return dictionary;
                        });
        }

        /// <summary>
        /// Gets a list of files
        /// changed in the working copy.  It gets a list of the files that
        /// are different in the head commit and the commit for the passed
        /// hash.  It returns a list of paths that appear in both lists.
        /// </summary>
        public static string[] ChangedFilesCommitWouldOverwrite(string hash)
        {
            var headHash = Refs.Hash("HEAD");

            return
                Diff.NameStatus(Diff.GetDiff(headHash)).Keys
                    .Intersect(Diff.NameStatus(Diff.GetDiff(headHash, hash)).Keys)
                    .ToArray();
        }

        /// <summary>
        /// Returns a diff object (see above for the format of a
        /// diff object).  If `hash1` is passed, it is used as the first
        /// version in the diff.  If it is not passed, the index is used.  If
        /// `hash2` is passed, it is used as the second version in the diff.
        /// If it is not passed` the working copy is used.
        /// </summary>
        private static Dictionary<string, Diff> GetDiff(string hash1 = null, string hash2 = null)
        {
            var a = hash1 == null ? Index.Toc() : Objects.CommitToc(hash1);
            var b = hash2 == null ? Index.WorkingCopyToc() : Objects.CommitToc(hash2);
            return Diff.TocDiff(a, b);
        }

        public static string[] AddedOrModifiedFiles()
        {
            var headToc = Refs.Hash("HEAD") != null ? Objects.CommitToc(Refs.Hash("HEAD")) : new Dictionary<string, string>();
            var wc = Diff.NameStatus(Diff.TocDiff(headToc, Index.WorkingCopyToc()));
            return wc.Where(item => item.Value != FileStatus.DELETE).Select(item => item.Key).ToArray();
        }
    }
}
