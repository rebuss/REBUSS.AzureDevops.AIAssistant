namespace AzureDevOpsPullRequestAPI
{
    public class ConfigConsts
    {
        public const string RepositoryNameKey = "AzureDevOps:RepositoryName";
        public const string ProjectNameKey = "AzureDevOps:ProjectName";
        public const string OrganizationNameKey = "AzureDevOps:Organization";
        public const string PersonalAccessTokenKey = "AzureDevOps:PersonalAccessToken";
        public const string LocalRepoPathKey = "AzureDevOps:LocalRepoPath";
        public const string DiffFilesDirectory = "AzureDevOps:DiffFilesDirectory";
        public const string MicrosoftAccount = MicrosoftCopilot + "AccountName";
        public const string EdgePath = MicrosoftCopilot + "MsEdgePath";
        public const string UserProfileDataDir = MicrosoftCopilot + "UserProfileDataDir";
        public const string ModalWindowName = MicrosoftCopilot + "ModalWindowName";
        private const string MicrosoftCopilot = "AzureDevOps:MicrosoftCopilot:";
    }
}
