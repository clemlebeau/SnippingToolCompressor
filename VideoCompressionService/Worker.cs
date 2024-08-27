using System;
using System.Diagnostics;
using System.IO;

namespace VideoCompressionService
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;

        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;
            this.RecordingsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), Constants.ScreenRecordingsDirectory);
            if (!Path.Exists(this.RecordingsDirectory))
            {
                throw new FileNotFoundException(this.RecordingsDirectory);
            }

            this.OutputDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), $"{Path.GetDirectoryName(this.RecordingsDirectory)}{Constants.OutputDirectorySuffix}");

            if (!Path.Exists(this.OutputDirectory))
            {
                Directory.CreateDirectory(this.OutputDirectory);
            }

            this.Watcher = new FileSystemWatcher(this.RecordingsDirectory);
        }

        private string RecordingsDirectory { get; set; }
        private string OutputDirectory { get; set; }
        private FileSystemWatcher Watcher { get; set; }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            this.StartWatcher();
            while (!stoppingToken.IsCancellationRequested)
            {
            }
        }

        private void StartWatcher()
        {
            this.Watcher.Created += this.OnCreatedFile;
            this.Watcher.EnableRaisingEvents = true;
            _logger.LogInformation($"Started FileSystemWatcher for directory {this.Watcher.Path}");
        }

        private void OnCreatedFile(object sender, FileSystemEventArgs e)
        {
            _logger.LogInformation($"File creation detected: {e.FullPath}");

            bool isExtensionValid = Path.GetExtension(e.FullPath) == ".mp4";
            if (isExtensionValid)
            {
                string outputPath = Path.Combine(this.OutputDirectory, e.Name);
                ProcessStartInfo processStartInfo = new ProcessStartInfo();
                processStartInfo.FileName = Constants.FfmpegExecutablePath;
                processStartInfo.Arguments = String.Format(Constants.CompressionCommandArguments, e.FullPath, outputPath);
                _logger.LogInformation($"Beginning compression of {e.Name} at {DateTimeOffset.Now}");
                using Process process = Process.Start(processStartInfo);
                if (process is null)
                {
                    _logger.LogError($"Couldn't start {Constants.FfmpegExecutablePath}.");
                    return;
                }

                process.WaitForExit();
                if (process.ExitCode != 0)
                {
                    _logger.LogError($"Failed to compress {e.FullPath}.");
                    return;
                }
                else
                {
                    _logger.LogInformation("Compression succeeded. Deleting larger video file.");
                    File.Delete(e.FullPath);
                }
            }
            else
            {
                _logger.LogInformation("Not .mp4 file, skipping compression.");
            }
        }

        private static class Constants
        {
            public const string ScreenRecordingsDirectory = "Screen Recordings\\";
            public const string OutputDirectorySuffix = " Compressed";
            public const string FfmpegExecutablePath = "\"C:\\Program Files\\ffmpeg\\ffmpeg.exe\"";
            public const string CompressionCommandArguments = "-loglevel error -hide_banner -nostats -i \"{0}\" -c:v libx264 -b:v 1.2M -c:a aac -b:a 64k \"{1}\"";
        }
    }
}
