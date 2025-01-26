using AzureDevOpsPullRequestAPI;
using LibGit2Sharp;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using System.Diagnostics;
using System.Text;

namespace REBUSS.AzureDevOps.PullRequestAPI.Services
{
    public class GitService
    {
        private readonly string personalAccessToken;
        private readonly string localRepoPath;
        private readonly string organization;
        private readonly string projectName;
        private readonly string repo;

        public GitService(IConfiguration configuration)
        {
            personalAccessToken = configuration[ConfigConsts.PersonalAccessTokenKey] ?? throw new ArgumentNullException(nameof(personalAccessToken));
            organization = configuration[ConfigConsts.OrganizationNameKey] ?? throw new ArgumentNullException(nameof(organization));
            projectName = configuration[ConfigConsts.ProjectNameKey] ?? throw new ArgumentNullException(nameof(projectName));
            repo = configuration[ConfigConsts.RepositoryNameKey] ?? throw new ArgumentNullException(nameof(repo));
            localRepoPath = configuration[ConfigConsts.LocalRepoPathKey] ?? throw new ArgumentNullException(nameof(localRepoPath));
        }

        public async Task<string> GetPullRequestDiffContent(int pullRequestId)
        {
            Uri orgUrl = new Uri($"https://dev.azure.com/{organization}");
            VssBasicCredential credentials = new VssBasicCredential(string.Empty, personalAccessToken);
            VssConnection connection = new VssConnection(orgUrl, credentials);
            using (GitHttpClient gitClient = connection.GetClient<GitHttpClient>())
            {
                GitPullRequest pullRequest = await gitClient.GetPullRequestAsync(projectName, repo, pullRequestId);
                var iterations = await gitClient.GetPullRequestIterationsAsync(projectName, repo, pullRequestId);
                var lastIteration = iterations.Last();
                GitPullRequestIterationChanges changes = await gitClient.GetPullRequestIterationChangesAsync(projectName, repo, pullRequestId, lastIteration.Id.Value);
                StringBuilder diffContent = new StringBuilder();

                using (var repo = new Repository(localRepoPath))
                {
                    FetchBranches(repo, pullRequest);
                    await AppendDiffContentForChanges(changes, pullRequest, diffContent);
                }

                return diffContent.ToString();
            }
        }

        public void FetchBranches(Repository repo, GitPullRequest pullRequest)
        {
            var remote = repo.Network.Remotes["origin"];
            var fetchOptions = new FetchOptions
            {
                CredentialsProvider = (url, usernameFromUrl, types) =>
                    new UsernamePasswordCredentials
                    {
                        Username = string.Empty,
                        Password = personalAccessToken
                    }
            };

            Commands.Fetch(
                repo,
                remote.Name,
                new[] {
                        ExtractBranchNameFromRef(pullRequest.TargetRefName),
                        ExtractBranchNameFromRef(pullRequest.SourceRefName)
                },
                fetchOptions,
                null);
        }

        public async Task AppendDiffContentForChanges(GitPullRequestIterationChanges changes, GitPullRequest pullRequest, StringBuilder diffContent)
        {
            foreach (var change in changes.ChangeEntries)
            {
                if (change.Item is GitItem gitItem)
                {
                    string localCommitId = pullRequest.LastMergeSourceCommit.CommitId;
                    string remoteCommitId = pullRequest.LastMergeTargetCommit.CommitId;
                    string filePath = gitItem.Path;

                    // Use git diff command to get the diff for the specific file
                    ProcessStartInfo startInfo = new ProcessStartInfo
                    {
                        FileName = "git",
                        Arguments = $"diff {remoteCommitId} {localCommitId} -- {filePath}",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WorkingDirectory = localRepoPath
                    };

                    using (Process process = Process.Start(startInfo))
                    {
                        using (StreamReader reader = process.StandardOutput)
                        {
                            string result = await reader.ReadToEndAsync();
                            diffContent.Append(result);
                        }
                    }
                }
            }
        }

        public async Task<bool> IsLatestCommitIncludedInDiff(string branchName, string diffContent)
        {
            var latestCommitHash = await GetLatestCommitHash(branchName);
            return diffContent.Contains(latestCommitHash);
        }

        public async Task<string> GetFullDiffFileFor(int pullRequestId, string fileName)
        {
            string branchName = await GetBranchNameForPullRequest(pullRequestId);
            string arguments = $"diff {branchName} -- {fileName} --unified=999999";

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = localRepoPath
            };

            using (Process process = Process.Start(startInfo))
            {
                using (StreamReader reader = process.StandardOutput)
                {
                    return await reader.ReadToEndAsync();
                }
            }
        }

        private async Task<string> GetLatestCommitHash(string branchName)
        {
            var repo = new Repository(localRepoPath);
            var branch = repo.Branches[branchName];
            var latestCommit = branch.Commits.First();
            return latestCommit.Sha;
        }

        private async Task<string> GetBranchNameForPullRequest(int pullRequestId)
        {
            Uri orgUrl = new Uri($"https://dev.azure.com/{organization}");
            VssBasicCredential credentials = new VssBasicCredential(string.Empty, personalAccessToken);
            VssConnection connection = new VssConnection(orgUrl, credentials);
            using (GitHttpClient gitClient = connection.GetClient<GitHttpClient>())
            {
                GitPullRequest pullRequest = await gitClient.GetPullRequestAsync(projectName, repo, pullRequestId);
                return ExtractBranchNameFromRef(pullRequest.SourceRefName);
            }
        }

        private string ExtractBranchNameFromRef(string refName)
        {
            return refName?.Replace(string.Format("refs/heads/"), string.Empty);
        }

        public async Task<bool> IsLatestCommitIncludedInDiff(int id, string diffFile)
        {
            var diffContent = File.ReadAllText(diffFile);
            var branchName = await GetBranchNameForPullRequest(id);
            string latestCommitHash;
            if (IsDiffFileContainsChangesInMultipleFiles(diffContent))
            {
                latestCommitHash = await GetLatestCommitHash(branchName);
            }
            else
            {
                var fileName = ExtractModifiedFileName(diffContent);
                latestCommitHash = await GetLatestCommitHashForFile(fileName, branchName);
            }

            return diffFile.Contains(latestCommitHash);
        }

        public string ExtractModifiedFileName(string diffContent)
        {
            var lines = diffContent.Split('\n');
            foreach (var line in lines)
            {
                if (line.StartsWith("diff --git"))
                {
                    var parts = line.Split(' ');
                    if (parts.Length > 2)
                    {
                        return Path.GetFileName(parts[2]);
                    }
                }
            }

            return string.Empty;
        }

        internal async Task<string?> GetLocalChangesDiffContent()
        {
            throw new NotImplementedException();
        }

        public bool IsDiffFileContainsChangesInMultipleFiles(string diffFile)
        {
            // Check if the diff file contains changes in more than one file
            var fileChangeMarkers = diffFile.Split(new[] { "diff --git" }, StringSplitOptions.None);
            return fileChangeMarkers.Length > 2;
        }

        public async Task<string> GetLatestCommitHashForFile(string fileName, string branchName)
        {
            // TODO zrobiæ branch fetch
            using (var repo = new Repository(localRepoPath))
            {
                var branch = repo.Branches[branchName];
                foreach (var commit in branch.Commits)
                {
                    var treeEntry = commit[fileName];
                    if (treeEntry != null)
                    {
                        return commit.Sha;
                    }
                }
            }
            return string.Empty;
        }
    }
}
