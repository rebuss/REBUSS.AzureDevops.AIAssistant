using AzureDevOpsPullRequestAPI;
using FluentAssertions;
using LibGit2Sharp;
using Microsoft.Extensions.Configuration;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Moq;
using REBUSS.AzureDevOps.PullRequestAPI.Git.Model;
using REBUSS.AzureDevOps.PullRequestAPI.Git;

namespace REBUSS.AzureDevOps.PullRequestAPI.UnitTests.Git
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
            mockRepository.Setup(r => r.Branches[$"refs/remotes/origin/{branchName}"]).Returns(mockBranch.Object);

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

        [Test]
        public async Task IsLatestCommitIncludedInDiff_Should_Return_True_When_Latest_Commit_Is_Included()
        {
            // Arrange
            var branchName = "main";
            var diffContent = "some diff content with commit hash abc123";
            var latestCommitHash = "abc123";

            var mockRepository = new Mock<IRepository>();
            var mockBranch = new Mock<Branch>();
            var mockCommit = new Mock<Commit>();
            var mockCommitLog = new Mock<ICommitLog>();

            mockCommit.Setup(c => c.Sha).Returns(latestCommitHash);
            mockBranch.Setup(b => b.Commits).Returns(mockCommitLog.Object);
            mockCommitLog.Setup(cl => cl.GetEnumerator()).Returns(new List<Commit> { mockCommit.Object }.GetEnumerator());
            mockRepository.Setup(r => r.Branches[branchName]).Returns(mockBranch.Object);

            // Act
            var result = await _gitService.IsLatestCommitIncludedInDiff(branchName, diffContent, mockRepository.Object);

            // Assert
            result.Should().BeTrue();
        }

        [Test]
        public async Task IsLatestCommitIncludedInDiff_Should_Return_False_When_Latest_Commit_Is_Not_Included()
        {
            // Arrange
            var branchName = "main";
            var diffContent = "some diff content without the latest commit hash";
            var latestCommitHash = "abc123";

            var mockRepository = new Mock<IRepository>();
            var mockBranch = new Mock<Branch>();
            var mockCommit = new Mock<Commit>();
            var mockCommitLog = new Mock<ICommitLog>();

            mockCommit.Setup(c => c.Sha).Returns(latestCommitHash);
            mockBranch.Setup(b => b.Commits).Returns(mockCommitLog.Object);
            mockCommitLog.Setup(cl => cl.GetEnumerator()).Returns(new List<Commit> { mockCommit.Object }.GetEnumerator());
            mockRepository.Setup(r => r.Branches[branchName]).Returns(mockBranch.Object);

            // Act
            var result = await _gitService.IsLatestCommitIncludedInDiff(branchName, diffContent, mockRepository.Object);

            // Assert
            result.Should().BeFalse();
        }

        [Test]
        public async Task IsLatestCommitIncludedInDiff_Should_Return_False_When_Branch_Does_Not_Exist()
        {
            // Arrange
            var branchName = "nonexistent-branch";
            var diffContent = "some diff content";

            var mockRepository = new Mock<IRepository>();

            mockRepository.Setup(r => r.Branches[branchName]).Returns((Branch)null);

            // Act
            var result = await _gitService.IsLatestCommitIncludedInDiff(branchName, diffContent, mockRepository.Object);

            // Assert
            result.Should().BeFalse();
        }

        [Test]
        public async Task IsLatestCommitIncludedInDiff2_Should_Return_True_When_Latest_Commit_Is_Included()
        {
            // Arrange
            var id = 1;
            var diffContent = "diff --git a/file1.txt b/file1.txt\nindex 83db48f..f735c4e 100644\n--- a/file1.txt\n+++ b/file1.txt\n@@ -1,4 +1,4 @@\nabc123";
            var latestCommitHash = "abc123";
            var branchName = "main";

            var mockRepository = new Mock<IRepository>();
            var mockBranch = new Mock<Branch>();
            var mockCommit = new Mock<Commit>();
            var mockCommitLog = new Mock<ICommitLog>();
            var mockTreeEntry = new Mock<TreeEntry>();

            mockCommit.Setup(c => c["file1.txt"]).Returns(mockTreeEntry.Object);
            mockCommit.Setup(c => c.Sha).Returns(latestCommitHash);
            mockBranch.Setup(b => b.Commits).Returns(mockCommitLog.Object);
            mockCommitLog.Setup(cl => cl.GetEnumerator()).Returns(new List<Commit> { mockCommit.Object }.GetEnumerator());
            mockRepository.Setup(r => r.Branches[$"refs/remotes/origin/{branchName}"]).Returns(mockBranch.Object);

            _gitClientMock.Setup(g => g.GetPullRequestAsync(id)).ReturnsAsync(new GitPullRequest { SourceRefName = "refs/heads/main" });

            // Act
            var result = await _gitService.IsLatestCommitIncludedInDiff(id, diffContent, mockRepository.Object);

            // Assert
            result.Should().BeTrue();
        }

        [Test]
        public async Task IsLatestCommitIncludedInDiff2_Should_Return_False_When_Latest_Commit_Is_Not_Included()
        {
            // Arrange
            var id = 1;
            var diffContent = "some diff content without the latest commit hash";
            var latestCommitHash = "abc123";
            var branchName = "main";

            var mockRepository = new Mock<IRepository>();
            var mockBranch = new Mock<Branch>();
            var mockCommit = new Mock<Commit>();
            var mockCommitLog = new Mock<ICommitLog>();

            mockCommit.Setup(c => c.Sha).Returns(latestCommitHash);
            mockBranch.Setup(b => b.Commits).Returns(mockCommitLog.Object);
            mockCommitLog.Setup(cl => cl.GetEnumerator()).Returns(new List<Commit> { mockCommit.Object }.GetEnumerator());
            mockRepository.Setup(r => r.Branches[branchName]).Returns(mockBranch.Object);

            _gitClientMock.Setup(g => g.GetPullRequestAsync(id)).ReturnsAsync(new GitPullRequest { SourceRefName = "refs/heads/main" });

            // Act
            var result = await _gitService.IsLatestCommitIncludedInDiff(id, diffContent, mockRepository.Object);

            // Assert
            result.Should().BeFalse();
        }

        [Test]
        public async Task IsLatestCommitIncludedInDiff2_Should_Return_False_When_Branch_Does_Not_Exist()
        {
            // Arrange
            var id = 1;
            var diffContent = "some diff content";
            var branchName = "nonexistent-branch";

            var mockRepository = new Mock<IRepository>();

            mockRepository.Setup(r => r.Branches[branchName]).Returns((Branch)null);

            _gitClientMock.Setup(g => g.GetPullRequestAsync(id)).ReturnsAsync(new GitPullRequest { SourceRefName = "refs/heads/nonexistent-branch" });

            // Act
            var result = await _gitService.IsLatestCommitIncludedInDiff(id, diffContent, mockRepository.Object);

            // Assert
            result.Should().BeFalse();
        }

        [Test]
        public async Task IsLatestCommitIncludedInDiff2_Should_Return_True_When_Latest_Commit_Is_Included_In_Single_File_Diff()
        {
            // Arrange
            var id = 1;
            var diffContent = "diff --git a/file1.txt b/file1.txt\nindex 83db48f..f735c4e 100644\n--- a/file1.txt\n+++ b/file1.txt\n@@ -1,4 +1,4 @@\nabc123";
            var latestCommitHash = "abc123";
            var branchName = "main";

            var mockRepository = new Mock<IRepository>();
            var mockBranch = new Mock<Branch>();
            var mockCommit = new Mock<Commit>();
            var mockCommitLog = new Mock<ICommitLog>();
            var mockTreeEntry = new Mock<TreeEntry>();

            mockCommit.Setup(c => c["file1.txt"]).Returns(mockTreeEntry.Object);
            mockCommit.Setup(c => c.Sha).Returns(latestCommitHash);
            mockBranch.Setup(b => b.Commits).Returns(mockCommitLog.Object);
            mockCommitLog.Setup(cl => cl.GetEnumerator()).Returns(new List<Commit> { mockCommit.Object }.GetEnumerator());
            mockRepository.Setup(r => r.Branches[$"refs/remotes/origin/{branchName}"]).Returns(mockBranch.Object);

            _gitClientMock.Setup(g => g.GetPullRequestAsync(id)).ReturnsAsync(new GitPullRequest { SourceRefName = "refs/heads/main" });

            // Act
            var result = await _gitService.IsLatestCommitIncludedInDiff(id, diffContent, mockRepository.Object);

            // Assert
            result.Should().BeTrue();
        }

        [Test]
        public async Task IsLatestCommitIncludedInDiff2_Should_Return_False_When_Latest_Commit_Is_Not_Included_In_Single_File_Diff()
        {
            // Arrange
            var id = 1;
            var diffContent = "diff --git a/file1.txt b/file1.txt\nindex 83db48f..f735c4e 100644\n--- a/file1.txt\n+++ b/file1.txt\n@@ -1,4 +1,4 @@\n";
            var latestCommitHash = "abc123";
            var branchName = "main";

            var mockRepository = new Mock<IRepository>();
            var mockBranch = new Mock<Branch>();
            var mockCommit = new Mock<Commit>();
            var mockCommitLog = new Mock<ICommitLog>();

            mockCommit.Setup(c => c.Sha).Returns(latestCommitHash);
            mockBranch.Setup(b => b.Commits).Returns(mockCommitLog.Object);
            mockCommitLog.Setup(cl => cl.GetEnumerator()).Returns(new List<Commit> { mockCommit.Object }.GetEnumerator());
            mockRepository.Setup(r => r.Branches[branchName]).Returns(mockBranch.Object);

            _gitClientMock.Setup(g => g.GetPullRequestAsync(id)).ReturnsAsync(new GitPullRequest { SourceRefName = "refs/heads/main" });

            // Act
            var result = await _gitService.IsLatestCommitIncludedInDiff(id, diffContent, mockRepository.Object);

            // Assert
            result.Should().BeFalse();
        }

        [Test]
        public async Task GetBranchNameForPullRequest_Should_Return_Correct_BranchName()
        {
            // Arrange
            var pullRequestId = 1;
            var expectedBranchName = "feature-branch";
            var pullRequest = new GitPullRequest
            {
                SourceRefName = "refs/heads/feature-branch"
            };

            _gitClientMock.Setup(g => g.GetPullRequestAsync(pullRequestId)).ReturnsAsync(pullRequest);

            // Act
            var result = await _gitService.GetBranchNameForPullRequest(pullRequestId);

            // Assert
            result.Should().Be(expectedBranchName);
        }

        [Test]
        public async Task GetBranchNameForPullRequest_Should_Retrun_Null_When_PullRequest_Not_Found()
        {
            // Arrange
            var pullRequestId = 1;

            _gitClientMock.Setup(g => g.GetPullRequestAsync(pullRequestId)).ReturnsAsync(() => null);
            _gitService.GitClient = _gitClientMock.Object;

            // Act
            var result = await _gitService.GetBranchNameForPullRequest(pullRequestId);

            // Assert
            result.Should().BeNull();
        }

        [Test]
        public async Task GetBranchNameForPullRequest_Should_Handle_Null_SourceRefName()
        {
            // Arrange
            var pullRequestId = 1;
            var pullRequest = new GitPullRequest
            {
                SourceRefName = null
            };

            _gitClientMock.Setup(g => g.GetPullRequestAsync(pullRequestId)).ReturnsAsync(pullRequest);

            // Act
            var result = await _gitService.GetBranchNameForPullRequest(pullRequestId);

            // Assert
            result.Should().BeNull();
        }

        [Test]
        public async Task GetBranchNameForPullRequest_Should_Handle_Empty_SourceRefName()
        {
            // Arrange
            var pullRequestId = 1;
            var pullRequest = new GitPullRequest
            {
                SourceRefName = string.Empty
            };

            _gitClientMock.Setup(g => g.GetPullRequestAsync(pullRequestId)).ReturnsAsync(pullRequest);

            // Act
            var result = await _gitService.GetBranchNameForPullRequest(pullRequestId);

            // Assert
            result.Should().BeEmpty();
        }

        [Test]
        public void PrepareFilePath_Should_Return_Empty_String_When_FilePath_Is_Null()
        {
            // Arrange
            string filePath = null;

            // Act
            var result = _gitService.PrepareFilePath(filePath);

            // Assert
            result.Should().BeEmpty();
        }

        [Test]
        public void PrepareFilePath_Should_Remove_Leading_Slash()
        {
            // Arrange
            string filePath = "/xxx/.cpp";

            // Act
            var result = _gitService.PrepareFilePath(filePath);

            // Assert
            result.Should().Be("xxx/.cpp");
        }

        [Test]
        public void PrepareFilePath_Should_Return_Same_Path_When_No_Leading_Slash()
        {
            // Arrange
            string filePath = "xxx/.cpp";

            // Act
            var result = _gitService.PrepareFilePath(filePath);

            // Assert
            result.Should().Be(filePath);
        }

        [Test]
        public void PrepareFilePath_Should_Handle_Empty_String()
        {
            // Arrange
            string filePath = string.Empty;

            // Act
            var result = _gitService.PrepareFilePath(filePath);

            // Assert
            result.Should().BeEmpty();
        }
    }
}
