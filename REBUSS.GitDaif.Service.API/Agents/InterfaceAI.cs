namespace GitDaif.ServiceAPI.Agents
{
    public interface InterfaceAI
    {
        Task<object> AskAgent(string prompt, string filePath = null);
    }
}
