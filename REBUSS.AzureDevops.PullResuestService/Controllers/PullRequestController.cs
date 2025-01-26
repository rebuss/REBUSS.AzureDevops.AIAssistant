using AzureDevOpsPullRequestAPI;
using AzureDevOpsPullRequestAPI.Agents;
using Microsoft.AspNetCore.Mvc;
using REBUSS.AzureDevOps.PullRequestAPI.Agents.Copilot;
using REBUSS.AzureDevOps.PullRequestAPI.Services;

namespace REBUSS.AzureDevOpsPullRequestAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class PullRequestController : Controller
    {
        private readonly GitService gitService;
        private readonly string diffFilesDirectory;
        private readonly InterfaceAI aiAgent;

        public PullRequestController(IConfiguration config)
        {
            gitService = new GitService(config);
            aiAgent = new BrowserCopilot(config);
            diffFilesDirectory = config[ConfigConsts.DiffFilesDirectory] ?? throw new ArgumentNullException(nameof(diffFilesDirectory));
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpGet("GetDiffFile")]
        public async Task<IActionResult> GetDiffFile(int id)
        {
            var diffContent = await gitService.GetPullRequestDiffContent(id);
            return Content(diffContent);
        }

        [HttpGet("Summarize")]
        public async Task<IActionResult> Summarize(int id)
        {
            var diffFile = GetLatestReviewFile(id);
            string diffContent = string.Empty;
            if (string.IsNullOrEmpty(diffFile) || !await gitService.IsLatestCommitIncludedInDiff(id, diffFile))
            {
                diffContent = await gitService.GetPullRequestDiffContent(id);
                string fileName = Path.Combine(diffFilesDirectory, $"{id}_Review_{DateTime.Now.ToString("yyyyMMddHHmmssfff")}.diff.txt");
                await System.IO.File.WriteAllTextAsync(fileName, diffContent);
            }

            var prompt = System.IO.File.ReadAllText("Prompts/SummarizePullRequest.txt");
            var result = await aiAgent.AskAgent(prompt, diffFile);

            return View();
        }

        [HttpGet("Review")]
        public async Task<IActionResult> Review(int id)
        {
            var diffFile = GetLatestReviewFile(id);
            string diffContent = string.Empty;
            if (string.IsNullOrEmpty(diffFile) || !await gitService.IsLatestCommitIncludedInDiff(id, diffFile))
            {
                diffContent = await gitService.GetPullRequestDiffContent(id);
                string fileName = Path.Combine(diffFilesDirectory, $"{id}_Review_{DateTime.Now.ToString("yyyyMMddHHmmssfff")}.diff.txt");
                await System.IO.File.WriteAllTextAsync(fileName, diffContent);
            }
            var prompt = System.IO.File.ReadAllText("Prompts/PullRequestReview.txt");
            var result = await aiAgent.AskAgent(prompt, diffFile);

            return View();
        }

        public async Task<IActionResult> ReviewSingleFile(int id, string fileName)
        {
            var diffFile = GetLatestReviewFile(fileName);
            string diffContent = string.Empty;
            if(string.IsNullOrEmpty(diffFile) || !await gitService.IsLatestCommitIncludedInDiff(id, diffFile))
            {
                diffContent = await gitService.GetFullDiffFileFor(id, fileName);
                string fullDiffFilePath = Path.Combine(diffFilesDirectory, $"{id}_FileReview_{fileName}_{DateTime.Now.ToString("yyyyMMddHHmmssfff")}.diff.txt");
                await System.IO.File.WriteAllTextAsync(fileName, diffContent);
            }
            
            var prompt = System.IO.File.ReadAllText("Prompts/ReviewSingleFile.txt");
            var result = await aiAgent.AskAgent(prompt, fileName);

            return View();
        }

        [HttpGet("SummarizeLocalChanges")]
        public async Task<IActionResult> SummarizeLocalChanges()
        {
            var diffContent = await gitService.GetLocalChangesDiffContent();
            string fileName = Path.Combine(diffFilesDirectory, $"LocalReview_{DateTime.Now.ToString("yyyyMMddHHmmssfff")}.diff.txt");
            await System.IO.File.WriteAllTextAsync(fileName, diffContent);
            var prompt = System.IO.File.ReadAllText("Prompts/SummarizePullRequest.txt");
            var result = await aiAgent.AskAgent(prompt, fileName);

            return View();
        }

        [HttpGet("ReviewLocalChanges")]
        public async Task<IActionResult> ReviewLocalChanges()
        {
            var diffContent = await gitService.GetLocalChangesDiffContent();
            string fileName = Path.Combine(diffFilesDirectory, $"LocalReview_{DateTime.Now.ToString("yyyyMMddHHmmssfff")}.diff.txt");
            await System.IO.File.WriteAllTextAsync(fileName, diffContent);
            var prompt = System.IO.File.ReadAllText("Prompts/PullRequestReview.txt");
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
