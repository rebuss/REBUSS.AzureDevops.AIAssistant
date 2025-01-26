using AzureDevOpsPullRequestAPI;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using REBUSS.AzureDevOps.PullRequestAPI.Services;

namespace REBUSS.AzureDevOps.PullRequestAPI.UnitTests.Services
{
    [TestFixture]
    public class GitServiceTests
    {
        private readonly Mock<IConfiguration> _configurationMock;
        private readonly GitService _gitService;

        public GitServiceTests()
        {
            _configurationMock = new Mock<IConfiguration>();

            _configurationMock.Setup(config => config[ConfigConsts.PersonalAccessTokenKey]).Returns("testToken");
            _configurationMock.Setup(config => config[ConfigConsts.OrganizationNameKey]).Returns("testOrg");
            _configurationMock.Setup(config => config[ConfigConsts.ProjectNameKey]).Returns("testProject");
            _configurationMock.Setup(config => config[ConfigConsts.RepositoryNameKey]).Returns("testRepo");
            _configurationMock.Setup(config => config[ConfigConsts.LocalRepoPathKey]).Returns("testPath");

            _gitService = new GitService(_configurationMock.Object);
        }

        [Test]
        public void Constructor_Should_Throw_ArgumentNullException_When_Configuration_Is_Null()
        {
            Assert.Throws<ArgumentNullException>(() => new GitService(null));
        }

        [Test]
        public void ExtractBranchNameFromRef_Should_Return_Correct_BranchName()
        {
            var method = _gitService.GetType().GetMethod("ExtractBranchNameFromRef", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var result = method.Invoke(_gitService, new object[] { "refs/heads/testBranch" });
            result.Should().Be("testBranch");
        }
    }
}
