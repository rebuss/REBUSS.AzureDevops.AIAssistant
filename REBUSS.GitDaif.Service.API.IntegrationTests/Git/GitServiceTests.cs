using GitDaif.ServiceAPI;
using LibGit2Sharp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using REBUSS.GitDaif.Service.API.Git;
using NUnit.Framework;

namespace REBUSS.GitDaif.Service.API.IntegrationTests.Git
{
    [TestFixture]
    public class GitServiceTests
    {
        private GitService _gitService;
        private IConfiguration _configuration;
        private string _filePath;
        private string _branchName;
        private int _pullRequestId;

        [SetUp]
        public void Setup()
        {
            var serviceCollection = new ServiceCollection();
            _configuration = BuildConfiguration();
            _gitService = new GitService(_configuration);

            _filePath = _configuration["TestSettings:FilePath"];
            _branchName = _configuration["TestSettings:BranchName"];
            _pullRequestId = int.Parse(_configuration["TestSettings:PullRequestId"]);
        }

        private IConfiguration BuildConfiguration()
        {
            return new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.local.json", optional: false, reloadOnChange: true)
                .AddJsonFile("appsettings.test.json", optional: false, reloadOnChange: true)
                .Build();
        }

        [Test]
        public async Task GetBranchNameForPullRequest_Should_Return_Correct_BranchName()
        {
            // Arrange
            var pullRequestId = 42882;

            // Act
            var result = await _gitService.GetBranchNameForPullRequest(pullRequestId);

            // Assert
            Assert.That(result, Is.EqualTo(_branchName));
        }

        [Test]
        public async Task GetDiffContentForChanges_Should_Return_Valid_Diff()
        {
            // Arrange
            var localRepoPath = _configuration[ConfigConsts.LocalRepoPathKey];

            // Act
            var result = await _gitService.GetPullRequestDiffContent(_pullRequestId, new Repository(localRepoPath));

            // Assert
            Assert.That(result, Is.Not.Empty);
            Assert.That(result, Is.Not.Null);
        }

        [Test]
        public async Task ExtractModifiedFileName_Should_Return_Correct_FileName()
        {
            // Arrange
            var localRepoPath = _configuration[ConfigConsts.LocalRepoPathKey];
            var diffContent = await _gitService.GetPullRequestDiffContent(_pullRequestId, new Repository(localRepoPath));

            // Act
            var result = _gitService.ExtractModifiedFileName(diffContent);

            // Assert
            Assert.That(result, Is.EqualTo(Path.GetFileName(_filePath)));
        }

        [Test]
        public async Task GetFullDiffFileFor_Should_Return_Valid_Diff()
        {
            // Arrange
            var localRepoPath = _configuration[ConfigConsts.LocalRepoPathKey];

            // Act
            var result = await _gitService.GetFullDiffFileFor(new Repository(localRepoPath), _pullRequestId, _filePath);

            // Assert
            Assert.That(result, Is.Not.Empty);
            Assert.That(result, Is.Not.Null);
        }

        [Test]
        public async Task GetLocalChangesDiffContent_Should_Return_Valid_Diff_For_Staged_Changes_Only()
        {
            // Act
            var result = await _gitService.GetLocalChangesDiffContent();

            // Assert
            Assert.That(result, Is.Not.Empty);
            Assert.That(result, Is.Not.Null);
        }
    }
}

