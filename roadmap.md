# Roadmap: High-Performance HLS Video Upload & Streaming API (.NET Web API + Nginx)

This roadmap defines the complete step-by-step implementation blueprint for building a lightweight, ultra-high-performance **Video Management & HLS Streaming API** using **.NET Web API (C#)**, **EF Core**, **FFmpeg**, and **Nginx Reverse Proxy Caching**.

---

## 🏗️ Architectural Overview & Principles

1. **No Over-Engineered Architecture**: Keep it clean and pragmatic using a single Web API project with clean folder division (`Controllers`, `Services`, `Data`, `Models`, `BackgroundJobs`).
2. **Asynchronous HLS Processing**: Video upload saves the raw file and returns a `GUID` immediately (`202 Accepted`). Transcoding to HLS (`.m3u8` playlist & `.ts` chunks) runs in an in-memory background worker (`Channel<T>`).
3. **Rich Metadata Extraction**: FFprobe automatically extracts duration, resolution, codecs, bitrate, frame rate, file size, and generates a poster thumbnail.
4. **Nginx HLS Chunk Caching**: Serves `.ts` chunks with `max-age=31536000, immutable` headers for 100% cache hit rates on repeat views, drastically reducing bandwidth and CPU usage.

```
[LAF Client] ──(POST Video Upload)──> [.NET Web API] ──(Instant GUID)──> [DB: Pending Status]
                                              │
                                   (Background Channel Queue)
                                              │
                                              ▼
                                   [FFmpeg HLS Transcoder]
                                    ├─ Extract Metadata & Thumbnail
                                    ├─ Convert to .m3u8 + .ts Chunks
                                    └─ Update DB Status -> Completed
                                              │
[HLS Player / LAF] <──(Stream .m3u8/.ts)── [Nginx Cache] <── (Static Files)
```

---

## 📁 Proposed Folder Structure

```text
MediaStreamApi/
├── Controllers/
│   └── VideosController.cs         # Upload, Manifest, Segment, Thumbnail, Metadata endpoints
├── Data/
│   └── MediaDbContext.cs           # EF Core DbContext
├── Models/
│   ├── VideoMetadata.cs            # EF Core Entity for Video Metadata & Status
│   ├── UploadVideoRequest.cs       # DTO for incoming upload
│   └── VideoResponseDto.cs         # JSON DTO for LAF integration
├── Services/
│   ├── IVideoStorageService.cs     # File I/O & directory management
│   ├── VideoStorageService.cs
│   ├── IHlsTranscoderService.cs    # FFmpeg wrapper (HLS conversion & metadata extraction)
│   └── HlsTranscoderService.cs
├── BackgroundJobs/
│   ├── VideoProcessingQueue.cs     # Channel-based async queue
│   └── VideoProcessingWorker.cs    # Background HostedService worker
├── Storage/                        # Local Storage (or mapped mount)
│   ├── TempUploads/                # Temporary raw uploaded MP4s
│   └── ProcessedHls/               # Extracted {Guid}/master.m3u8, {Guid}/segment_000.ts, thumbnail.jpg
├── appsettings.json
├── nginx.conf                      # Production-ready Nginx configuration
└── Program.cs                      # Minimal APIs / Service registrations
```

---

## 📋 Execution Phases & Action Items

### Phase 1: Database & Entity Framework Core Setup

- [ ] **Data Model (`Models/VideoMetadata.cs`)**:
  Create entity containing full video details:
  ```csharp
  public class VideoMetadata
  {
      public Guid Id { get; set; }
      public string OriginalFileName { get; set; } = string.Empty;
      public string ContentType { get; set; } = string.Empty;
      public long FileSizeBytes { get; set; }
      public double DurationSeconds { get; set; }
      public int Width { get; set; }
      public int Height { get; set; }
      public string Resolution { get; set; } = string.Empty; // e.g. "1920x1080 (1080p)"
      public string VideoCodec { get; set; } = string.Empty; // e.g. "h264"
      public string AudioCodec { get; set; } = string.Empty; // e.g. "aac"
      public long Bitrate { get; set; }
      public double FrameRate { get; set; } // e.g. 29.97, 60.0
      public string UploadedBy { get; set; } = string.Empty;
      public VideoProcessingStatus Status { get; set; } // Pending, Processing, Completed, Failed
      public string? ErrorMessage { get; set; }
      public string HlsMasterPlaylistPath { get; set; } = string.Empty;
      public string ThumbnailPath { get; set; } = string.Empty;
      public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
      public DateTime? CompletedAt { get; set; }
  }

  public enum VideoProcessingStatus
  {
      Pending = 0,
      Processing = 1,
      Completed = 2,
      Failed = 3
  }
  ```
- [ ] **EF Core Context (`Data/MediaDbContext.cs`)**:
  Configure `DbSet<VideoMetadata>` with SQL Server / PostgreSQL / SQLite provider.

---

### Phase 2: Asynchronous Background Processing Pipeline

- [ ] **Processing Channel (`BackgroundJobs/VideoProcessingQueue.cs`)**:
  Implement lightweight thread-safe queue using System.Threading.Channels:
  ```csharp
  public class VideoProcessingQueue
  {
      private readonly Channel<Guid> _queue = Channel.CreateUnbounded<Guid>();
      public ValueTask QueueVideoAsync(Guid videoId) => _queue.Writer.WriteAsync(videoId);
      public ValueTask<Guid> DequeueVideoAsync(CancellationToken cancellationToken) => _queue.Reader.ReadAsync(cancellationToken);
  }
  ```
- [ ] **Background Worker (`BackgroundJobs/VideoProcessingWorker.cs`)**:
  Implement `BackgroundService` to process queued videos sequentially without blocking Web API requests.

---

### Phase 3: FFmpeg HLS Transcoding & Metadata Service

- [ ] **FFmpeg Integration (`Services/HlsTranscoderService.cs`)**:
  Implement FFmpeg commands using `CliWrap` or `System.Diagnostics.Process`:
  1. **Metadata Probe Command (FFprobe)**:
     ```bash
     ffprobe -v quiet -print_format json -show_format -show_streams input.mp4
     ```
     Parse JSON to populate `DurationSeconds`, `Width`, `Height`, `Bitrate`, `VideoCodec`, `AudioCodec`, `FrameRate`.

  2. **Poster Thumbnail Extraction**:
     ```bash
     ffmpeg -ss 00:00:01 -i input.mp4 -vframes 1 -q:v 2 thumbnail.jpg
     ```

  3. **HLS Transcoding (MP4 -> .m3u8 + .ts Chunks)**:
     ```bash
     ffmpeg -i input.mp4 \
       -codec:v libx264 -crf 22 -preset fast \
       -codec:a aac -b:a 128k \
       -hls_time 6 \
       -hls_playlist_type vod \
       -hls_segment_filename "segment_%03d.ts" \
       master.m3u8
     ```

---

### Phase 4: API Endpoints Implementation (`Controllers/VideosController.cs`)

- [ ] **Upload Endpoint (`POST /api/videos/upload`)**:
  - Accepts `IFormFile videoFile` and optional `uploadedBy` parameter.
  - Validates extension (`.mp4`, `.mov`, `.avi`, `.mkv`) and max size.
  - Generates new `Guid videoId`.
  - Saves file to `Storage/TempUploads/{videoId}.mp4`.
  - Creates DB entry with `Status = VideoProcessingStatus.Pending`.
  - Triggers `_processingQueue.QueueVideoAsync(videoId)`.
  - Returns HTTP 202 Accepted with JSON containing `videoId` and status endpoint URL.

- [ ] **Status / Metadata Endpoint (`GET /api/videos/{id}`)**:
  - Returns full metadata JSON (used by LAF or Web Clients to check if video processing is `Completed`).

- [ ] **HLS Playlist Endpoint (`GET /api/videos/{id}/manifest.m3u8`)**:
  - Serves `master.m3u8` file with headers:
    `Cache-Control: public, max-age=60`
    `Content-Type: application/vnd.apple.mpegurl`

- [ ] **HLS Segment Endpoint (`GET /api/videos/{id}/segments/{segmentName}`)**:
  - Serves `.ts` video chunk file with high-performance caching headers:
    `Cache-Control: public, max-age=31536000, immutable`
    `Content-Type: video/mp2t`

- [ ] **Thumbnail Endpoint (`GET /api/videos/{id}/thumbnail`)**:
  - Serves `thumbnail.jpg` image (`Content-Type: image/jpeg`).

---

### Phase 5: Nginx Reverse Proxy & HLS Caching Setup (`nginx.conf`)

- [ ] **Nginx Config Creation**:
  Configure Nginx to cache `.ts` segments in RAM/Disk while forwarding API calls to .NET Web API:

```nginx
# Cache zone for HLS video segments
proxy_cache_path /var/cache/nginx/hls_cache 
                 levels=1:2 
                 keys_zone=hls_cache:50m 
                 max_size=10g 
                 inactive=30d 
                 use_temp_path=off;

server {
    listen 80;
    server_name media.yourdomain.com;

    client_max_body_size 500M; # Allow large video uploads

    location / {
        proxy_pass http://127.0.0.1:5000;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }

    # Highly optimized caching for HLS video segments (.ts files)
    location ~* \.ts$ {
        proxy_pass http://127.0.0.1:5000;
        proxy_cache hls_cache;
        proxy_cache_valid 200 30d;
        proxy_cache_key $request_uri;
        
        # Performance headers
        add_header X-Cache-Status $upstream_cache_status;
        add_header Cache-Control "public, max-age=31536000, immutable";
        
        # Zero-copy TCP streaming
        sendfile on;
        tcp_nopush on;
    }

    # Short cache for playlist manifests (.m3u8)
    location ~* \.m3u8$ {
        proxy_pass http://127.0.0.1:5000;
        proxy_cache hls_cache;
        proxy_cache_valid 200 60s;
        add_header X-Cache-Status $upstream_cache_status;
        add_header Cache-Control "public, max-age=60";
    }
}
```

---

### Phase 6: LED Application Framework (LAF) Integration Guide

- [ ] **LAF C# Upload Snippet**:
  Document how LAF sends video from form to API:
  ```csharp
  // Example C# code for LAF form to upload video to .NET API
  using (var client = new HttpClient())
  using (var content = new MultipartFormDataContent())
  {
      var fileBytes = File.ReadAllBytes(filePath);
      var byteArrayContent = new ByteArrayContent(fileBytes);
      byteArrayContent.Headers.ContentType = new MediaTypeHeaderValue("video/mp4");
      
      content.Add(byteArrayContent, "videoFile", Path.GetFileName(filePath));
      content.Add(new StringContent(userId), "uploadedBy");

      var response = await client.PostAsync("http://media.yourdomain.com/api/videos/upload", content);
      var json = await response.Content.ReadAsStringAsync();
      // Returns Guid videoId
  }
  ```

---

## ✅ Verification & Testing Checklist

1. [ ] **Upload Test**: Send 50MB MP4 file via POST request -> verify immediate `202 Accepted` response with `videoId`.
2. [ ] **Background Conversion**: Verify FFmpeg runs in background, creates `Storage/ProcessedHls/{Guid}/master.m3u8` and `.ts` chunks.
3. [ ] **Metadata Extraction**: Query `GET /api/videos/{id}` -> verify `durationSeconds`, `resolution`, `videoCodec`, `fileSizeBytes` are accurately populated in DB.
4. [ ] **HLS Playback Test**: Open `http://localhost/api/videos/{id}/manifest.m3u8` in VLC Player or Video.js web player.
5. [ ] **Nginx Cache Verification**: Inspect HTTP response headers on `.ts` segment requests -> verify `X-Cache-Status: HIT` on repeat segment requests.
