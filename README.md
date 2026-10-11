# High-Performance HLS Video Upload & Streaming Service (.NET 10 Minimal API + Nginx)

Lightweight, ultra-high-performance **Video Management & HLS Streaming Microservice** built with **.NET 10 Minimal APIs**, **Entity Framework Core (SQLite)**, **FFmpeg**, and **Nginx Reverse Proxy Caching**.

---

## 🚀 Key Features

- ⚡ **Controller-Free Minimal API Architecture**: Lightweight, high throughput, zero controller overhead.
- 🔄 **Asynchronous Background Transcoding**: Fast upload with immediate `202 Accepted` returning a `GUID`. Video probe, thumbnail generation, and HLS transcoding run seamlessly in a thread-safe `Channel<Guid>` queue.
- 📊 **Rich Metadata Extraction**: Automatic extraction of video duration, resolution, codecs (video/audio), bitrate, framerate, and file size via FFprobe.
- 🖼️ **Poster Thumbnail Generation**: Automatic keyframe/thumbnail extraction at high quality.
- 📺 **HLS Adaptive Streaming**: Standard `.m3u8` playlist manifest and `.ts` chunk generation compatible with any HLS player (Video.js, HLS.js, Plyr, iOS/Android Safari, VLC).
- 🚀 **Nginx Edge Caching**: Production-ready Nginx configuration for caching `.ts` segments with immutable headers (`max-age=31536000, immutable`).

---

## 📁 Architecture & Folder Structure

```text
laf-course-service/
├── src/
│   └── Laf.CourseService/
│       ├── BackgroundJobs/
│       │   ├── VideoProcessingQueue.cs     # Channel-based thread-safe async queue
│       │   └── VideoProcessingWorker.cs    # Background worker service
│       ├── Data/
│       │   └── MediaDbContext.cs           # EF Core Database Context
│       ├── Endpoints/
│       │   └── VideoEndpoints.cs           # Controller-less Minimal API Endpoints
│       ├── Models/
│       │   ├── VideoMetadata.cs            # EF Core Entity & Processing Status
│       │   ├── UploadVideoRequest.cs       # Upload request & response DTOs
│       │   ├── VideoResponseDto.cs         # JSON DTO for LAF & web clients
│       │   └── VideoProbeResult.cs         # Probe metadata model
│       ├── Services/
│       │   ├── IVideoStorageService.cs     # Disk I/O & file path management
│       │   ├── VideoStorageService.cs
│       │   ├── IHlsTranscoderService.cs    # FFmpeg / FFprobe execution & transcoding
│       │   ├── HlsTranscoderService.cs
│       │   └── MediaStorageOptions.cs      # Storage configuration options
│       ├── Storage/                        # Local file storage
│       │   ├── TempUploads/                # Temporary uploaded raw video files
│       │   └── ProcessedHls/               # Extracted {Guid}/master.m3u8, .ts chunks & thumbnail.jpg
│       ├── appsettings.json
│       └── Program.cs                      # Minimal API entry point & DI registration
├── nginx.conf                              # Production Nginx reverse proxy & segment cache
├── roadmap.md                              # Roadmap & specifications
└── README.md
```

---

## 🔌 API Endpoints Summary

| Method | Endpoint | Description |
|---|---|---|
| `POST` | `/api/videos/upload` | Upload video file (`multipart/form-data`) & start async HLS job |
| `GET` | `/api/videos/{id}` | Get metadata, processing status, and streaming URLs |
| `GET` | `/api/videos` | List all videos with pagination (`page`, `pageSize`) |
| `GET` | `/api/videos/{id}/manifest.m3u8` | Stream HLS master playlist |
| `GET` | `/api/videos/{id}/segments/{segmentName}` | Fetch HLS video chunk (`.ts`) with immutable caching |
| `GET` | `/api/videos/{id}/thumbnail` | Fetch poster thumbnail image (`.jpg`) |
| `DELETE` | `/api/videos/{id}` | Delete video and all HLS files |

---

## 🛠️ LAF (LED Application Framework) Integration

### C# Upload Snippet

```csharp
using System.Net.Http.Headers;

public async Task<Guid?> UploadVideoToCourseServiceAsync(string filePath, string userId, string serviceBaseUrl = "http://localhost:5000")
{
    using var httpClient = new HttpClient();
    using var form = new MultipartFormDataContent();

    var fileStream = File.OpenRead(filePath);
    using var streamContent = new StreamContent(fileStream);
    streamContent.Headers.ContentType = new MediaTypeHeaderValue("video/mp4");

    form.Add(streamContent, "videoFile", Path.GetFileName(filePath));
    form.Add(new StringContent(userId), "uploadedBy");

    var response = await httpClient.PostAsync($"{serviceBaseUrl}/api/videos/upload", form);
    if (response.IsSuccessStatusCode)
    {
        var result = await response.Content.ReadFromJsonAsync<VideoUploadAcceptedResponse>();
        return result?.VideoId;
    }

    return null;
}
```

---

## ⚙️ Prerequisites & Configuration

### 1. Install FFmpeg
Ensure `ffmpeg` and `ffprobe` are installed on your server or system:
- **Windows**: Install via `winget install Gyan.FFmpeg` or `choco install ffmpeg`.
- **Linux (Ubuntu/Debian)**: `sudo apt update && sudo apt install -y ffmpeg`
- **macOS**: `brew install ffmpeg`

You can also specify custom executable paths in `appsettings.json`:
```json
"MediaStorage": {
  "BasePath": "Storage",
  "FfmpegPath": "C:\\ffmpeg\\bin\\ffmpeg.exe",
  "FfprobePath": "C:\\ffmpeg\\bin\\ffprobe.exe"
}
```

### 2. Run the Service Locally
```bash
cd src/Laf.CourseService
dotnet run
```
The service will start and initialize the SQLite database automatically.

---

## 🌐 Production Nginx Deployment

Copy `nginx.conf` to `/etc/nginx/sites-available/default` or include it in your Nginx configuration to enable zero-copy TCP streaming and immutable segment caching:

```bash
sudo nginx -t && sudo systemctl reload nginx
```