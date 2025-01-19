using LibGit2Sharp;
using Microsoft.AspNetCore.Mvc;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using REBUSS.AzureDevOpsPullRequestAPI.Services;
using System.Diagnostics;
using System.Text;

namespace REBUSS.AzureDevOpsPullRequestAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class PullRequestController : Controller
    {
        private readonly GitService gitService;

        public PullRequestController(IConfiguration config)
        {
            gitService = new GitService(config);
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
        public IActionResult Summarize(int id)
        {
            // Implementation goes here
            return View();
        }

        [HttpGet("Review")]
        public IActionResult Review(int id)
        {
            // Implementation goes here
            return View();
        }

        [HttpGet("ReviewAndApplyOnAzure")]
        public IActionResult ReviewAndApplyOnAzure(int id)
        {
            // Implementation goes here
            return View();
        }
    }
}
