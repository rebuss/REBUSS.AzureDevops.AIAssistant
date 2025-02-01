using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace REBUSS.GitDaif.Service.API.Services
{
    public class DiffFileCleanerBackgroundService : BackgroundService
    {
        private readonly string diffFilesDirectory;
        private readonly ILogger<DiffFileCleanerBackgroundService> logger;

        public DiffFileCleanerBackgroundService(string diffFilesDirectory, ILogger<DiffFileCleanerBackgroundService> logger)
        {
            this.diffFilesDirectory = diffFilesDirectory ?? throw new ArgumentNullException(nameof(diffFilesDirectory));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            logger.LogInformation("DiffFileCleanerBackgroundService started.");
            // Perform initial cleanup
            CleanDiffFiles();

            // Set up a timer to run the cleanup every 24 hours
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromDays(1), stoppingToken);
                CleanDiffFiles();
            }
        }

        private void CleanDiffFiles()
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
                        logger.LogInformation($"Deleted old diff file: {file.Name}");
                    }
                }
            }
            catch (Exception ex)
            {
                // Log the exception
                logger.LogError(ex, "An error occurred while cleaning diff files.");
            }
        }
    }
}