using Microsoft.TeamFoundation.SourceControl.WebApi;

namespace REBUSS.AzureDevOps.PullRequestAPI.Git.Model
{
    public interface IGitClient
    {
        Task<GitPullRequest> GetPullRequestAsync(int pullRequestId);
        Task<GitPullRequestIteration> GetLastIterationAsync(int pullRequestId);
        Task<GitPullRequestIterationChanges> GetIterationChangesAsync(int pullRequestId, int iterationId);
    }
}
