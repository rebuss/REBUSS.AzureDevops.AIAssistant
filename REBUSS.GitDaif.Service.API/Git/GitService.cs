using GitDaif.ServiceAPI;
using LibGit2Sharp;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using REBUSS.GitDaif.Service.API.Git.Model;
using System.Text;

namespace REBUSS.GitDaif.Service.API.Git
{
    public class GitService
    {
        private readonly string personalAccessToken;
        private readonly string organization;
        private readonly string projectName;
        private readonly string repo;
        private readonly string localRepoPath;
        private readonly ILogger<GitService> logger;

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

        [ActivatorUtilitiesConstructor]
        public GitService(IConfiguration configuration, ILogger<GitService> logger) : this(configuration)
        {
            this.logger = logger;
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

        public async Task<string> GetFullDiffFileFor(IRepository repo, int pullRequestId, string fileName)
        {
            try
            {
                var pullRequest = await GitClient.GetPullRequestAsync(pullRequestId);
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
                logger.LogError(ex, "An error occurred while getting full diff file for pull request {PullRequestId} and file {FileName}.", pullRequestId, fileName);
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
                logger.LogError(ex, "An error occurred while getting full diff file for local file {FilePath}.", filePath);
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
                logger.LogError(ex, "An error occurred while getting latest commit hash for file {FilePath} in branch {BranchName}.", filePath, branchName);
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

        public async Task<string> GetPullRequestDiffContent(int pullRequestId, IRepository repo)
        {
            try
            {
                var pullRequest = await GitClient.GetPullRequestAsync(pullRequestId);
                var lastIteration = await GitClient.GetLastIterationAsync(pullRequestId);
                var changes = await GitClient.GetIterationChangesAsync(pullRequestId, lastIteration.Id.Value);

                var branchNames = new[]
                {
                    ExtractBranchNameFromRef(pullRequest.TargetRefName),
                    ExtractBranchNameFromRef(pullRequest.SourceRefName)
                };

                return await GetDiffContentForChanges(changes, pullRequest);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while getting diff content for pull request {PullRequestId}.", pullRequestId);
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
                logger.LogError(ex, "An error occurred while checking if the latest commit is included in diff for branch {BranchName}.", branchName);
                throw;
            }
        }

        public async Task<bool> IsLatestCommitIncludedInDiff(int id, string diffContent, IRepository repo)
        {
            try
            {
                var branchName = await GetBranchNameForPullRequest(id);
                string latestCommitHash = IsDiffFileContainsChangesInMultipleFiles(diffContent)
                    ? await GetLatestCommitHash(branchName, repo)
                    : await GetLatestCommitHashForFile(ExtractModifiedFileName(diffContent), branchName, repo);

                return !string.IsNullOrEmpty(latestCommitHash) && diffContent.Contains(latestCommitHash);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while checking if the latest commit is included in diff for pull request {PullRequestId}.", id);
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
                logger.LogError(ex, "An error occurred while extracting branch name from ref {RefName}.", refName);
                throw;
            }
        }

        internal async Task<string> GetBranchNameForPullRequest(int pullRequestId)
        {
            try
            {
                var pullRequest = await GitClient.GetPullRequestAsync(pullRequestId);
                if (pullRequest == null)
                {
                    return null;
                }

                return ExtractBranchNameFromRef(pullRequest.SourceRefName);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while getting branch name for pull request {PullRequestId}.", pullRequestId);
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
                logger.LogError(ex, "An error occurred while getting git diff for file {FilePath} between commits {RemoteCommitId} and {LocalCommitId}.", filePath, remoteCommitId, localCommitId);
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
                logger.LogError(ex, "An error occurred while getting latest commit hash for branch {BranchName}.", branchName);
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
                logger.LogError(ex, "An error occurred while preparing file path {FilePath}.", filePath);
                throw;
            }
        }
    }
}
