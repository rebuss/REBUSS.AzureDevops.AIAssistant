using GitDaif.ServiceAPI;
using GitDaif.ServiceAPI.Agents;
using LibGit2Sharp;
using Microsoft.AspNetCore.Mvc;
using REBUSS.GitDaif.Service.API.Agents;
using REBUSS.GitDaif.Service.API.Git;

namespace REBUSS.GitDaif.Service.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class PullRequestController : Controller
    {
        private readonly GitService gitService;
        private readonly string diffFilesDirectory;
        private readonly string localRepoPath;
        private readonly InterfaceAI aiAgent;

        public PullRequestController(IConfiguration config)
        {
            gitService = new GitService(config);
            aiAgent = new BrowserCopilotForEnterprise(config);
            diffFilesDirectory = config[ConfigConsts.DiffFilesDirectory] ?? throw new ArgumentNullException(nameof(diffFilesDirectory));
            localRepoPath = config[ConfigConsts.LocalRepoPathKey] ?? throw new ArgumentNullException(nameof(localRepoPath));
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpGet("GetDiffFile")]
        public async Task<IActionResult> GetDiffFile(int id)
        {
            using (var repo = new Repository(localRepoPath))
            {
                var diffContent = await gitService.GetPullRequestDiffContent(id, repo);
                return Content(diffContent);
            }
        }

        [HttpGet("Summarize")]
        public async Task<IActionResult> Summarize(int id)
        {
            return await ProcessPullRequest(id, "Prompts/SummarizePullRequest.txt");
        }

        [HttpGet("Review")]
        public async Task<IActionResult> Review(int id)
        {
            return await ProcessPullRequest(id, "Prompts/PullRequestReview.txt");
        }

        [HttpGet("SummarizeLocalChanges")]
        public async Task<IActionResult> SummarizeLocalChanges()
        {
            return await ProcessLocalChanges("Prompts/SummarizePullRequest.txt");
        }

        [HttpGet("ReviewLocalChanges")]
        public async Task<IActionResult> ReviewLocalChanges()
        {
            return await ProcessLocalChanges("Prompts/PullRequestReview.txt");
        }

        [HttpGet("ReviewSingleFile")]
        public async Task<IActionResult> ReviewSingleFile(int id, string filePath)
        {
            using (var repo = new Repository(localRepoPath))
            {
                var fileName = FormatFileName(filePath);
                var diffFile = GetLatestReviewFile(fileName);
                string diffContent = System.IO.File.Exists(diffFile) ? System.IO.File.ReadAllText(diffFile) : string.Empty;
                
                if (string.IsNullOrEmpty(diffContent) || !await gitService.IsLatestCommitIncludedInDiff(id, diffContent, repo))
                {
                    diffContent = await gitService.GetFullDiffFileFor(repo, id, fileName);
                    diffFile = await SaveDiffContentToFile(diffContent, $"{id}_FileReview_{fileName}");
                }

                return await ReviewSingleFile(diffFile);
            }
        }

        [HttpGet("ReviewSingleLocalFile")]
        public async Task<IActionResult> ReviewSingleLocalFile(string filePath)
        {
            using (var repo = new Repository(localRepoPath))
            {
                var fileName = FormatFileName(filePath);
                var diffContent = await gitService.GetFullDiffFileForLocal(repo, filePath);
                var diffFile = await SaveDiffContentToFile(diffContent, $"LocalFileReview_{fileName}");
                return await ReviewSingleFile(diffFile);
            }
        }

        private async Task<IActionResult> ReviewSingleFile(string diffFilePath)
        {
            var prompt = System.IO.File.ReadAllText("Prompts/ReviewSingleFile.txt");
            var result = await aiAgent.AskAgent(prompt, diffFilePath);
            return View();
        }

        private string FormatFileName(string filePath)
        {
            return filePath.Replace('\\', '-').Replace('/', '-');
        }

        private async Task<string> SaveDiffContentToFile(string diffContent, string fileName)
        {
            string fullDiffFilePath = Path.Combine(diffFilesDirectory, $"{fileName}_{DateTime.Now.ToString("yyyyMMddHHmmssfff")}.diff.txt");
            await System.IO.File.WriteAllTextAsync(fullDiffFilePath, diffContent);
            return fullDiffFilePath;
        }

        private async Task<IActionResult> ProcessPullRequest(int id, string promptFilePath)
        {
            var diffFile = GetLatestReviewFile(id);
            string diffContent = string.Empty;
            using (var repo = new Repository(localRepoPath))
            {
                if (string.IsNullOrEmpty(diffFile) || !await gitService.IsLatestCommitIncludedInDiff(id, diffFile, repo))
                {
                    diffContent = await gitService.GetPullRequestDiffContent(id, repo);
                    string fileName = Path.Combine(diffFilesDirectory, $"{id}_Review_{DateTime.Now.ToString("yyyyMMddHHmmssfff")}.diff.txt");
                    await System.IO.File.WriteAllTextAsync(fileName, diffContent);
                }
            }

            var prompt = System.IO.File.ReadAllText(promptFilePath);
            var result = await aiAgent.AskAgent(prompt, diffFile);

            return View();
        }

        private async Task<IActionResult> ProcessLocalChanges(string promptFilePath)
        {
            var diffContent = await gitService.GetLocalChangesDiffContent();
            string fileName = Path.Combine(diffFilesDirectory, $"LocalReview_{DateTime.Now.ToString("yyyyMMddHHmmssfff")}.diff.txt");
            await System.IO.File.WriteAllTextAsync(fileName, diffContent);
            var prompt = System.IO.File.ReadAllText(promptFilePath);
            var result = await aiAgent.AskAgent(prompt, fileName);

            return View();
        }

        private string GetLatestReviewFile(int id)
        {
            var directoryInfo = new DirectoryInfo(diffFilesDirectory);
            var latestFile = directoryInfo.GetFiles($"{id}_Review*")
                                          .OrderByDescending(f => f.LastWriteTime)
                                          .FirstOrDefault();
            return latestFile?.FullName;
        }

        private string GetLatestReviewFile(string fileName)
        {
            var directoryInfo = new DirectoryInfo(diffFilesDirectory);
            var latestFile = directoryInfo.GetFiles($"*{fileName}*")
                                          .OrderByDescending(f => f.LastWriteTime)
                                          .FirstOrDefault();
            return latestFile?.FullName;
        }
    }
}
