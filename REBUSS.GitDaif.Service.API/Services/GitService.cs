using GitDaif.ServiceAPI;
using LibGit2Sharp;
using Microsoft.Extensions.Options;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using REBUSS.GitDaif.Service.API.DTO.Requests;
using REBUSS.GitDaif.Service.API.Services.Model;
using System.Text;

namespace REBUSS.GitDaif.Service.API.Services
{
    public class GitService
    {
        private readonly string personalAccessToken;
        private readonly string localRepoPath;
        private readonly ILogger<GitService> logger;

        [ActivatorUtilitiesConstructor]
        public GitService(IOptions<AppSettings> settings, ILogger<GitService> logger) : this(settings.Value)
        {
            this.logger = logger;
        }

        public GitService(AppSettings settings) : this(settings, (IGitClient)null)
        {
        }

        public GitService(AppSettings settings, IGitClient gitClient)
        {
            if (settings is null) throw new ArgumentNullException(nameof(settings));
            personalAccessToken = settings.PersonalAccessToken ?? throw new ArgumentNullException(nameof(personalAccessToken));
            localRepoPath = settings.LocalRepoPath ?? throw new ArgumentNullException(nameof(localRepoPath));
            GitClient = gitClient ?? new GitClient(personalAccessToken);
        }

        public IGitClient GitClient { get; set; }

        public async Task<string> GetDiffContentForChanges(GitPullRequestIterationChanges changes, GitPullRequest pullRequest)
        {
            try
            {
                StringBuilder diffContent = new StringBuilder();
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

                return diffContent.ToString();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while getting diff content for changes.");
                throw;
            }
        }

        public string ExtractModifiedFileName(string diffContent)
        {
            try
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
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while extracting modified file name from diff content.");
                throw;
            }
        }

