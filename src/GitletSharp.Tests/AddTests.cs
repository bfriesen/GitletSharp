using GitletSharp.Core;
using NUnit.Framework;

namespace GitletSharp.Tests
{
    public class AddTests : TestBase
    {
        [SetUp]
        public new void Setup()
        {
            Steps.Init();
        }

        // TODO: Need to verify directory searching

        [Test]
        public void GivenANewRepository_WhenSpecifiedFileExists_ThenAddCreatesIndex()
        {
            NewRepositoryAssumptions();

            Files.CurrentPath.Combine("number.txt").WriteAllText("first!");
            Gitlet.Add("number.txt");

            var index = Files.CurrentPath.Combine(".gitlet", "index").ReadAllText();

            var expectedIndex = string.Format("number.txt 0 {0}\n", Util.Hash("first!"));
            Assert.That(index, Is.EqualTo(expectedIndex));
        }

        [Test]
        public void GivenANewRepository_WhenSpecifiedFileExists_ThenAddCreatesAnObjectFile()
        {
            NewRepositoryAssumptions();

            Files.CurrentPath.Combine("number.txt").WriteAllText("first!");
            Gitlet.Add("number.txt");

            var objectFile = Files.CurrentPath.Combine(".gitlet", "objects", Util.Hash("first!")).ReadAllText();

            Assert.That(objectFile, Is.EqualTo("first!"));
        }

        [Test]
        public void GivenANewRepository_WhenSpecifiedFileDoesNotExists_ThenAddThrowsException()
        {
            NewRepositoryAssumptions();

            Assert.That(() => Gitlet.Add("number.txt"), Throws.Exception.With.Message.ContainsSubstring("did not match any files"));
        }

        private static void NewRepositoryAssumptions()
        {
            Assume.That(Files.CurrentPath.Combine(".gitlet", "index").ReadAllText(), Is.Null,
                "The index file should not exist prior to the execution of this test.");
        }
    }
}