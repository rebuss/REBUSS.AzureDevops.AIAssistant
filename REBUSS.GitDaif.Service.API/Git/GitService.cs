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

        public async Task<string> GetDiffContentForChanges(GitPullRequestIterationChanges changes, GitPullRequest pullRequest)
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

        public async Task<string> GetFullDiffFileFor(IRepository repo, int pullRequestId, string fileName)
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

        public async Task<string> GetFullDiffFileForLocal(IRepository repo, string filePath)
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

        public async Task<string> GetLatestCommitHashForFile(string filePath, string branchName, IRepository repo)
        {
            var branch = repo.Branches[$"refs/remotes/origin/{branchName}"];
            if(branch == null)
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

        public async Task<string?> GetLocalChangesDiffContent()
        {
            using (var repo = new Repository(localRepoPath))
            {
                var changes = repo.Diff.Compare<Patch>(repo.Head.Tip.Tree, DiffTargets.Index);
                return await Task.FromResult(changes.Content);
            }
        }

        public async Task<string> GetPullRequestDiffContent(int pullRequestId, IRepository repo)
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

        public bool IsDiffFileContainsChangesInMultipleFiles(string diffFile)
        {
            var fileChangeMarkers = diffFile.Split(new[] { "diff --git" }, StringSplitOptions.None);
            return fileChangeMarkers.Length > 2;
        }

        public async Task<bool> IsLatestCommitIncludedInDiff(string branchName, string diffContent, IRepository repo)
        {
            var latestCommitHash = await GetLatestCommitHash(branchName, repo);
            return !string.IsNullOrEmpty(latestCommitHash) && diffContent.Contains(latestCommitHash);
        }

        public async Task<bool> IsLatestCommitIncludedInDiff(int id, string diffContent, IRepository repo)
        {
            var branchName = await GetBranchNameForPullRequest(id);
            string latestCommitHash = IsDiffFileContainsChangesInMultipleFiles(diffContent)
                ? await GetLatestCommitHash(branchName, repo)
                : await GetLatestCommitHashForFile(ExtractModifiedFileName(diffContent), branchName, repo);

            return !string.IsNullOrEmpty(latestCommitHash) && diffContent.Contains(latestCommitHash);
        }

        internal string ExtractBranchNameFromRef(string refName)
        {
            return refName?.Replace("refs/heads/", string.Empty);
        }

        internal async Task<string> GetBranchNameForPullRequest(int pullRequestId)
        {
            var pullRequest = await GitClient.GetPullRequestAsync(pullRequestId);
            if(pullRequest == null)
            {
                return null;
            }

            return ExtractBranchNameFromRef(pullRequest.SourceRefName);
        }

        internal async Task<string> GetGitDiffAsync(string remoteCommitId, string localCommitId, string filePath, bool full = false)
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

        internal async Task<string> GetLatestCommitHash(string branchName, IRepository repo)
        {
            var branch = repo?.Branches[branchName];
            if (branch == null)
            {
                return string.Empty;
            }

            var latestCommit = branch.Commits.First();
            return latestCommit.Sha;
        }

        internal string PrepareFilePath(string filePath)
        {
            if(filePath == null)
            {
                return string.Empty;
            }

            if (filePath.StartsWith("/"))
            {
                return filePath.Substring(1);
            }

            return filePath;
        }
    }
}