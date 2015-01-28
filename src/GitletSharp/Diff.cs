namespace GitletSharp
{
    internal static class Diff
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

}
