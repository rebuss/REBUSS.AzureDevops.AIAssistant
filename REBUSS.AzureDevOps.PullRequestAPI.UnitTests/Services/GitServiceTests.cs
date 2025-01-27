using AzureDevOpsPullRequestAPI;
using FluentAssertions;
using LibGit2Sharp;
using Microsoft.Extensions.Configuration;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Moq;
using REBUSS.AzureDevOps.PullRequestAPI.Git.Model;
using REBUSS.AzureDevOps.PullRequestAPI.Services;
using System.Text;

namespace REBUSS.AzureDevOps.PullRequestAPI.UnitTests.Services
{
    [TestFixture]
    public class GitServiceTests
    {
        private Mock<IConfiguration> _configurationMock;
        private GitService _gitService;
        private Mock<IGitClient> _gitClientMock;

        [SetUp]
        public void SetUp()
        {
            _configurationMock = new Mock<IConfiguration>();
            _gitClientMock = new Mock<IGitClient>();

            _configurationMock.Setup(config => config[ConfigConsts.PersonalAccessTokenKey]).Returns("testToken");
            _configurationMock.Setup(config => config[ConfigConsts.OrganizationNameKey]).Returns("testOrg");
            _configurationMock.Setup(config => config[ConfigConsts.ProjectNameKey]).Returns("testProject");
            _configurationMock.Setup(config => config[ConfigConsts.RepositoryNameKey]).Returns("testRepo");
            _configurationMock.Setup(config => config[ConfigConsts.LocalRepoPathKey]).Returns("testPath");

            _gitService = new GitService(_configurationMock.Object, _gitClientMock.Object);
        }

        [Test]
        public void Constructor_Should_Throw_ArgumentNullException_When_Configuration_Is_Null()
        {
            Assert.Throws<ArgumentNullException>(() => new GitService(null));
        }

        [Test]
        public void ExtractBranchNameFromRef_Should_Return_Correct_BranchName()
        {
            // Act
            var result = _gitService.ExtractBranchNameFromRef("refs/heads/testBranch");

            // Assert
            result.Should().Be("testBranch");
        }

        [Test]
        public void ExtractModifiedFileName_Should_Return_Correct_FileName()
        {
            // Arrange
            var diffContent = "diff --git a/file1.txt b/file1.txt\nindex 83db48f..f735c4e 100644\n--- a/file1.txt\n+++ b/file1.txt\n@@ -1,4 +1,4 @@";

            // Act
            var result = _gitService.ExtractModifiedFileName(diffContent);

            // Assert
            result.Should().Be("file1.txt");
        }

        [Test]
        public void ExtractModifiedFileName_Should_Return_Empty_String_When_No_FileName_Found()
        {
            // Arrange
            var diffContent = "some random text without diff --git";

            // Act
            var result = _gitService.ExtractModifiedFileName(diffContent);

            // Assert
            result.Should().BeEmpty();
        }

        [Test]
        public void ExtractModifiedFileName_Should_Handle_Multiple_Diff_Entries()
        {
            // Arrange
            var diffContent = "diff --git a/file1.txt b/file1.txt\nindex 83db48f..f735c4e 100644\n--- a/file1.txt\n+++ b/file1.txt\n@@ -1,4 +1,4 @@\ndiff --git a/file2.txt b/file2.txt\nindex 83db48f..f735c4e 100644\n--- a/file2.txt\n+++ b/file2.txt\n@@ -1,4 +1,4 @@";

            // Act
            var result = _gitService.ExtractModifiedFileName(diffContent);

            // Assert
            result.Should().Be("file1.txt");
        }

