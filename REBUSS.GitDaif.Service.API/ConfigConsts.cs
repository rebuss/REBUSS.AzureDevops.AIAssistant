namespace GitDaif.ServiceAPI
{
    public class ConfigConsts
    {
        public const string PersonalAccessTokenKey = "GitDaif.Service:PersonalAccessToken";
        public const string LocalRepoPathKey = "GitDaif.Service:LocalRepoPath";
        public const string DiffFilesDirectory = "GitDaif.Service:DiffFilesDirectory";
        public const string MicrosoftAccount = MicrosoftCopilot + "AccountName";
        public const string EdgePath = MicrosoftCopilot + "MsEdgePath";
        public const string UserProfileDataDir = MicrosoftCopilot + "UserProfileDataDir";
        public const string ModalWindowName = MicrosoftCopilot + "ModalWindowName";
        private const string MicrosoftCopilot = "GitDaif.Service:MicrosoftCopilot:";
    }
}
