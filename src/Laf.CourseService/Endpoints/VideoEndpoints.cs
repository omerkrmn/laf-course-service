using Laf.CourseService.BackgroundJobs;
using Laf.CourseService.Data;
using Laf.CourseService.Models;
using Laf.CourseService.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Laf.CourseService.Endpoints;

public static class VideoEndpoints
{
    public static RouteGroupBuilder MapVideoEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/videos")
            .WithTags("Videos");

        // 1. Upload Video
        group.MapPost("/upload", async (
            HttpRequest request,
            [FromForm] IFormFile? videoFile,
            [FromForm] IFormFile? thumbnailFile,
            [FromForm] string? uploadedBy,
            [FromForm] string? uploaderId,
            MediaDbContext dbContext,
            IVideoStorageService storageService,
            VideoProcessingQueue queue,
            IOptions<MediaStorageOptions> storageOptions,
            CancellationToken cancellationToken) =>
        {
            var options = storageOptions.Value;

            // Fallback checking if video is inside request.Form.Files
            if (videoFile == null && request.HasFormContentType && request.Form.Files.Count > 0)
            {
                videoFile = request.Form.Files["videoFile"] ?? request.Form.Files[0];
            }

            // Fallback checking if thumbnail is inside request.Form.Files
            if (thumbnailFile == null && request.HasFormContentType)
            {
                thumbnailFile = request.Form.Files["thumbnailFile"] ?? request.Form.Files["thumbnail"];
            }

            if (string.IsNullOrEmpty(uploadedBy) && request.HasFormContentType && request.Form.TryGetValue("uploadedBy", out var uploadedByVal))
            {
                uploadedBy = uploadedByVal.ToString();
            }

            if (string.IsNullOrEmpty(uploaderId) && request.HasFormContentType && request.Form.TryGetValue("uploaderId", out var uploaderIdVal))
            {
                uploaderId = uploaderIdVal.ToString();
            }

            if (videoFile == null || videoFile.Length == 0)
            {
                return Results.BadRequest(new { error = "A valid video file is required in 'videoFile' field." });
            }

            if (videoFile.Length > options.MaxFileSizeBytes)
            {
                return Results.BadRequest(new
                {
                    error = $"File size exceeds the maximum limit of {options.MaxFileSizeBytes / (1024 * 1024)} MB."
                });
            }

            var extension = Path.GetExtension(videoFile.FileName).ToLowerInvariant();
            if (!options.AllowedExtensions.Contains(extension))
            {
                return Results.BadRequest(new
                {
                    error = $"Unsupported file extension '{extension}'. Allowed extensions: {string.Join(", ", options.AllowedExtensions)}"
                });
            }

            var videoId = Guid.NewGuid();

            // Handle optional custom thumbnail uploaded by user
            string? customThumbnailPath = null;
            if (thumbnailFile != null && thumbnailFile.Length > 0)
            {
                var thumbExt = Path.GetExtension(thumbnailFile.FileName).ToLowerInvariant();
                var allowedImageExts = new[] { ".jpg", ".jpeg", ".png", ".webp" };
                if (!allowedImageExts.Contains(thumbExt))
                {
                    return Results.BadRequest(new
                    {
                        error = $"Unsupported thumbnail image extension '{thumbExt}'. Allowed: {string.Join(", ", allowedImageExts)}"
                    });
                }

                if (thumbnailFile.Length > 10 * 1024 * 1024)
                {
                    return Results.BadRequest(new { error = "Thumbnail file size exceeds the 10 MB limit." });
                }

                customThumbnailPath = await storageService.SaveCustomThumbnailAsync(videoId, thumbnailFile, cancellationToken);
            }

            // Save raw upload to temp folder
            await storageService.SaveTemporaryUploadAsync(videoId, videoFile, cancellationToken);

            // Create initial DB entry with Pending status
            var videoMetadata = new VideoMetadata
            {
                Id = videoId,
                OriginalFileName = videoFile.FileName,
                ContentType = string.IsNullOrWhiteSpace(videoFile.ContentType) ? "video/mp4" : videoFile.ContentType,
                FileSizeBytes = videoFile.Length,
                UploadedBy = string.IsNullOrWhiteSpace(uploadedBy) ? "Anonymous" : uploadedBy.Trim(),
                UploaderId = string.IsNullOrWhiteSpace(uploaderId) ? string.Empty : uploaderId.Trim(),
                Status = VideoProcessingStatus.Pending,
                ThumbnailPath = customThumbnailPath ?? string.Empty,
                CreatedAt = DateTime.UtcNow
            };

            dbContext.Videos.Add(videoMetadata);
            await dbContext.SaveChangesAsync(cancellationToken);

            // Enqueue background processing job
            await queue.QueueVideoAsync(videoId);

            var baseUrl = $"{request.Scheme}://{request.Host}";
            var statusUrl = $"{baseUrl}/api/videos/{videoId}";

            var response = new VideoUploadAcceptedResponse
            {
                VideoId = videoId,
                Message = "Video uploaded successfully and queued for HLS transcoding.",
                Status = VideoProcessingStatus.Pending,
                StatusUrl = statusUrl
            };

            return Results.Accepted(statusUrl, response);
        })
        .DisableAntiforgery()
        .WithName("UploadVideo")
        .WithSummary("Uploads a video file with an optional custom thumbnail, and starts asynchronous HLS transcoding.")
        .Produces<VideoUploadAcceptedResponse>(StatusCodes.Status202Accepted)
        .Produces(StatusCodes.Status400BadRequest);

