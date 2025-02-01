
using REBUSS.GitDaif.Service.API.DTO.Responses;

namespace GitDaif.ServiceAPI.Agents
{
    public class BrowserChatGPT : InterfaceAI
    {
        public Task<BaseResponse> AskAgent(string prompt, string filePath = null)
        {
            throw new NotImplementedException();
        }
    }
}
