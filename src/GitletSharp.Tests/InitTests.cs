using GitletSharp.Core;
using NUnit.Framework;

namespace GitletSharp.Tests
{
    public class InitTests : TestBase
    {
        [Test]
        public void GivenExistingRepository_ThenInitThrowException()
        {
            Gitlet.Init();

            Assert.That(() => Gitlet.Init(), Throws.Exception.With.Message.EqualTo("unsupported"));
        }

        [Test]
        public void GivenNewRepository_WhenNotBare_ThenInitCreatesHead()
        {
            Gitlet.Init();

            var head = Files.CurrentPath.Combine(".gitlet", "HEAD").ReadAllText();

            Assert.That(head, Is.EqualTo("ref: refs/heads/master\n"));
        }

        [Test]
        public void GivenNewRepository_WhenBare_ThenInitCreatesHead()
        {
            Gitlet.Init(Bare());

            var head = Files.CurrentPath.Combine("HEAD").ReadAllText();

            Assert.That(head, Is.EqualTo("ref: refs/heads/master\n"));
        }

        [Test]
        public void GivenNewRepository_WhenNotBare_ThenInitCreatesConfig()
        {
            Gitlet.Init();

            var config = Files.CurrentPath.Combine(".gitlet", "config").ReadAllText();

            Assert.That(config, Is.EqualTo("[core]\n    bare = false\n"));
        }

        [Test]
        public void GivenNewRepository_WhenBare_ThenInitCreatesConfig()
        {
            Gitlet.Init(Bare());

            var config = Files.CurrentPath.Combine("config").ReadAllText();

            Assert.That(config, Is.EqualTo("[core]\n    bare = true\n"));
        }

        [Test]
        public void GivenNewRepository_WhenNotBare_ThenInitCreatesObjectsDirectory()
        {
            Gitlet.Init();

            var objectsPath = Files.CurrentPath.Combine(".gitlet", "objects");

            Assert.That(Directory.Exists(objectsPath), Is.True);
        }

        [Test]
        public void GivenNewRepository_WhenBare_ThenInitCreatesObjectsDirectory()
        {
            Gitlet.Init(Bare());

            var objectsPath = Files.CurrentPath.Combine("objects");

            Assert.That(Directory.Exists(objectsPath), Is.True);
        }

        [Test]
        public void GivenNewRepository_WhenNotBare_ThenInitCreatesRefsDirectory()
        {
            Gitlet.Init();

            var refsPath = Files.CurrentPath.Combine(".gitlet", "refs");

            Assert.That(Directory.Exists(refsPath), Is.True);
        }

        [Test]
        public void GivenNewRepository_WhenBare_ThenInitCreatesRefsDirectory()
        {
            Gitlet.Init(Bare());

            var refsPath = Files.CurrentPath.Combine("refs");

            Assert.That(Directory.Exists(refsPath), Is.True);
        }

        [Test]
        public void GivenNewRepository_WhenNotBare_ThenInitCreatesRefsHeadsDirectory()
        {
            Gitlet.Init();

            var refsHeadsPath = Files.CurrentPath.Combine(".gitlet", "refs", "heads");

            Assert.That(Directory.Exists(refsHeadsPath), Is.True);
        }

        [Test]
        public void GivenNewRepository_WhenBare_ThenInitCreatesRefsHeadsDirectory()
        {
            Gitlet.Init(Bare());

            var refsHeadsPath = Files.CurrentPath.Combine("refs", "heads");

            Assert.That(Directory.Exists(refsHeadsPath), Is.True);
        }

        private static InitOptions Bare()
        {
            return new InitOptions { Bare = true };
        }
    }
}