        // 1.1 Update/Set Custom Thumbnail for an Existing Video
        group.MapPut("/{id:guid}/thumbnail", async (
            Guid id,
            HttpRequest request,
            [FromForm] IFormFile? thumbnailFile,
            MediaDbContext dbContext,
            IVideoStorageService storageService,
            CancellationToken cancellationToken) =>
        {
            if (thumbnailFile == null && request.HasFormContentType && request.Form.Files.Count > 0)
            {
                thumbnailFile = request.Form.Files["thumbnailFile"] ?? request.Form.Files["thumbnail"] ?? request.Form.Files[0];
            }

            if (thumbnailFile == null || thumbnailFile.Length == 0)
            {
                return Results.BadRequest(new { error = "A valid image file is required in 'thumbnailFile' field." });
            }

            var thumbExt = Path.GetExtension(thumbnailFile.FileName).ToLowerInvariant();
            var allowedImageExts = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            if (!allowedImageExts.Contains(thumbExt))
            {
                return Results.BadRequest(new { error = $"Unsupported image extension '{thumbExt}'. Allowed: {string.Join(", ", allowedImageExts)}" });
            }

            var video = await dbContext.Videos.FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
            if (video == null)
            {
                return Results.NotFound(new { error = $"Video with ID '{id}' was not found." });
            }

            var savedPath = await storageService.SaveCustomThumbnailAsync(id, thumbnailFile, cancellationToken);
            video.ThumbnailPath = savedPath;
            await dbContext.SaveChangesAsync(cancellationToken);

            var baseUrl = $"{request.Scheme}://{request.Host}";
            return Results.Ok(new
            {
                message = "Thumbnail updated successfully.",
                thumbnailUrl = $"{baseUrl}/api/videos/{id}/thumbnail"
            });
        })
        .DisableAntiforgery()
        .WithName("UpdateVideoThumbnail")
        .WithSummary("Sets or updates the poster thumbnail for an existing video.")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound);

        // 2. Get Video Status & Metadata by ID
        group.MapGet("/{id:guid}", async (
            Guid id,
            HttpRequest request,
            MediaDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var video = await dbContext.Videos.AsNoTracking().FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
            if (video == null)
            {
                return Results.NotFound(new { error = $"Video with ID '{id}' was not found." });
            }

            var baseUrl = $"{request.Scheme}://{request.Host}";
            return Results.Ok(VideoResponseDto.FromEntity(video, baseUrl));
        })
        .WithName("GetVideoById")
        .WithSummary("Gets metadata and processing status for a video.")
        .Produces<VideoResponseDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        // 3. List All Videos
        group.MapGet("/", async (
            HttpRequest request,
            MediaDbContext dbContext,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            CancellationToken cancellationToken = default) =>
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 20;

            var totalCount = await dbContext.Videos.CountAsync(cancellationToken);
            var items = await dbContext.Videos
                .AsNoTracking()
                .OrderByDescending(v => v.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            var baseUrl = $"{request.Scheme}://{request.Host}";
            var dtoList = items.Select(v => VideoResponseDto.FromEntity(v, baseUrl)).ToList();

            return Results.Ok(new
            {
                total = totalCount,
                page,
                pageSize,
                totalPages = (int)Math.Ceiling((double)totalCount / pageSize),
                data = dtoList
            });
        })
        .WithName("ListVideos")
        .WithSummary("Lists uploaded videos with pagination.")
        .Produces(StatusCodes.Status200OK);

        // 4. HLS Master Playlist Manifest (.m3u8)
        group.MapGet("/{id:guid}/manifest.m3u8", async (
            Guid id,
            HttpResponse response,
            MediaDbContext dbContext,
            IVideoStorageService storageService,
            IMemoryCache cache,
            CancellationToken cancellationToken) =>
        {
            var video = await dbContext.Videos.AsNoTracking().FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
            if (video == null)
            {
                return Results.NotFound(new { error = $"Video with ID '{id}' was not found." });
            }

            if (video.Status != VideoProcessingStatus.Completed)
            {
                return Results.Problem(
                    detail: $"Video processing is currently '{video.Status}'. Master playlist is only available when status is Completed.",
                    statusCode: StatusCodes.Status409Conflict);
            }

            var masterPath = storageService.GetMasterPlaylistPath(id);
            if (!File.Exists(masterPath))
            {
                return Results.NotFound(new { error = "HLS master playlist file not found on server." });
            }

            var manifestKey = $"manifest_{id}";
            if (cache.TryGetValue(manifestKey, out _))
            {
                response.Headers["X-Cache-Status"] = "HIT";
            }
            else
            {
                response.Headers["X-Cache-Status"] = "MISS";
                cache.Set(manifestKey, true, TimeSpan.FromSeconds(60));
            }

            response.Headers.CacheControl = "public, max-age=60";
            return Results.File(masterPath, "application/vnd.apple.mpegurl", lastModified: File.GetLastWriteTimeUtc(masterPath), enableRangeProcessing: true);
        })
        .WithName("GetHlsManifest")
        .WithSummary("Serves HLS master playlist (.m3u8) for video streaming.")
        .Produces(StatusCodes.Status200OK, contentType: "application/vnd.apple.mpegurl")
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);

        // 5. HLS Video Segment (.ts chunk) - supports both direct relative /{id}/{segmentName} and /{id}/segments/{segmentName}
        group.MapGet("/{id:guid}/{segmentName}", ServeSegment);
        group.MapGet("/{id:guid}/segments/{segmentName}", ServeSegment)
            .WithName("GetHlsSegment")
            .WithSummary("Serves an immutable HLS video chunk segment (.ts).")
            .Produces(StatusCodes.Status200OK, contentType: "video/mp2t")
            .Produces(StatusCodes.Status404NotFound);

        static IResult ServeSegment(
            Guid id,
            string segmentName,
            HttpResponse response,
            IVideoStorageService storageService,
            IMemoryCache cache)
        {
            if (string.IsNullOrWhiteSpace(segmentName) || !segmentName.EndsWith(".ts", StringComparison.OrdinalIgnoreCase))
            {
                return Results.BadRequest(new { error = "Invalid segment file name. Must be a .ts file." });
            }

            var segmentPath = storageService.GetSegmentPath(id, segmentName);
            if (!File.Exists(segmentPath))
            {
                return Results.NotFound(new { error = $"Segment '{segmentName}' not found for video '{id}'." });
            }

            var segmentKey = $"seg_{id}_{segmentName.ToLowerInvariant()}";
            if (cache.TryGetValue(segmentKey, out _))
            {
                response.Headers["X-Cache-Status"] = "HIT";
            }
            else
            {
                response.Headers["X-Cache-Status"] = "MISS";
                cache.Set(segmentKey, true, TimeSpan.FromMinutes(60));
            }

            response.Headers.CacheControl = "public, max-age=31536000, immutable";
            return Results.File(segmentPath, "video/mp2t", lastModified: File.GetLastWriteTimeUtc(segmentPath), enableRangeProcessing: true);
        }

        // 6. Poster Thumbnail
        group.MapGet("/{id:guid}/thumbnail", async (
            Guid id,
            HttpResponse response,
            MediaDbContext dbContext,
            IVideoStorageService storageService,
            CancellationToken cancellationToken) =>
        {
            var video = await dbContext.Videos.AsNoTracking().FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
            if (video == null)
            {
                return Results.NotFound(new { error = $"Video with ID '{id}' was not found." });
            }

            var thumbnailPath = storageService.GetThumbnailPath(id);
            if (!File.Exists(thumbnailPath))
            {
                return Results.NotFound(new { error = "Thumbnail file not found." });
            }

            response.Headers.CacheControl = "public, max-age=86400";
            return Results.File(thumbnailPath, "image/jpeg", lastModified: File.GetLastWriteTimeUtc(thumbnailPath));
        })
        .WithName("GetVideoThumbnail")
        .WithSummary("Serves the generated video thumbnail image.")
        .Produces(StatusCodes.Status200OK, contentType: "image/jpeg")
        .Produces(StatusCodes.Status404NotFound);

        // 7. Delete All Videos (Maintenance / Test)
        group.MapDelete("/all", async (
            MediaDbContext dbContext,
            IVideoStorageService storageService,
            CancellationToken cancellationToken) =>
        {
            // Clear physical temp files and HLS processed folders
            storageService.DeleteAll();

            // Clear database records
            var deletedCount = await dbContext.Videos.ExecuteDeleteAsync(cancellationToken);

            return Results.Ok(new
            {
                message = "All videos and associated files deleted successfully.",
                deletedCount
            });
        })
        .WithName("DeleteAllVideos")
        .WithSummary("Deletes all videos and removes physical temporary and processed HLS storage files.")
        .Produces(StatusCodes.Status200OK);

        // 8. Delete Video
        group.MapDelete("/{id:guid}", async (
            Guid id,
            MediaDbContext dbContext,
            IVideoStorageService storageService,
            CancellationToken cancellationToken) =>
        {
            var video = await dbContext.Videos.FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
            if (video == null)
            {
                return Results.NotFound(new { error = $"Video with ID '{id}' was not found." });
            }

            storageService.CleanupTempFile(id);
            storageService.DeleteProcessedHls(id);

            dbContext.Videos.Remove(video);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Results.NoContent();
        })
        .WithName("DeleteVideo")
        .WithSummary("Deletes video record and all associated processed HLS files.")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound);

        return group;
    }
}
