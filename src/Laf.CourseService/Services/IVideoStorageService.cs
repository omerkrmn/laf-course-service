namespace Laf.CourseService.Services;

public interface IVideoStorageService
{
    void EnsureDirectoriesCreated();
    Task<string> SaveTemporaryUploadAsync(Guid videoId, IFormFile file, CancellationToken cancellationToken = default);
    string GetTempFilePath(Guid videoId, string originalFileName);
    string? FindExistingTempFilePath(Guid videoId);
    string GetProcessedHlsDirectory(Guid videoId);
    string GetMasterPlaylistPath(Guid videoId);
    string GetThumbnailPath(Guid videoId);
    string GetSegmentPath(Guid videoId, string segmentFileName);
    bool SegmentExists(Guid videoId, string segmentFileName);
    bool MasterPlaylistExists(Guid videoId);
    bool ThumbnailExists(Guid videoId);
    void CleanupTempFile(Guid videoId);
    void DeleteProcessedHls(Guid videoId);
}
