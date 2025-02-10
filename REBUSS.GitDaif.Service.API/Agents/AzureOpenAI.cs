using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using REBUSS.GitDaif.Service.API.Agents;
using REBUSS.GitDaif.Service.API.DTO.Responses;

namespace GitDaif.ServiceAPI.Agents
{
    public class AzureOpenAI : InterfaceAI
    {
        private readonly Kernel _kernel;
        private readonly ILogger<AzureOpenAI> _logger;
        private readonly IChatCompletionService _chatCompletionService;

        public AzureOpenAI(Kernel kernel, ILogger<AzureOpenAI> logger)
        {
            _kernel = kernel;
            _logger = logger;
            _chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();
        }

        public async Task<BaseResponse> AskAgent(string prompt, string filePath = null)
        {
            string fileContent = filePath != null ? File.ReadAllText(filePath) : string.Empty;
            var history = new ChatHistory();
            history.AddSystemMessage(fileContent);
            history.AddUserMessage("Based on the above data " + prompt);

            var result = await _chatCompletionService.GetChatMessageContentAsync(history, null, _kernel);
            
            return new BaseResponse { Success = true, Message = result.Content ?? string.Empty, Timestamp = DateTime.Now };
        }
    }
}
