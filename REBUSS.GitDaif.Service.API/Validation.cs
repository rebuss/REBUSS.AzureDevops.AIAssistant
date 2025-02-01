using REBUSS.GitDaif.Service.API.DTO.Requests;

namespace REBUSS.GitDaif.Service.API
{
    public class Validation
    {
        public static bool IsPullRequestDataOk(PullRequestData data)
        {
            return !string.IsNullOrEmpty(data.OrganizationName) &&
                   !string.IsNullOrEmpty(data.RepositoryName) &&
                   !string.IsNullOrEmpty(data.ProjectName) &&
                   data.Id > 0;
        }
        public static bool IsFileReviewDataOk(FileReviewData data)
        {
            return IsPullRequestDataOk(data) &&
                   !string.IsNullOrEmpty(data.FilePath);
        }

        public static bool IsLocalFileReviewDataOk(LocalFileReviewData data)
        {
            return !string.IsNullOrEmpty(data.FilePath) && File.Exists(data.FilePath);
        }
    }
}
