using AzureDevOpsPullRequestAPI;
using LibGit2Sharp;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using REBUSS.AzureDevOps.PullRequestAPI.Git.Model;
using System.Diagnostics;
using System.Text;

namespace REBUSS.AzureDevOps.PullRequestAPI.Services
{
    public class GitService
    {
        private readonly string personalAccessToken;
        private readonly string organization;
        private readonly string projectName;
        private readonly string repo;
        private readonly string localRepoPath;

        public GitService(IConfiguration configuration, IGitClient gitClient = null)
        {
            if (configuration is null) throw new ArgumentNullException(nameof(configuration));
            personalAccessToken = configuration[ConfigConsts.PersonalAccessTokenKey] ?? throw new ArgumentNullException(nameof(personalAccessToken));
            organization = configuration[ConfigConsts.OrganizationNameKey] ?? throw new ArgumentNullException(nameof(organization));
            projectName = configuration[ConfigConsts.ProjectNameKey] ?? throw new ArgumentNullException(nameof(projectName));
            repo = configuration[ConfigConsts.RepositoryNameKey] ?? throw new ArgumentNullException(nameof(repo));
            localRepoPath = configuration[ConfigConsts.LocalRepoPathKey] ?? throw new ArgumentNullException(nameof(localRepoPath));
            GitClient = gitClient ?? new GitClient(organization, repo, projectName, personalAccessToken);
        }

        public IGitClient GitClient { get; set; }

        public async Task AppendDiffContentForChanges(GitPullRequestIterationChanges changes, GitPullRequest pullRequest, StringBuilder diffContent)
        {
            foreach (var change in changes.ChangeEntries)
            {
                if (change.Item is GitItem gitItem)
                {
                    string localCommitId = pullRequest.LastMergeSourceCommit.CommitId;
                    string remoteCommitId = pullRequest.LastMergeTargetCommit.CommitId;
                    string filePath = gitItem.Path;

                    string result = await GetGitDiffAsync(remoteCommitId, localCommitId, filePath);
                    diffContent.Append(result);
                }
            }
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

        public async Task<string> GetFullDiffFileFor(int pullRequestId, string fileName)
        {
            string branchName = await GetBranchNameForPullRequest(pullRequestId);
            return await GetGitDiffAsync(branchName, fileName);
        }

        public async Task<string> GetLatestCommitHashForFile(string fileName, string branchName, IRepository repo)
        {
            var branch = repo.Branches[branchName];
            if(branch == null)
            {
                return string.Empty;
            }

            foreach (var commit in branch.Commits)
            {
                var treeEntry = commit[fileName];
                if (treeEntry != null)
                {
                    return commit.Sha;
                }
            }

            return string.Empty;
        }

        public async Task<string?> GetLocalChangesDiffContent()
        {
            throw new NotImplementedException();
        }

        public async Task<string> GetPullRequestDiffContent(int pullRequestId, IRepository repo)
        {
            var pullRequest = await GitClient.GetPullRequestAsync(pullRequestId);
            var lastIteration = await GitClient.GetLastIterationAsync(pullRequestId);
            var changes = await GitClient.GetIterationChangesAsync(pullRequestId, lastIteration.Id.Value);
            var diffContent = new StringBuilder();

            var branchNames = new[]  
            {   
                ExtractBranchNameFromRef(pullRequest.TargetRefName),
                ExtractBranchNameFromRef(pullRequest.SourceRefName) 
            };
            
            GitClient.FetchBranches(repo, pullRequest, branchNames);
            await AppendDiffContentForChanges(changes, pullRequest, diffContent);

            return diffContent.ToString();
        }

        public bool IsDiffFileContainsChangesInMultipleFiles(string diffFile)
        {
            var fileChangeMarkers = diffFile.Split(new[] { "diff --git" }, StringSplitOptions.None);
            return fileChangeMarkers.Length > 2;
        }

        public async Task<bool> IsLatestCommitIncludedInDiff(string branchName, string diffContent, IRepository repo)
        {
            var latestCommitHash = await GetLatestCommitHash(branchName, repo);
            return diffContent.Contains(latestCommitHash);
        }

        public async Task<bool> IsLatestCommitIncludedInDiff(int id, string diffFile, IRepository repo)
        {
            var diffContent = File.ReadAllText(diffFile);
            var branchName = await GetBranchNameForPullRequest(id);
            string latestCommitHash = IsDiffFileContainsChangesInMultipleFiles(diffContent)
                ? await GetLatestCommitHash(branchName, repo)
                : await GetLatestCommitHashForFile(ExtractModifiedFileName(diffContent), branchName, repo);

            return diffFile.Contains(latestCommitHash);
        }

        internal string ExtractBranchNameFromRef(string refName)
        {
            return refName?.Replace("refs/heads/", string.Empty);
        }

        internal async Task<string> GetBranchNameForPullRequest(int pullRequestId)
        {
            var pullRequest = await GitClient.GetPullRequestAsync(pullRequestId);
            return ExtractBranchNameFromRef(pullRequest.SourceRefName);
        }

        internal async Task<string> GetGitDiffAsync(string remoteCommitId, string localCommitId, string filePath)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = $"diff {remoteCommitId} {localCommitId} -- {filePath}",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = localRepoPath
            };

            using (var process = Process.Start(startInfo))
            {
                using (var reader = process.StandardOutput)
                {
                    return await reader.ReadToEndAsync();
                }
            }
        }

        internal async Task<string> GetGitDiffAsync(string branchName, string fileName)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = $"diff {branchName} -- {fileName} --unified=999999",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = localRepoPath
            };

            using (var process = Process.Start(startInfo))
            {
                using (var reader = process.StandardOutput)
                {
                    return await reader.ReadToEndAsync();
                }
            }
        }

        internal async Task<string> GetLatestCommitHash(string branchName, IRepository repo)
        {
            var branch = repo.Branches[branchName];
            var latestCommit = branch.Commits.First();
            return latestCommit.Sha;
        }
    }
}