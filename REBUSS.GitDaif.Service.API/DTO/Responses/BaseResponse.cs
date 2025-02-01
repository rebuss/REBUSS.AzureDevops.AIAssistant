namespace REBUSS.GitDaif.Service.API.DTO.Responses
{
    public class BaseResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
