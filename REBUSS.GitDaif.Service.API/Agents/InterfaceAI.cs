using REBUSS.GitDaif.Service.API.DTO.Responses;

namespace GitDaif.ServiceAPI.Agents
{
    public interface InterfaceAI
    {
        Task<BaseResponse> AskAgent(string prompt, string filePath = null);
    }
}
