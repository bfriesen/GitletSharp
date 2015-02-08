using GitletSharp.Core;

namespace GitletSharp
{
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
}
