using Microsoft.TeamFoundation.SourceControl.WebApi;
using REBUSS.GitDaif.Service.API.DTO.Requests;

namespace REBUSS.GitDaif.Service.API.Git.Model
{
    public interface IGitClient
    {
        Task<GitPullRequest> GetPullRequestAsync(PullRequestData prData);
        Task<GitPullRequestIteration> GetLastIterationAsync(PullRequestData prData);
        Task<GitPullRequestIterationChanges> GetIterationChangesAsync(PullRequestData prData, int iterationId);
    }
}