namespace GitDaif.ServiceAPI
{
    public class ConfigConsts
    {
        public const string PersonalAccessTokenKey = "PersonalAccessToken";
        public const string LocalRepoPathKey = "LocalRepoPath";
        public const string DiffFilesDirectory = "DiffFilesDirectory";
        public const string MicrosoftAccount = MicrosoftCopilot + "AccountName";
        public const string EdgePath = MicrosoftCopilot + "MsEdgePath";
        public const string UserProfileDataDir = MicrosoftCopilot + "UserProfileDataDir";
        public const string ModalWindowName = MicrosoftCopilot + "ModalWindowName";
        private const string MicrosoftCopilot = "GitDaif.Service:MicrosoftCopilot:";
    }
}
