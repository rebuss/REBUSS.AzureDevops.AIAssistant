namespace REBUSS.GitDaif.Service.API.DTO
{
    public class PullRequestData : BaseQueryData
    {
        public string OrganizationName { get; set; }

        public string RepositoryName { get; set; }

        public string ProjectName { get; set; }

        public int PullRequestId { get; set; }
    }
}