        [Test]
        public async Task GetLatestCommitHashForFile_Should_Return_Correct_CommitHash_When_File_Exists()
        {
            // Arrange
            var fileName = "file1.txt";
            var branchName = "main";
            var expectedCommitHash = "abc123";

            var mockRepository = new Mock<IRepository>();
            var mockBranch = new Mock<Branch>();
            var mockCommit = new Mock<Commit>();
            var mockCommitLog = new Mock<ICommitLog>();
            var mockTreeEntry = new Mock<TreeEntry>();

            mockCommit.Setup(c => c[fileName]).Returns(mockTreeEntry.Object);
            mockCommit.Setup(c => c.Sha).Returns(expectedCommitHash);
            mockBranch.Setup(b => b.Commits).Returns(mockCommitLog.Object);
            mockCommitLog.Setup(cl => cl.GetEnumerator()).Returns(new List<Commit> { mockCommit.Object }.GetEnumerator());
            mockRepository.Setup(r => r.Branches[branchName]).Returns(mockBranch.Object);

            // Act
            var result = await _gitService.GetLatestCommitHashForFile(fileName, branchName, mockRepository.Object);

            // Assert
            result.Should().Be(expectedCommitHash);
        }

        [Test]
        public async Task GetLatestCommitHashForFile_Should_Return_Empty_String_When_File_Does_Not_Exist()
        {
            // Arrange
            var fileName = "file1.txt";
            var branchName = "main";

            var mockRepository = new Mock<IRepository>();
            var mockBranch = new Mock<Branch>();
            var mockCommit = new Mock<Commit>();
            var mockCommitLog = new Mock<ICommitLog>();

            mockCommit.Setup(c => c[fileName]).Returns((TreeEntry)null);
            mockBranch.Setup(b => b.Commits).Returns(mockCommitLog.Object);
            mockCommitLog.Setup(cl => cl.GetEnumerator()).Returns(new List<Commit> { mockCommit.Object }.GetEnumerator());
            mockRepository.Setup(r => r.Branches[branchName]).Returns(mockBranch.Object);

            // Act
            var result = await _gitService.GetLatestCommitHashForFile(fileName, branchName, mockRepository.Object);

            // Assert
            result.Should().BeEmpty();
        }

        [Test]
        public async Task GetLatestCommitHashForFile_Should_Return_Empty_String_When_Branch_Does_Not_Exist()
        {
            // Arrange
            var fileName = "file1.txt";
            var branchName = "nonexistent-branch";

            var mockRepository = new Mock<IRepository>();

            mockRepository.Setup(r => r.Branches[branchName]).Returns((Branch)null);

            // Act
            var result = await _gitService.GetLatestCommitHashForFile(fileName, branchName, mockRepository.Object);

            // Assert
            result.Should().BeEmpty();
        }

        [Test]
        public void IsDiffFileContainsChangesInMultipleFiles_Should_Return_True_When_Multiple_Files_Changed()
        {
            // Arrange
            var diffFile = "diff --git a/file1.txt b/file1.txt\n" +
                           "index 83db48f..f735c4e 100644\n" +
                           "--- a/file1.txt\n" +
                           "+++ b/file1.txt\n" +
                           "@@ -1,4 +1,4 @@\n" +
                           "diff --git a/file2.txt b/file2.txt\n" +
                           "index 83db48f..f735c4e 100644\n" +
                           "--- a/file2.txt\n" +
                           "+++ b/file2.txt\n" +
                           "@@ -1,4 +1,4 @@";

            // Act
            var result = _gitService.IsDiffFileContainsChangesInMultipleFiles(diffFile);

            // Assert
            result.Should().BeTrue();
        }

        [Test]
        public void IsDiffFileContainsChangesInMultipleFiles_Should_Return_False_When_Single_File_Changed()
        {
            // Arrange
            var diffFile = "diff --git a/file1.txt b/file1.txt\n" +
                           "index 83db48f..f735c4e 100644\n" +
                           "--- a/file1.txt\n" +
                           "+++ b/file1.txt\n" +
                           "@@ -1,4 +1,4 @@";

            // Act
            var result = _gitService.IsDiffFileContainsChangesInMultipleFiles(diffFile);

            // Assert
            result.Should().BeFalse();
        }

        [Test]
        public void IsDiffFileContainsChangesInMultipleFiles_Should_Return_False_When_No_Files_Changed()
        {
            // Arrange
            var diffFile = "some random text without diff --git";

            // Act
            var result = _gitService.IsDiffFileContainsChangesInMultipleFiles(diffFile);

            // Assert
            result.Should().BeFalse();
        }
    }
}
