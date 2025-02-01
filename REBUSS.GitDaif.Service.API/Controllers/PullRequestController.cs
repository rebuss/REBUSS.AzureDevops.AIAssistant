using GitDaif.ServiceAPI;
using GitDaif.ServiceAPI.Agents;
using LibGit2Sharp;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using REBUSS.GitDaif.Service.API;
using REBUSS.GitDaif.Service.API.Agents;
using REBUSS.GitDaif.Service.API.DTO.Requests;
using REBUSS.GitDaif.Service.API.DTO.Responses;
using REBUSS.GitDaif.Service.API.Services;

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
        private readonly ILogger<PullRequestController> logger;

        public PullRequestController(IOptions<AppSettings> settings, ILogger<PullRequestController> logger, IConfiguration config)
        {
            gitService = new GitService(settings.Value);
            aiAgent = new BrowserCopilotForEnterprise(config);
            diffFilesDirectory = settings.Value.DiffFilesDirectory ?? throw new ArgumentNullException(nameof(diffFilesDirectory));
            localRepoPath = settings.Value.LocalRepoPath ?? throw new ArgumentNullException(nameof(localRepoPath));
            this.logger = logger;
        }

        [HttpPost("GetDiffFile")]
        public async Task<IActionResult> GetDiffFile([FromBody] PullRequestData data)
        {
            try
            {
                if(!Validation.IsPullRequestDataOk(data))
                {
                    return BadRequest("Invalid pull request data.");
                }

                using (var repo = new Repository(localRepoPath))
                {
                    var diffContent = await gitService.GetPullRequestDiffContent(data, repo);
                    return Ok(GetResponse(diffContent));
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while getting the diff file for pull request {PullRequestId}", data.Id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while processing your request.");
            }
        }

        [HttpPost("Summarize")]
        public async Task<IActionResult> Summarize([FromBody] PullRequestData data)
        {
            try
            {
                if (!Validation.IsPullRequestDataOk(data))
                {
                    return BadRequest("Invalid pull request data.");
                }

                PreProcessPullRequestData(data, "Prompts/SummarizePullRequest.txt");
                return await ProcessPullRequest(data);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while summarizing the pull request {PullRequestId}", data.Id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while processing your request.");
            }
        }

        [HttpPost("Review")]
        public async Task<IActionResult> Review([FromBody] PullRequestData data)
        {
            try
            {
                if (!Validation.IsPullRequestDataOk(data))
                {
                    return BadRequest("Invalid pull request data.");
                }

                PreProcessPullRequestData(data, "Prompts/PullRequestReview.txt");
                return await ProcessPullRequest(data);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while reviewing the pull request {PullRequestId}", data.Id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while processing your request.");
            }
        }

        [HttpPost("SummarizeLocalChanges")]
        public async Task<IActionResult> SummarizeLocalChanges([FromBody] BaseQueryData data)
        {
            try
            {
                PreProcessPullRequestData(data, "Prompts/SummarizePullRequest.txt");
                return await ProcessLocalChanges(data.Query);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while summarizing local changes.");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while processing your request.");
            }
        }

        [HttpPost("ReviewLocalChanges")]
        public async Task<IActionResult> ReviewLocalChanges([FromBody] BaseQueryData data)
        {
            try
            {
                PreProcessPullRequestData(data, "Prompts/PullRequestReview.txt");
                return await ProcessLocalChanges(data.Query);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while reviewing local changes.");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while processing your request.");
            }
        }

        [HttpPost("ReviewSingleFile")]
        public async Task<IActionResult> ReviewSingleFile([FromBody] FileReviewData data)
        {
            try
            {
                if (!Validation.IsFileReviewDataOk(data))
                {
                    return BadRequest("Invalid file review data.");
                }

                using (var repo = new Repository(localRepoPath))
                {
                    var fileName = FormatFileName(data.FilePath);
                    var diffFile = GetLatestReviewFile(fileName);
                    string diffContent = System.IO.File.Exists(diffFile) ? System.IO.File.ReadAllText(diffFile) : string.Empty;

                    if (string.IsNullOrEmpty(diffContent) || !await gitService.IsLatestCommitIncludedInDiff(data, diffContent, repo))
                    {
                        diffContent = await gitService.GetFullDiffFileFor(repo, data, fileName);
                        diffFile = await SaveDiffContentToFile(diffContent, $"{data.Id}_FileReview_{fileName}");
                    }

                    PreProcessPullRequestData(data, "Prompts/ReviewSingleFile.txt");
                    return await ReviewSingleFile(diffFile, data.Query);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while reviewing the single file {FilePath} for pull request {PullRequestId}", data.FilePath, data.Id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while processing your request.");
            }
        }

        [HttpPost("ReviewSingleLocalFile")]
        public async Task<IActionResult> ReviewSingleLocalFile([FromBody] LocalFileReviewData data)
        {
            try
            {
                if (!Validation.IsLocalFileReviewDataOk(data))
                {
                    return BadRequest("Invalid local file review data.");
                }

                using (var repo = new Repository(localRepoPath))
                {
                    var fileName = FormatFileName(data.FilePath);
                    var diffContent = await gitService.GetFullDiffFileForLocal(repo, data.FilePath);
                    var diffFile = await SaveDiffContentToFile(diffContent, $"LocalFileReview_{fileName}");
                    PreProcessPullRequestData(data, "Prompts/ReviewSingleFile.txt");
                    return await ReviewSingleFile(diffFile, data.Query);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while reviewing the single local file {FilePath}", data.FilePath);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while processing your request.");
            }
        }

        private async Task<IActionResult> ReviewSingleFile(string diffFilePath, string prompt)
        {
            var result = await aiAgent.AskAgent(prompt, diffFilePath);
            return Ok(result);
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

        private async Task<IActionResult> ProcessPullRequest(PullRequestData prData)
        {
            var diffFile = GetLatestReviewFile(prData.Id);
            string diffContent = string.Empty;
            using (var repo = new Repository(localRepoPath))
            {
                if (string.IsNullOrEmpty(diffFile) || !await gitService.IsLatestCommitIncludedInDiff(prData, diffFile, repo))
                {
                    diffContent = await gitService.GetPullRequestDiffContent(prData, repo);
                    string fileName = Path.Combine(diffFilesDirectory, $"{prData.Id}_Review_{DateTime.Now.ToString("yyyyMMddHHmmssfff")}.diff.txt");
                    await System.IO.File.WriteAllTextAsync(fileName, diffContent);
                }
            }

            var result = await aiAgent.AskAgent(prData.Query, diffFile);

            return Ok(result);
        }

        private async Task<IActionResult> ProcessLocalChanges(string prompt)
        {
            var diffContent = await gitService.GetLocalChangesDiffContent();
            string fileName = Path.Combine(diffFilesDirectory, $"LocalReview_{DateTime.Now.ToString("yyyyMMddHHmmssfff")}.diff.txt");
            await System.IO.File.WriteAllTextAsync(fileName, diffContent);
            var result = await aiAgent.AskAgent(prompt, fileName);

            return Ok(result);
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

        private BaseResponse GetResponse(string message, bool success = true)
        {
            return new BaseResponse
            {
                Success = success,
                Message = message,
                Timestamp = DateTime.Now
            };
        }

        private void PreProcessPullRequestData(BaseQueryData data, string defaultPropmptPath)
        {
            if (string.IsNullOrEmpty(data.Query))
            {
                data.Query = System.IO.File.ReadAllText(defaultPropmptPath);
            }
        }
    }
}
