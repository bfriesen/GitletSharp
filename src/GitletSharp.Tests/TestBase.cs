using System.IO;
using GitletSharp.Core;
using NUnit.Framework;

namespace GitletSharp.Tests
{
    public abstract class TestBase
    {
        private string _previousCurrentPath;

        [SetUp]
        public void Setup()
        {
            _previousCurrentPath = Files.CurrentPath;
            
            var path = Path.GetTempPath().Combine("GitletSharp.Tests");

            if (path.DirectoryExists())
            {
                path.DeleteDirectory();
            }

            path.CreateDirectory();
            Files.CurrentPath = path;
        }

        [TearDown]
        public void Teardown()
        {
            Files.CurrentPath = _previousCurrentPath;
        }
    }
}