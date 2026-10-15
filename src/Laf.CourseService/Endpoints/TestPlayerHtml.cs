namespace Laf.CourseService.Endpoints;

public static class TestPlayerHtml
{
    public const string Content = """
<!DOCTYPE html>
<html lang="tr">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>LAF Course Service - HLS Stream & Test Dashboard</title>
  <link rel="preconnect" href="https://fonts.googleapis.com">
  <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
  <link href="https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700&display=swap" rel="stylesheet">
  <script src="https://cdn.jsdelivr.net/npm/hls.js@1.5.8/dist/hls.min.js"></script>
  <style>
    :root {
      --bg: #0b0f19;
      --card-bg: rgba(18, 24, 38, 0.75);
      --card-border: rgba(255, 255, 255, 0.08);
      --accent: #6366f1;
      --accent-hover: #4f46e5;
      --accent-glow: rgba(99, 102, 241, 0.25);
      --success: #10b981;
      --warning: #f59e0b;
      --danger: #ef4444;
      --text: #f3f4f6;
      --text-muted: #9ca3af;
    }

    * { box-sizing: border-box; margin: 0; padding: 0; }
    body {
      font-family: 'Inter', system-ui, -apple-system, sans-serif;
      background: radial-gradient(circle at top, #1e1b4b 0%, var(--bg) 50%);
      color: var(--text);
      min-height: 100vh;
      padding: 24px;
    }

    .container {
      max-width: 1200px;
      margin: 0 auto;
      display: flex;
      flex-direction: column;
      gap: 24px;
    }

    header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 18px 24px;
      background: var(--card-bg);
      border: 1px solid var(--card-border);
      border-radius: 16px;
      backdrop-filter: blur(12px);
    }

    .logo-group {
      display: flex;
      align-items: center;
      gap: 12px;
    }

    .badge {
      font-size: 11px;
      font-weight: 600;
      padding: 4px 10px;
      border-radius: 999px;
      text-transform: uppercase;
      letter-spacing: 0.5px;
    }
    .badge-online { background: rgba(16, 185, 129, 0.15); color: var(--success); border: 1px solid rgba(16, 185, 129, 0.3); }

    .grid-layout {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 24px;
    }

    @media (max-width: 900px) {
      .grid-layout { grid-template-columns: 1fr; }
    }

    .card {
      background: var(--card-bg);
      border: 1px solid var(--card-border);
      border-radius: 16px;
      padding: 24px;
      backdrop-filter: blur(12px);
      box-shadow: 0 8px 32px rgba(0, 0, 0, 0.3);
    }

    .card-title {
      font-size: 17px;
      font-weight: 600;
      margin-bottom: 16px;
      display: flex;
      align-items: center;
      justify-content: space-between;
    }

    /* Video Player */
    .player-container {
      position: relative;
      width: 100%;
      background: #000;
      border-radius: 12px;
      overflow: hidden;
      aspect-ratio: 16 / 9;
      box-shadow: 0 12px 30px rgba(0,0,0,0.5);
      border: 1px solid rgba(255, 255, 255, 0.1);
    }

    video {
      width: 100%;
      height: 100%;
      object-fit: contain;
    }

    .player-stats {
      margin-top: 14px;
      padding: 12px;
      background: rgba(0, 0, 0, 0.4);
      border-radius: 8px;
      font-size: 13px;
      color: var(--text-muted);
      display: flex;
      flex-wrap: wrap;
      gap: 16px;
    }
    .stat-item strong { color: var(--text); }

    /* Upload Area */
    .drop-zone {
      border: 2px dashed rgba(99, 102, 241, 0.4);
      border-radius: 12px;
      padding: 32px 20px;
      text-align: center;
      cursor: pointer;
      transition: all 0.2s ease;
      background: rgba(99, 102, 241, 0.03);
    }

    .drop-zone:hover, .drop-zone.dragover {
      border-color: var(--accent);
      background: var(--accent-glow);
    }

    .drop-zone p { color: var(--text-muted); font-size: 13px; margin-top: 8px; }
    .drop-zone strong { color: var(--text); font-size: 15px; }

    .file-input { display: none; }

    .btn {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      gap: 8px;
      background: var(--accent);
      color: #fff;
      border: none;
      padding: 10px 18px;
      border-radius: 8px;
      font-size: 14px;
      font-weight: 500;
      cursor: pointer;
      transition: background 0.2s;
    }
    .btn:hover { background: var(--accent-hover); }
    .btn:disabled { opacity: 0.5; cursor: not-allowed; }
    .btn-sm { padding: 6px 12px; font-size: 12px; }
    .btn-danger { background: rgba(239, 68, 68, 0.2); color: #fca5a5; border: 1px solid rgba(239, 68, 68, 0.3); }
    .btn-danger:hover { background: rgba(239, 68, 68, 0.4); }

    /* Progress bar */
    .progress-wrapper {
      margin-top: 14px;
      display: none;
    }
    .progress-bar-bg {
      background: rgba(255,255,255,0.1);
      height: 8px;
      border-radius: 99px;
      overflow: hidden;
    }
    .progress-bar-fill {
      background: var(--accent);
      height: 100%;
      width: 0%;
      transition: width 0.2s;
    }
    .progress-text {
      font-size: 12px;
      color: var(--text-muted);
      margin-top: 6px;
      display: flex;
      justify-content: space-between;
    }

    /* Video List */
    .video-list {
      display: flex;
      flex-direction: column;
      gap: 10px;
      max-height: 400px;
      overflow-y: auto;
      margin-top: 12px;
    }

    .video-item {
      display: flex;
      align-items: center;
      justify-content: space-between;
      padding: 12px 14px;
      background: rgba(255, 255, 255, 0.03);
      border: 1px solid var(--card-border);
      border-radius: 10px;
      cursor: pointer;
      transition: all 0.2s;
    }
    .video-item:hover, .video-item.active {
      border-color: var(--accent);
      background: rgba(99, 102, 241, 0.08);
    }
    .video-meta {
      display: flex;
      flex-direction: column;
      gap: 4px;
    }
    .video-title { font-weight: 500; font-size: 14px; }
    .video-subtitle { font-size: 12px; color: var(--text-muted); }

    .status-badge {
      font-size: 11px;
      padding: 3px 8px;
      border-radius: 6px;
      font-weight: 600;
    }
    .status-Completed { background: rgba(16, 185, 129, 0.2); color: #6ee7b7; }
    .status-Processing { background: rgba(245, 158, 11, 0.2); color: #fcd34d; }
    .status-Pending { background: rgba(156, 163, 175, 0.2); color: #d1d5db; }
    .status-Failed { background: rgba(239, 68, 68, 0.2); color: #fca5a5; }

    .url-box {
      margin-top: 12px;
      padding: 10px 14px;
      background: rgba(0, 0, 0, 0.4);
      border-radius: 8px;
      font-family: monospace;
      font-size: 12px;
      word-break: break-all;
      color: #a5b4fc;
      border: 1px solid rgba(255,255,255,0.06);
    }
  </style>
</head>
<body>
  <div class="container">
    <header>
      <div class="logo-group">
        <h1 style="font-size: 20px; font-weight: 700;">LAF Course Video Service</h1>
        <span class="badge badge-online">● Windows Server / IIS Ready</span>
      </div>
      <div>
        <a href="/openapi/v1.json" target="_blank" class="btn btn-sm" style="background: rgba(255,255,255,0.08); text-decoration: none;">OpenAPI Spec</a>
      </div>
    </header>

    <div class="grid-layout">
      <!-- SOL TARAF: Web Video Oynatıcı -->
      <div class="card">
        <div class="card-title">
          <span>🎬 HLS Canlı İzleme & Test Oynatıcı</span>
          <span id="playerBadge" class="badge" style="display:none;"></span>
        </div>

        <div class="player-container">
          <video id="videoPlayer" controls playsinline></video>
        </div>

        <div class="player-stats" id="playerStats">
          <div class="stat-item">Çözünürlük: <strong id="statResolution">-</strong></div>
          <div class="stat-item">Codec: <strong id="statCodec">-</strong></div>
          <div class="stat-item">Bitrate: <strong id="statBitrate">-</strong></div>
          <div class="stat-item">Süre: <strong id="statDuration">-</strong></div>
        </div>

        <div id="manifestBox" class="url-box" style="display: none;">
          <div><strong>Master HLS Manifest URL:</strong></div>
          <div id="manifestUrlText">-</div>
        </div>
      </div>

      <!-- SAĞ TARAF: Video Yükleme ve Liste -->
      <div class="card">
        <div class="card-title">
          <span>📤 Video Yükleme (Maks: 500 MB)</span>
          <button id="refreshBtn" class="btn btn-sm" style="background: rgba(255,255,255,0.08);">🔄 Yenile</button>
        </div>

        <div class="drop-zone" id="dropZone">
          <strong>Video Seçin veya Buraya Sürükleyin</strong>
          <p>Desteklenen: .mp4, .mov, .avi, .mkv, .webm</p>
          <input type="file" id="fileInput" class="file-input" accept="video/*">
        </div>

        <div style="margin-top: 12px; display: flex; flex-direction: column; gap: 6px;">
          <label style="font-size: 12px; color: var(--text-muted); font-weight: 500;">
            🖼️ Özel Kapak Görseli (İsteğe Bağlı - Boşsa videodan otomatik üretilir):
          </label>
          <input type="file" id="thumbInput" accept="image/png, image/jpeg, image/webp" style="font-size: 12px; color: var(--text-muted); background: rgba(255,255,255,0.05); padding: 8px 12px; border-radius: 8px; border: 1px solid var(--card-border); cursor: pointer;">
        </div>

        <div class="progress-wrapper" id="progressWrapper">
          <div class="progress-bar-bg">
            <div class="progress-bar-fill" id="progressBar"></div>
          </div>
          <div class="progress-text">
            <span id="progressStatus">Yükleniyor...</span>
            <span id="progressPercent">%0</span>
          </div>
        </div>

        <div class="card-title" style="margin-top: 24px; margin-bottom: 8px;">
          <span style="font-size: 15px;">📋 Sistemdeki Videolar</span>
        </div>
        <div class="video-list" id="videoList">
          <div style="font-size: 13px; color: var(--text-muted); padding: 8px;">Videolar yükleniyor...</div>
        </div>
      </div>
    </div>
  </div>

  <script>
    const videoPlayer = document.getElementById('videoPlayer');
    const dropZone = document.getElementById('dropZone');
    const fileInput = document.getElementById('fileInput');
    const progressWrapper = document.getElementById('progressWrapper');
    const progressBar = document.getElementById('progressBar');
    const progressStatus = document.getElementById('progressStatus');
    const progressPercent = document.getElementById('progressPercent');
    const videoList = document.getElementById('videoList');
    const refreshBtn = document.getElementById('refreshBtn');
    const manifestBox = document.getElementById('manifestBox');
    const manifestUrlText = document.getElementById('manifestUrlText');

    let currentHls = null;
    let pollInterval = null;

    // Drop zone interactions
    dropZone.addEventListener('click', () => fileInput.click());
    dropZone.addEventListener('dragover', (e) => { e.preventDefault(); dropZone.classList.add('dragover'); });
    dropZone.addEventListener('dragleave', () => dropZone.classList.remove('dragover'));
    dropZone.addEventListener('drop', (e) => {
      e.preventDefault();
      dropZone.classList.remove('dragover');
      if (e.dataTransfer.files.length > 0) uploadFile(e.dataTransfer.files[0]);
    });
    fileInput.addEventListener('change', (e) => {
      if (e.target.files.length > 0) uploadFile(e.target.files[0]);
    });

    refreshBtn.addEventListener('click', loadVideos);

    function uploadFile(file) {
      if (!file) return;

      const formData = new FormData();
      formData.append('videoFile', file);
      formData.append('uploadedBy', 'LocalTester');

      const thumbInput = document.getElementById('thumbInput');
      if (thumbInput && thumbInput.files && thumbInput.files[0]) {
        formData.append('thumbnailFile', thumbInput.files[0]);
      }

      progressWrapper.style.display = 'block';
      progressBar.style.width = '0%';
      progressPercent.innerText = '%0';
      progressStatus.innerText = 'Yükleniyor: ' + file.name;

      const xhr = new XMLHttpRequest();
      xhr.open('POST', '/api/videos/upload', true);

      xhr.upload.onprogress = (e) => {
        if (e.lengthComputable) {
          const percent = Math.round((e.loaded / e.total) * 100);
          progressBar.style.width = percent + '%';
          progressPercent.innerText = '%' + percent;
          if (percent === 100) {
            progressStatus.innerText = 'İşleniyor (Transcoding sırasına alındı)...';
          }
        }
      };

      xhr.onload = () => {
        if (xhr.status === 202) {
          const resp = JSON.parse(xhr.responseText);
          progressStatus.innerText = 'Yüklendi! FFmpeg HLS dönüştürme başladı...';
          loadVideos();
          startPolling(resp.videoId);
        } else {
          progressStatus.innerText = 'Hata: ' + xhr.responseText;
        }
      };

      xhr.onerror = () => {
        progressStatus.innerText = 'Sunucuya bağlanırken hata oluştu.';
      };

      xhr.send(formData);
    }

    function startPolling(videoId) {
      if (pollInterval) clearInterval(pollInterval);
      pollInterval = setInterval(async () => {
        try {
          const res = await fetch(`/api/videos/${videoId}`);
          if (res.ok) {
            const data = await res.json();
            if (data.statusText === 'Completed') {
              clearInterval(pollInterval);
              progressWrapper.style.display = 'none';
              loadVideos();
              playVideo(data);
            } else if (data.statusText === 'Failed') {
              clearInterval(pollInterval);
              progressStatus.innerText = 'Dönüştürme başarısız oldu: ' + (data.errorMessage || 'Bilinmeyen hata');
              loadVideos();
            }
          }
        } catch (e) {
          console.error(e);
        }
      }, 2500);
    }

    async function loadVideos() {
      try {
        const res = await fetch('/api/videos?pageSize=50');
        if (!res.ok) return;
        const result = await res.json();
        const list = result.data || [];

        if (list.length === 0) {
          videoList.innerHTML = '<div style="font-size: 13px; color: var(--text-muted); padding: 8px;">Henüz hiç video yüklenmemiş.</div>';
          return;
        }

        videoList.innerHTML = '';
        list.forEach(v => {
          const item = document.createElement('div');
          item.className = 'video-item';
          item.innerHTML = `
            <div style="display:flex; align-items:center; gap:12px;">
              ${v.thumbnailUrl ? `<img src="${v.thumbnailUrl}" style="width: 52px; height: 36px; object-fit: cover; border-radius: 6px; border: 1px solid rgba(255,255,255,0.15);" loading="lazy">` : ''}
              <div class="video-meta">
                <div class="video-title">${v.originalFileName}</div>
                <div class="video-subtitle">${v.resolution || 'Hazırlanıyor'} • ${(v.fileSizeBytes / (1024*1024)).toFixed(1)} MB • ${new Date(v.createdAt).toLocaleTimeString()}</div>
              </div>
            </div>
            <div style="display:flex; align-items:center; gap:8px;">
              <span class="status-badge status-${v.statusText}">${v.statusText}</span>
              ${v.statusText === 'Completed' ? `<button class="btn btn-sm play-btn">▶ Oynat</button>` : ''}
              <button class="btn btn-sm btn-danger del-btn">Sil</button>
            </div>
          `;

          const playBtn = item.querySelector('.play-btn');
          if (playBtn) {
            playBtn.addEventListener('click', (e) => {
              e.stopPropagation();
              playVideo(v);
            });
          }

          item.querySelector('.del-btn').addEventListener('click', async (e) => {
            e.stopPropagation();
            if (confirm(`'${v.originalFileName}' videosunu silmek istediğinize emin misiniz?`)) {
              await fetch(`/api/videos/${v.id}`, { method: 'DELETE' });
              loadVideos();
            }
          });

          item.addEventListener('click', () => {
            if (v.statusText === 'Completed') playVideo(v);
          });

          videoList.appendChild(item);
        });
      } catch (err) {
        console.error('Videolar yüklenemedi:', err);
      }
    }

    function playVideo(video) {
      if (!video || !video.manifestUrl) return;

      document.getElementById('statResolution').innerText = video.resolution || '-';
      document.getElementById('statCodec').innerText = `${video.videoCodec} / ${video.audioCodec}` || '-';
      document.getElementById('statBitrate').innerText = video.bitrate ? (video.bitrate / 1000).toFixed(0) + ' kbps' : '-';
      document.getElementById('statDuration').innerText = video.durationSeconds ? video.durationSeconds.toFixed(1) + ' sn' : '-';

      manifestBox.style.display = 'block';
      manifestUrlText.innerText = video.manifestUrl;
      videoPlayer.poster = video.thumbnailUrl || '';

      if (currentHls) {
        currentHls.destroy();
        currentHls = null;
      }

      const streamUrl = video.manifestUrl;

      if (Hls.isSupported()) {
        currentHls = new Hls({
          maxBufferLength: 30,
          enableWorker: true
        });
        currentHls.loadSource(streamUrl);
        currentHls.attachMedia(videoPlayer);
        currentHls.on(Hls.Events.MANIFEST_PARSED, () => {
          videoPlayer.play().catch(e => console.log('Autoplay prevented:', e));
        });
      } else if (videoPlayer.canPlayType('application/vnd.apple.mpegurl')) {
        // Safari native HLS
        videoPlayer.src = streamUrl;
        videoPlayer.addEventListener('loadedmetadata', () => {
          videoPlayer.play().catch(e => console.log('Autoplay prevented:', e));
        });
      } else {
        alert('Tarayıcınız HLS video oynatmayı desteklemiyor.');
      }
    }

    // İlk açılışta videoları listele
    loadVideos();
  </script>
</body>
</html>
""";
}