        public async Task<string> GetFullDiffFileFor(IRepository repo, PullRequestData prData, string fileName)
        {
            try
            {
                var pullRequest = await GitClient.GetPullRequestAsync(prData);
                var branchNames = new[]
                {
                    ExtractBranchNameFromRef(pullRequest.TargetRefName),
                    ExtractBranchNameFromRef(pullRequest.SourceRefName)
                };

                var commit1 = await GetLatestCommitHashForFile(fileName, branchNames[0], repo);
                var commit2 = await GetLatestCommitHashForFile(fileName, branchNames[1], repo);
                return await GetGitDiffAsync(commit1, commit2, fileName, true);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"An error occurred while getting full diff file for pull request {prData.Id} and file {fileName}.");
                throw;
            }
        }

        public async Task<string> GetFullDiffFileForLocal(IRepository repo, string filePath)
        {
            try
            {
                var headCommit = repo.Head.Tip;
                var emptyTree = repo.ObjectDatabase.CreateTree(new TreeDefinition());
                var diffOptions = new CompareOptions
                {
                    ContextLines = 999999
                };
                var changes = repo.Diff.Compare<Patch>(emptyTree, headCommit.Tree, new[] { PrepareFilePath(filePath) }, diffOptions);
                return await Task.FromResult(changes.Content);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"An error occurred while getting full diff file for local file {filePath}.");
                throw;
            }
        }

        public async Task<string> GetLatestCommitHashForFile(string filePath, string branchName, IRepository repo)
        {
            try
            {
                var branch = repo.Branches[$"refs/remotes/origin/{branchName}"];
                if (branch == null)
                {
                    return string.Empty;
                }

                foreach (var commit in branch.Commits)
                {
                    var treeEntry = commit[PrepareFilePath(filePath)];
                    if (treeEntry != null)
                    {
                        return commit.Sha;
                    }
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"An error occurred while getting latest commit hash for file {filePath} in branch {branchName}.");
                throw;
            }
        }

        public async Task<string?> GetLocalChangesDiffContent()
        {
            try
            {
                using (var repo = new Repository(localRepoPath))
                {
                    var changes = repo.Diff.Compare<Patch>(repo.Head.Tip.Tree, DiffTargets.Index);
                    return await Task.FromResult(changes.Content);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while getting local changes diff content.");
                throw;
            }
        }

        public async Task<string> GetPullRequestDiffContent(PullRequestData prData, IRepository repo)
        {
            try
            {
                var pullRequest = await GitClient.GetPullRequestAsync(prData);
                var lastIteration = await GitClient.GetLastIterationAsync(prData);
                var changes = await GitClient.GetIterationChangesAsync(prData, lastIteration.Id.Value);

                var branchNames = new[]
                {
                    ExtractBranchNameFromRef(pullRequest.TargetRefName),
                    ExtractBranchNameFromRef(pullRequest.SourceRefName)
                };

                return await GetDiffContentForChanges(changes, pullRequest);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"An error occurred while getting diff content for pull request {prData.Id}.");
                throw;
            }
        }

        public bool IsDiffFileContainsChangesInMultipleFiles(string diffFile)
        {
            try
            {
                var fileChangeMarkers = diffFile.Split(new[] { "diff --git" }, StringSplitOptions.None);
                return fileChangeMarkers.Length > 2;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while checking if diff file contains changes in multiple files.");
                throw;
            }
        }

        public async Task<bool> IsLatestCommitIncludedInDiff(string branchName, string diffContent, IRepository repo)
        {
            try
            {
                var latestCommitHash = await GetLatestCommitHash(branchName, repo);
                return !string.IsNullOrEmpty(latestCommitHash) && diffContent.Contains(latestCommitHash);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"An error occurred while checking if the latest commit is included in diff for branch {branchName}.");
                throw;
            }
        }

        public async Task<bool> IsLatestCommitIncludedInDiff(PullRequestData prData, string diffContent, IRepository repo)
        {
            try
            {
                var branchName = await GetBranchNameForPullRequest(prData);
                string latestCommitHash = IsDiffFileContainsChangesInMultipleFiles(diffContent)
                    ? await GetLatestCommitHash(branchName, repo)
                    : await GetLatestCommitHashForFile(ExtractModifiedFileName(diffContent), branchName, repo);

                return !string.IsNullOrEmpty(latestCommitHash) && diffContent.Contains(latestCommitHash);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"An error occurred while checking if the latest commit is included in diff for pull request {prData.Id}.");
                throw;
            }
        }

        internal string ExtractBranchNameFromRef(string refName)
        {
            try
            {
                return refName?.Replace("refs/heads/", string.Empty);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"An error occurred while extracting branch name from ref {refName}.");
                throw;
            }
        }

        internal async Task<string> GetBranchNameForPullRequest(PullRequestData prData)
        {
            try
            {
                var pullRequest = await GitClient.GetPullRequestAsync(prData);
                if (pullRequest == null)
                {
                    return null;
                }

                return ExtractBranchNameFromRef(pullRequest.SourceRefName);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"An error occurred while getting branch name for pull request {prData.Id}.");
                throw;
            }
        }

        internal async Task<string> GetGitDiffAsync(string remoteCommitId, string localCommitId, string filePath, bool full = false)
        {
            try
            {
                using (var repo = new Repository(localRepoPath))
                {
                    var remoteCommit = repo.Lookup<Commit>(remoteCommitId);
                    var localCommit = repo.Lookup<Commit>(localCommitId);

                    if (remoteCommit == null || localCommit == null)
                    {
                        throw new ArgumentException("Invalid commit ID(s) provided.");
                    }

                    var diffOptions = new CompareOptions
                    {
                        IncludeUnmodified = false,
                        ContextLines = full ? 999999 : 3
                    };

                    var changes = repo.Diff.Compare<Patch>(remoteCommit.Tree, localCommit.Tree, new[] { PrepareFilePath(filePath) }, diffOptions);
                    return await Task.FromResult(changes.Content);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"An error occurred while getting git diff for file {filePath} between commits {remoteCommitId} and {localCommitId}.");
                throw;
            }
        }

        internal async Task<string> GetLatestCommitHash(string branchName, IRepository repo)
        {
            try
            {
                var branch = repo?.Branches[branchName];
                if (branch == null)
                {
                    return string.Empty;
                }

                var latestCommit = branch.Commits.First();
                return latestCommit.Sha;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"An error occurred while getting latest commit hash for branch {branchName}.");
                throw;
            }
        }

        internal string PrepareFilePath(string filePath)
        {
            try
            {
                if (filePath == null)
                {
                    return string.Empty;
                }

                if (filePath.StartsWith("/"))
                {
                    return filePath.Substring(1);
                }

                return filePath;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"An error occurred while preparing file path {filePath}.");
                throw;
            }
        }
    }
}
