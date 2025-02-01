using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using REBUSS.GitDaif.Service.API.DTO.Requests;

namespace REBUSS.GitDaif.Service.API.Services.Model
{
    public class GitClient : IGitClient
    {
        private readonly string personalAccessToken;

        public GitClient(string pat)
        {
            personalAccessToken = pat;
        }

        public async Task<GitPullRequest> GetPullRequestAsync(PullRequestData prData)
        {
            using (var gitClient = GetGitClient(prData.OrganizationName))
            {
                return await gitClient.GetPullRequestAsync(prData.ProjectName, prData.RepositoryName, prData.Id);
            }
        }

        public async Task<GitPullRequestIteration> GetLastIterationAsync(PullRequestData prData)
        {
            using (var gitClient = GetGitClient(prData.OrganizationName))
            {
                var iterations = await gitClient.GetPullRequestIterationsAsync(prData.ProjectName, prData.RepositoryName, prData.Id);
                return iterations.Last();
            }
        }

        public async Task<GitPullRequestIterationChanges> GetIterationChangesAsync(PullRequestData prData, int iterationId)
        {
            using (var gitClient = GetGitClient(prData.OrganizationName))
            {
                return await gitClient.GetPullRequestIterationChangesAsync(prData.ProjectName, prData.RepositoryName, prData.Id, iterationId);
            }
        }

        private GitHttpClient GetGitClient(string organization)
        {
            var orgUrl = new Uri($"https://dev.azure.com/{organization}");
            var credentials = new VssBasicCredential(string.Empty, personalAccessToken);
            var connection = new VssConnection(orgUrl, credentials);
            return connection.GetClient<GitHttpClient>();
        }
    }
}