using GitDaif.ServiceAPI;
using LibGit2Sharp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using REBUSS.GitDaif.Service.API.DTO.Requests;
using REBUSS.GitDaif.Service.API.Services;

namespace REBUSS.GitDaif.Service.API.IntegrationTests.Git
{
    [TestFixture]
    public class GitServiceTests
    {
        private GitService _gitService;
        private string _filePath;
        private string _branchName;
        private AppSettings _appSettings;

        [SetUp]
        public void Setup()
        {
            var serviceCollection = new ServiceCollection();
            _appSettings = BuildSettings();
            _gitService = new GitService(_appSettings);

            _branchName = "testing";
        }

        [Test]
        public async Task GetBranchNameForPullRequest_Should_Return_Correct_BranchName()
        {

            // Act
            var result = await _gitService.GetBranchNameForPullRequest(GetPullRequestData());

            // Assert
            Assert.That(result, Is.EqualTo(_branchName));
        }

        [Test]
        public async Task GetDiffContentForChanges_Should_Return_Valid_Diff()
        {
            // Arrange
            var localRepoPath = _appSettings.LocalRepoPath;

            // Act
            var result = await _gitService.GetPullRequestDiffContent(GetPullRequestData(), new Repository(localRepoPath));

            // Assert
            Assert.That(result, Is.Not.Empty);
            Assert.That(result, Is.Not.Null);
        }

        [Test]
        public async Task ExtractModifiedFileName_Should_Return_Correct_FileName()
        {
            // Arrange
            var localRepoPath = _appSettings.LocalRepoPath;
            var diffContent = await _gitService.GetPullRequestDiffContent(GetPullRequestData(), new Repository(localRepoPath));

            // Act
            var result = _gitService.ExtractModifiedFileName(diffContent);

            // Assert
            Assert.That(result, Is.EqualTo(Path.GetFileName(_filePath)));
        }

        [Test]
        public async Task GetFullDiffFileFor_Should_Return_Valid_Diff()
        {
            // Arrange
            var localRepoPath = _appSettings.LocalRepoPath;

            // Act
            var result = await _gitService.GetFullDiffFileFor(new Repository(localRepoPath), GetPullRequestData(), _filePath);

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

        private AppSettings BuildSettings()
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            return new AppSettings()
            {
                DiffFilesDirectory = config[ConfigConsts.DiffFilesDirectory],
                LocalRepoPath = config[ConfigConsts.LocalRepoPathKey],
                PersonalAccessToken = config[ConfigConsts.PersonalAccessTokenKey]
            };
        }

        private PullRequestData GetPullRequestData()
        {
            return new PullRequestData
            {
                OrganizationName = "REBUSS",
                ProjectName = "REBUSS",
                RepositoryName = "REBUSS",
                Id = 1
            };
        }
    }
}