using Newtonsoft.Json.Linq;

namespace REBUSS.GitDaif.Service.API.Properties
{
    public class AppSettings
    {
        private string diffFilesDirectory;

        public string DiffFilesDirectory
        {
            get => string.IsNullOrWhiteSpace(diffFilesDirectory) ? diffFilesDirectory = Path.GetTempPath() : diffFilesDirectory;
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    diffFilesDirectory = Path.GetTempPath();
                }
                else
                {
                    diffFilesDirectory = value;
                }
            }
        }

        public string LocalRepoPath { get; set; }

        public string PersonalAccessToken { get; set; }

        public string AIAgent { get; set; }
    }
}