using Laf.CourseService.Data;
using Laf.CourseService.Models;
using Laf.CourseService.Services;
using Microsoft.EntityFrameworkCore;

namespace Laf.CourseService.BackgroundJobs;

public class VideoProcessingWorker : BackgroundService
{
    private readonly VideoProcessingQueue _queue;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<VideoProcessingWorker> _logger;

    public VideoProcessingWorker(
        VideoProcessingQueue queue,
        IServiceProvider serviceProvider,
        ILogger<VideoProcessingWorker> logger)
    {
        _queue = queue;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("VideoProcessingWorker background service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var videoId = await _queue.DequeueVideoAsync(stoppingToken);
                _logger.LogInformation("Processing video item: {VideoId}", videoId);

                await ProcessVideoAsync(videoId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("VideoProcessingWorker is stopping due to cancellation.");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in VideoProcessingWorker execution loop.");
            }
        }

        _logger.LogInformation("VideoProcessingWorker background service finished.");
    }

    private async Task ProcessVideoAsync(Guid videoId, CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
        var storageService = scope.ServiceProvider.GetRequiredService<IVideoStorageService>();
        var transcoderService = scope.ServiceProvider.GetRequiredService<IHlsTranscoderService>();

        var video = await dbContext.Videos.FirstOrDefaultAsync(v => v.Id == videoId, cancellationToken);
        if (video == null)
        {
            _logger.LogWarning("Video record {VideoId} not found in database. Skipping processing.", videoId);
            return;
        }

        video.Status = VideoProcessingStatus.Processing;
        video.ErrorMessage = null;
        await dbContext.SaveChangesAsync(cancellationToken);

        var tempFilePath = storageService.FindExistingTempFilePath(videoId);
        if (string.IsNullOrEmpty(tempFilePath) || !File.Exists(tempFilePath))
        {
            var err = $"Temp upload file for video {videoId} does not exist.";
            _logger.LogError("{Error}", err);
            video.Status = VideoProcessingStatus.Failed;
            video.ErrorMessage = err;
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        var hlsDir = storageService.GetProcessedHlsDirectory(videoId);

        try
        {
            // 1. Probe rich metadata
            _logger.LogInformation("Probing metadata for video {VideoId}", videoId);
            var probeResult = await transcoderService.ProbeVideoAsync(tempFilePath, cancellationToken);

            video.DurationSeconds = probeResult.DurationSeconds;
            video.Width = probeResult.Width;
            video.Height = probeResult.Height;
            video.Resolution = probeResult.Resolution;
            video.VideoCodec = probeResult.VideoCodec;
            video.AudioCodec = probeResult.AudioCodec;
            video.Bitrate = probeResult.Bitrate;
            video.FrameRate = probeResult.FrameRate;
            if (probeResult.FileSizeBytes > 0)
            {
                video.FileSizeBytes = probeResult.FileSizeBytes;
            }

            // 2. Extract poster thumbnail
            _logger.LogInformation("Extracting poster thumbnail for video {VideoId}", videoId);
            var thumbnailPath = await transcoderService.GenerateThumbnailAsync(tempFilePath, hlsDir, cancellationToken);
            video.ThumbnailPath = thumbnailPath;

            // 3. Transcode to HLS (.m3u8 and .ts segments)
            _logger.LogInformation("Transcoding video {VideoId} to HLS", videoId);
            var masterPlaylistPath = await transcoderService.TranscodeToHlsAsync(tempFilePath, hlsDir, cancellationToken);
            video.HlsMasterPlaylistPath = masterPlaylistPath;

            // Mark as completed
            video.Status = VideoProcessingStatus.Completed;
            video.CompletedAt = DateTime.UtcNow;
            video.ErrorMessage = null;
            await dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Successfully completed HLS transcoding for video {VideoId}", videoId);

            // Cleanup temp upload
            storageService.CleanupTempFile(videoId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process video {VideoId}", videoId);
            video.Status = VideoProcessingStatus.Failed;
            video.ErrorMessage = ex.Message;
            await dbContext.SaveChangesAsync(CancellationToken.None);
        }
    }
}
