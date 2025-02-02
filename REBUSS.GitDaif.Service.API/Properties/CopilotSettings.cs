namespace REBUSS.GitDaif.Service.API.Properties
{
    public class CopilotSettings
    {
        private string accountName;

        public string ModalWindowName { get; set; }
        public string UserProfileDataDir { get; set; }
        public string MsEdgePath { get; set; }
        public string AccountName 
        { 
            get => accountName.Replace("@", "%40"); 
            set => accountName = value; 
        }
    }
}
