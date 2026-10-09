using Microsoft.Extensions.Options;

namespace Laf.CourseService.Services;

public class VideoStorageService : IVideoStorageService
{
    private readonly MediaStorageOptions _options;
    private readonly string _absoluteBasePath;
    private readonly string _tempUploadsPath;
    private readonly string _processedHlsPath;

    public VideoStorageService(IOptions<MediaStorageOptions> options, IWebHostEnvironment env)
    {
        _options = options.Value;

        _absoluteBasePath = Path.IsPathRooted(_options.BasePath)
            ? _options.BasePath
            : Path.Combine(env.ContentRootPath, _options.BasePath);

        _tempUploadsPath = Path.Combine(_absoluteBasePath, _options.TempUploadsFolder);
        _processedHlsPath = Path.Combine(_absoluteBasePath, _options.ProcessedHlsFolder);

        EnsureDirectoriesCreated();
    }

    public void EnsureDirectoriesCreated()
    {
        if (!Directory.Exists(_absoluteBasePath))
            Directory.CreateDirectory(_absoluteBasePath);

        if (!Directory.Exists(_tempUploadsPath))
            Directory.CreateDirectory(_tempUploadsPath);

        if (!Directory.Exists(_processedHlsPath))
            Directory.CreateDirectory(_processedHlsPath);
    }

    public async Task<string> SaveTemporaryUploadAsync(Guid videoId, IFormFile file, CancellationToken cancellationToken = default)
    {
        EnsureDirectoriesCreated();
        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrEmpty(extension))
            extension = ".mp4";

        var targetPath = Path.Combine(_tempUploadsPath, $"{videoId}{extension}");

        await using var stream = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await file.CopyToAsync(stream, cancellationToken);

        return targetPath;
    }

    public string GetTempFilePath(Guid videoId, string originalFileName)
    {
        var ext = Path.GetExtension(originalFileName);
        if (string.IsNullOrEmpty(ext)) ext = ".mp4";
        return Path.Combine(_tempUploadsPath, $"{videoId}{ext}");
    }

    public string? FindExistingTempFilePath(Guid videoId)
    {
        EnsureDirectoriesCreated();
        var files = Directory.GetFiles(_tempUploadsPath, $"{videoId}.*");
        return files.FirstOrDefault();
    }

    public string GetProcessedHlsDirectory(Guid videoId)
    {
        return Path.Combine(_processedHlsPath, videoId.ToString());
    }

    public string GetMasterPlaylistPath(Guid videoId)
    {
        return Path.Combine(GetProcessedHlsDirectory(videoId), "master.m3u8");
    }

    public string GetThumbnailPath(Guid videoId)
    {
        return Path.Combine(GetProcessedHlsDirectory(videoId), "thumbnail.jpg");
    }

    public string GetSegmentPath(Guid videoId, string segmentFileName)
    {
        // Sanitize segment file name to avoid directory traversal
        var safeFileName = Path.GetFileName(segmentFileName);
        return Path.Combine(GetProcessedHlsDirectory(videoId), safeFileName);
    }

    public bool SegmentExists(Guid videoId, string segmentFileName)
    {
        var path = GetSegmentPath(videoId, segmentFileName);
        return File.Exists(path);
    }

    public bool MasterPlaylistExists(Guid videoId)
    {
        return File.Exists(GetMasterPlaylistPath(videoId));
    }

    public bool ThumbnailExists(Guid videoId)
    {
        return File.Exists(GetThumbnailPath(videoId));
    }

    public void CleanupTempFile(Guid videoId)
    {
        var tempFile = FindExistingTempFilePath(videoId);
        if (!string.IsNullOrEmpty(tempFile) && File.Exists(tempFile))
        {
            try
            {
                File.Delete(tempFile);
            }
            catch
            {
                // Ignored - best effort cleanup
            }
        }
    }

    public void DeleteProcessedHls(Guid videoId)
    {
        var dir = GetProcessedHlsDirectory(videoId);
        if (Directory.Exists(dir))
        {
            try
            {
                Directory.Delete(dir, recursive: true);
            }
            catch
            {
                // Ignored - best effort cleanup
            }
        }
    }
}
