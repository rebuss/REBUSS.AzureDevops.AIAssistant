using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace REBUSS.GitDaif.Service.API
{
    public class DiffFileCleanerService
    {
        private readonly string diffFilesDirectory;
        private Timer timer;
        private ILogger<DiffFileCleanerService> logger;

        public DiffFileCleanerService(string diffFilesDirectory, ILogger<DiffFileCleanerService> logger)
        {
            this.diffFilesDirectory = diffFilesDirectory ?? throw new ArgumentNullException(nameof(diffFilesDirectory));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            Start();
        }

        public void Start()
        {
            // Perform initial cleanup
            CleanDiffFiles();

            // Set up a timer to run the cleanup every 24 hours
            timer = new Timer(CleanDiffFiles, null, TimeSpan.FromDays(1), TimeSpan.FromDays(1));
        }

        private void CleanDiffFiles(object state = null)
        {
            try
            {
                var directoryInfo = new DirectoryInfo(diffFilesDirectory);
                var diffFiles = directoryInfo.GetFiles("*.diff.txt");

                foreach (var file in diffFiles)
                {
                    if (file.CreationTime < DateTime.Now.Date)
                    {
                        file.Delete();
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError($"An error occurred while cleaning diff files: {ex.Message}");
            }
        }
    }
}
