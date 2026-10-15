const SQLITE_STORAGE_KEY = 'laf_sqlite_database_bin';

const ClientDB = {
    sqlite: null,
    db: null,

    async init() {
        if (typeof initSqlJs === 'undefined') {
            throw new Error('sql.js kütüphanesi yüklenemedi. Lütfen internet bağlantınızı kontrol edin.');
        }

        // 1. WASM SQLite Motorunu Başlat
        this.sqlite = await initSqlJs({
            locateFile: file => `https://cdnjs.cloudflare.com/ajax/libs/sql.js/1.12.0/${file}`
        });

        // 2. Varsa kayıtlı SQLite veritabanı dosyasını yükle
        const savedData = localStorage.getItem(SQLITE_STORAGE_KEY);
        if (savedData) {
            try {
                const u8 = this._base64ToUint8(savedData);
                this.db = new this.sqlite.Database(u8);
            } catch (err) {
                console.warn('[ClientDB] Kayıtlı SQLite veritabanı okunamadı, yenisi oluşturuluyor:', err);
                this.db = new this.sqlite.Database();
            }
        } else {
            this.db = new this.sqlite.Database();
        }

        // 3. SQLite Foreign Key Desteğini Aktifleştir
        this.db.run('PRAGMA foreign_keys = ON;');

        // 4. Tabloları oluştur
        this._createTables();

        // 5. Performans İndeksleri
        this._createIndices();
        this.persist();

        return this.db;
    },

    _createTables() {
        this.db.run(`
            -- 1. MASTER PERSONELLER TABLOSU
            CREATE TABLE IF NOT EXISTS Personeller (
                id TEXT PRIMARY KEY,
                adSoyad TEXT NOT NULL,
                email TEXT UNIQUE NOT NULL,
                telefon TEXT,
                departman TEXT NOT NULL,
                unvan TEXT NOT NULL,
                videoYukleyebilir INTEGER NOT NULL DEFAULT 1,
                durum TEXT NOT NULL DEFAULT 'Aktif',
                kayitTarihi TEXT NOT NULL
            );

            -- 2. MASTER VİDEOLAR TABLOSU (3NF: yukleyenPersonelId FK)
            CREATE TABLE IF NOT EXISTS Videolar (
                id TEXT PRIMARY KEY,
                originalFileName TEXT NOT NULL,
                baslik TEXT,
                fileSizeBytes REAL DEFAULT 0,
                durationSeconds REAL DEFAULT 0,
                manifestUrl TEXT,
                thumbnailUrl TEXT,
                status INTEGER DEFAULT 0,
                statusText TEXT DEFAULT 'Pending',
                resolution TEXT,
                videoCodec TEXT,
                audioCodec TEXT,
                bitrate REAL DEFAULT 0,
                frameRate REAL DEFAULT 0,
                yukleyenPersonelId TEXT,
                yuklemeTarihi TEXT NOT NULL,
                FOREIGN KEY (yukleyenPersonelId) REFERENCES Personeller(id) ON DELETE SET NULL
            );

            -- 3. MASTER OYNATMA LİSTELERİ (3NF: olusturanPersonelId FK)
            CREATE TABLE IF NOT EXISTS OynatmaListeleri (
                id TEXT PRIMARY KEY,
                baslik TEXT NOT NULL,
                aciklama TEXT,
                olusturanPersonelId TEXT NOT NULL,
                olusturmaTarihi TEXT NOT NULL,
                FOREIGN KEY (olusturanPersonelId) REFERENCES Personeller(id) ON DELETE CASCADE
            );

            -- 4. OYNATMA LİSTESİ ÖGELERİ (JUNCTION TABLE)
            CREATE TABLE IF NOT EXISTS OynatmaListesiOgeleri (
                id TEXT PRIMARY KEY,
                oynatmaListesiId TEXT NOT NULL,
                videoId TEXT NOT NULL,
                baslik TEXT NOT NULL,
                sira INTEGER NOT NULL DEFAULT 1,
                kilitliMi INTEGER NOT NULL DEFAULT 0,
                eklenmeTarihi TEXT NOT NULL,
                FOREIGN KEY (oynatmaListesiId) REFERENCES OynatmaListeleri(id) ON DELETE CASCADE,
                FOREIGN KEY (videoId) REFERENCES Videolar(id) ON DELETE CASCADE,
                UNIQUE(oynatmaListesiId, videoId)
            );

            -- 5. VİDEO ANALİTİKLERİ & TELEMETRİ (3NF: videoId ve izleyenPersonelId FK)
            CREATE TABLE IF NOT EXISTS VideoAnalitikleri (
                id TEXT PRIMARY KEY,
                videoId TEXT NOT NULL,
                izleyenPersonelId TEXT NOT NULL,
                izlendiMi INTEGER NOT NULL DEFAULT 0,
                toplamVideoSuresi REAL DEFAULT 0,
                sonPozisyon REAL DEFAULT 0,
                netIzlemeSuresi REAL DEFAULT 0,
                izlenenMaksimumSaniye REAL DEFAULT 0,
                tamamlanmaOrani INTEGER DEFAULT 0,
                durum TEXT DEFAULT 'Başlamadı',
                oynatmaHizi REAL DEFAULT 1,
                atladiMi INTEGER DEFAULT 0,
                atlamaSayisi INTEGER DEFAULT 0,
                atlamaGecmisiJson TEXT,
                altTabSayisi INTEGER DEFAULT 0,
                altTabGecmisiJson TEXT,
                toplamOdakDisiSure REAL DEFAULT 0,
                sonIzlemeTarihi TEXT,
                FOREIGN KEY (videoId) REFERENCES Videolar(id) ON DELETE CASCADE,
                FOREIGN KEY (izleyenPersonelId) REFERENCES Personeller(id) ON DELETE CASCADE,
                UNIQUE(videoId, izleyenPersonelId)
            );

            -- 6. VİDEO DEĞERLENDİRME TESTLERİ (3NF: videoId ve olusturanPersonelId FK)
            CREATE TABLE IF NOT EXISTS VideoTestleri (
                id TEXT PRIMARY KEY,
                videoId TEXT NOT NULL,
                baslik TEXT NOT NULL,
                aciklama TEXT,
                tur TEXT NOT NULL,
                tetiklemeSaniyesi REAL DEFAULT 0,
                gecmeNotu INTEGER DEFAULT 70,
                zorunluMu INTEGER DEFAULT 1,
                sorularJson TEXT NOT NULL,
                olusturanPersonelId TEXT,
                olusturmaTarihi TEXT NOT NULL,
                FOREIGN KEY (videoId) REFERENCES Videolar(id) ON DELETE CASCADE,
                FOREIGN KEY (olusturanPersonelId) REFERENCES Personeller(id) ON DELETE SET NULL
            );

            -- 7. ÖĞRENCİ TEST SONUÇLARI (3NF: testId, videoId, personelId FK)
            CREATE TABLE IF NOT EXISTS TestSonuclari (
                id TEXT PRIMARY KEY,
                testId TEXT NOT NULL,
                videoId TEXT NOT NULL,
                personelId TEXT NOT NULL,
                puan REAL NOT NULL DEFAULT 0,
                dogruSayisi INTEGER NOT NULL DEFAULT 0,
                yanlisSayisi INTEGER NOT NULL DEFAULT 0,
                toplamSoru INTEGER NOT NULL DEFAULT 0,
                gectiMi INTEGER NOT NULL DEFAULT 0,
                verilenCevaplarJson TEXT,
                tamamlanmaTarihi TEXT NOT NULL,
                FOREIGN KEY (testId) REFERENCES VideoTestleri(id) ON DELETE CASCADE,
                FOREIGN KEY (videoId) REFERENCES Videolar(id) ON DELETE CASCADE,
                FOREIGN KEY (personelId) REFERENCES Personeller(id) ON DELETE CASCADE
            );
        `);
    },

    _createIndices() {
        try {
            this.db.run(`
                CREATE INDEX IF NOT EXISTS idx_ogeler_playlist ON OynatmaListesiOgeleri(oynatmaListesiId);
                CREATE INDEX IF NOT EXISTS idx_ogeler_video ON OynatmaListesiOgeleri(videoId);
                CREATE INDEX IF NOT EXISTS idx_analitik_video ON VideoAnalitikleri(videoId);
                CREATE INDEX IF NOT EXISTS idx_analitik_personel ON VideoAnalitikleri(izleyenPersonelId);
                CREATE INDEX IF NOT EXISTS idx_test_video ON VideoTestleri(videoId);
                CREATE INDEX IF NOT EXISTS idx_sonuc_test ON TestSonuclari(testId);
                CREATE INDEX IF NOT EXISTS idx_sonuc_personel ON TestSonuclari(personelId);
                CREATE INDEX IF NOT EXISTS idx_videolar_uploader ON Videolar(yukleyenPersonelId);
            `);
        } catch (e) {
            console.warn('[ClientDB] İndeks oluşturma uyarısı:', e);
        }
    },


    _tableExists(tableName) {
        try {
            const res = this.db.exec(`SELECT name FROM sqlite_master WHERE type='table' AND name='${tableName}'`);
            return Boolean(res && res.length > 0 && res[0].values && res[0].values.length > 0);
        } catch {
            return false;
        }
    },

    _getTableColumns(tableName) {
        try {
            const res = this.db.exec(`PRAGMA table_info('${tableName}')`);
            if (res && res.length > 0 && res[0].values) {
                return res[0].values.map(r => r[1]);
            }
        } catch { }
        return [];
    },

    persist() {
        try {
            const data = this.db.export();
            const b64 = this._uint8ToBase64(data);
            localStorage.setItem(SQLITE_STORAGE_KEY, b64);
        } catch (e) {
            console.error('[ClientDB] SQLite kaydedilirken hata:', e);
        }
    },

    // --- PERSONEL SQL İŞLEMLERİ ---
    async getAllPersonel() {
        const stmt = this.db.prepare('SELECT * FROM Personeller ORDER BY adSoyad ASC');
        const list = [];
        while (stmt.step()) {
            const row = stmt.getAsObject();
            list.push(new Personel({
                ...row,
                videoYukleyebilir: Boolean(row.videoYukleyebilir)
            }));
        }
        stmt.free();
        return list;
    },

    async getPersonelById(id) {
        const stmt = this.db.prepare('SELECT * FROM Personeller WHERE id = ? LIMIT 1');
        stmt.bind([id]);
        let result = null;
        if (stmt.step()) {
            const row = stmt.getAsObject();
            result = new Personel({
                ...row,
                videoYukleyebilir: Boolean(row.videoYukleyebilir)
            });
        }
        stmt.free();
        return result;
    },

    async addPersonel(p) {
        this.db.run(`
            INSERT INTO Personeller (id, adSoyad, email, telefon, departman, unvan, videoYukleyebilir, durum, kayitTarihi)
            VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)
        `, [
            p.id,
            p.adSoyad,
            p.email,
            p.telefon || '',
            p.departman,
            p.unvan,
            p.videoYukleyebilir ? 1 : 0,
            p.durum || 'Aktif',
            p.kayitTarihi || new Date().toISOString()
        ]);
        this.persist();
        return p;
    },

    async updatePersonel(p) {
        this.db.run(`
            UPDATE Personeller
            SET adSoyad = ?, email = ?, telefon = ?, departman = ?, unvan = ?, videoYukleyebilir = ?, durum = ?
            WHERE id = ?
        `, [
            p.adSoyad,
            p.email,
            p.telefon || '',
            p.departman,
            p.unvan,
            p.videoYukleyebilir ? 1 : 0,
            p.durum || 'Aktif',
            p.id
        ]);
        this.persist();
        return p;
    },

    async deletePersonel(id) {
        this.db.run('DELETE FROM Personeller WHERE id = ?', [id]);
        this.persist();
    },

    // --- ANALİTİK SQL İŞLEMLERİ (3NF NORMALİZE & JOINLİ) ---
    async getAllAnalytics() {
        const stmt = this.db.prepare(`
            SELECT 
                a.*,
                COALESCE(p.adSoyad, 'Bilinmeyen Personel') AS izleyenPersonelAd,
                COALESCE(p.departman, 'Genel') AS izleyenDepartman,
                COALESCE(v.baslik, v.originalFileName, 'Eğitim Videosu') AS videoTitle,
                COALESCE(up.adSoyad, 'Eğitmen') AS videoUploader
            FROM VideoAnalitikleri a
            LEFT JOIN Personeller p ON a.izleyenPersonelId = p.id
            LEFT JOIN Videolar v ON a.videoId = v.id
            LEFT JOIN Personeller up ON v.yukleyenPersonelId = up.id
            ORDER BY a.sonIzlemeTarihi DESC
        `);
        const list = [];
        while (stmt.step()) {
            list.push(this._parseAnalyticsRow(stmt.getAsObject()));
        }
        stmt.free();
        return list;
    },

    async getAnalyticsByVideoId(videoId) {
        const stmt = this.db.prepare(`
            SELECT 
                a.*,
                COALESCE(p.adSoyad, 'Bilinmeyen Personel') AS izleyenPersonelAd,
                COALESCE(p.departman, 'Genel') AS izleyenDepartman,
                COALESCE(v.baslik, v.originalFileName, 'Eğitim Videosu') AS videoTitle,
                COALESCE(up.adSoyad, 'Eğitmen') AS videoUploader
            FROM VideoAnalitikleri a
            LEFT JOIN Personeller p ON a.izleyenPersonelId = p.id
            LEFT JOIN Videolar v ON a.videoId = v.id
            LEFT JOIN Personeller up ON v.yukleyenPersonelId = up.id
            WHERE a.videoId = ?
            ORDER BY a.sonIzlemeTarihi DESC
        `);
        stmt.bind([videoId]);
        const list = [];
        while (stmt.step()) {
            list.push(this._parseAnalyticsRow(stmt.getAsObject()));
        }
        stmt.free();
        return list;
    },

    async getAnalyticsByUserAndVideo(videoId, userId) {
        const stmt = this.db.prepare(`
            SELECT 
                a.*,
                COALESCE(p.adSoyad, 'Bilinmeyen Personel') AS izleyenPersonelAd,
                COALESCE(p.departman, 'Genel') AS izleyenDepartman,
                COALESCE(v.baslik, v.originalFileName, 'Eğitim Videosu') AS videoTitle,
                COALESCE(up.adSoyad, 'Eğitmen') AS videoUploader
            FROM VideoAnalitikleri a
            LEFT JOIN Personeller p ON a.izleyenPersonelId = p.id
            LEFT JOIN Videolar v ON a.videoId = v.id
            LEFT JOIN Personeller up ON v.yukleyenPersonelId = up.id
            WHERE a.videoId = ? AND a.izleyenPersonelId = ? 
            LIMIT 1
        `);
        stmt.bind([videoId, userId]);
        let result = null;
        if (stmt.step()) {
            result = this._parseAnalyticsRow(stmt.getAsObject());
        }
        stmt.free();
        return result;
    },

    async saveAnalytics(r) {
        // 3NF: VideoAnalitikleri tablosuna yalnızca telemetri ve FK bilgisi yazılır
        this.db.run(`
            INSERT INTO VideoAnalitikleri (
                id, videoId, izleyenPersonelId,
                izlendiMi, toplamVideoSuresi, sonPozisyon, netIzlemeSuresi, izlenenMaksimumSaniye, tamamlanmaOrani,
                durum, oynatmaHizi, atladiMi, atlamaSayisi, atlamaGecmisiJson, altTabSayisi, altTabGecmisiJson,
                toplamOdakDisiSure, sonIzlemeTarihi
            ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
            ON CONFLICT(videoId, izleyenPersonelId) DO UPDATE SET
                izlendiMi = excluded.izlendiMi,
                toplamVideoSuresi = excluded.toplamVideoSuresi,
                sonPozisyon = excluded.sonPozisyon,
                netIzlemeSuresi = excluded.netIzlemeSuresi,
                izlenenMaksimumSaniye = excluded.izlenenMaksimumSaniye,
                tamamlanmaOrani = excluded.tamamlanmaOrani,
                durum = excluded.durum,
                oynatmaHizi = excluded.oynatmaHizi,
                atladiMi = excluded.atladiMi,
                atlamaSayisi = excluded.atlamaSayisi,
                atlamaGecmisiJson = excluded.atlamaGecmisiJson,
                altTabSayisi = excluded.altTabSayisi,
                altTabGecmisiJson = excluded.altTabGecmisiJson,
                toplamOdakDisiSure = excluded.toplamOdakDisiSure,
                sonIzlemeTarihi = excluded.sonIzlemeTarihi;
        `, [
            r.id,
            r.videoId,
            r.izleyenPersonelId,
            r.izlendiMi ? 1 : 0,
            Number(r.toplamVideoSuresi) || 0,
            Number(r.sonPozisyon) || 0,
            Number(r.netIzlemeSuresi) || 0,
            Number(r.izlenenMaksimumSaniye) || 0,
            Number(r.tamamlanmaOrani) || 0,
            r.durum || 'Başlamadı',
            Number(r.oynatmaHizi) || 1,
            r.atladiMi ? 1 : 0,
            Number(r.atlamaSayisi) || 0,
            JSON.stringify(r.atlamaGecmisi || []),
            Number(r.altTabSayisi) || 0,
            JSON.stringify(r.altTabGecmisi || []),
            Number(r.toplamOdakDisiSure) || 0,
            r.sonIzlemeTarihi || new Date().toISOString()
        ]);
        this.persist();
        return r;
    },

    // --- OYNATMA LİSTESİ SQL İŞLEMLERİ (3NF NORMALİZE & JOINLİ) ---
    async getAllPlaylists() {
        const stmt = this.db.prepare(`
            SELECT 
                pl.*,
                COALESCE(p.adSoyad, 'Eğitmen') AS olusturanPersonelAd,
                COUNT(o.id) AS videoSayisi
            FROM OynatmaListeleri pl
            LEFT JOIN Personeller p ON pl.olusturanPersonelId = p.id
            LEFT JOIN OynatmaListesiOgeleri o ON pl.id = o.oynatmaListesiId
            GROUP BY pl.id
            ORDER BY pl.olusturmaTarihi ASC
        `);
        const list = [];
        while (stmt.step()) {
            list.push(new OynatmaListesi(stmt.getAsObject()));
        }
        stmt.free();
        return list;
    },

    async getPlaylistById(id) {
        const stmt = this.db.prepare(`
            SELECT 
                pl.*,
                COALESCE(p.adSoyad, 'Eğitmen') AS olusturanPersonelAd
            FROM OynatmaListeleri pl
            LEFT JOIN Personeller p ON pl.olusturanPersonelId = p.id
            WHERE pl.id = ? 
            LIMIT 1
        `);
        stmt.bind([id]);
        let result = null;
        if (stmt.step()) {
            result = new OynatmaListesi(stmt.getAsObject());
        }
        stmt.free();
        return result;
    },

    async createPlaylist(pl) {
        this.db.run(`
            INSERT INTO OynatmaListeleri (id, baslik, aciklama, olusturanPersonelId, olusturmaTarihi)
            VALUES (?, ?, ?, ?, ?)
        `, [
            pl.id,
            pl.baslik,
            pl.aciklama || '',
            pl.olusturanPersonelId,
            pl.olusturmaTarihi || new Date().toISOString()
        ]);
        this.persist();
        return pl;
    },

    async updatePlaylist(pl) {
        this.db.run(`
            UPDATE OynatmaListeleri
            SET baslik = ?, aciklama = ?
            WHERE id = ?
        `, [pl.baslik, pl.aciklama || '', pl.id]);
        this.persist();
        return pl;
    },

    async deletePlaylist(id) {
        this.db.run('DELETE FROM OynatmaListeleri WHERE id = ?', [id]);
        this.persist();
    },

    // --- OYNATMA LİSTESİ ÖGELERİ (ARA İLİŞKİ VARLIĞI - JOINLİ) ---
    async getPlaylistItems(playlistId) {
        const stmt = this.db.prepare(`
            SELECT 
                o.id, 
                o.oynatmaListesiId, 
                o.videoId, 
                COALESCE(NULLIF(o.baslik, ''), v.baslik, v.originalFileName) AS baslik, 
                o.sira, 
                o.kilitliMi, 
                o.eklenmeTarihi,
                v.originalFileName,
                v.durationSeconds,
                v.manifestUrl,
                v.thumbnailUrl,
                v.status,
                v.statusText,
                v.resolution,
                v.videoCodec,
                v.audioCodec,
                v.bitrate,
                v.frameRate,
                v.yukleyenPersonelId,
                COALESCE(p.adSoyad, '') AS yukleyenPersonelAd,
                v.yuklemeTarihi
            FROM OynatmaListesiOgeleri o
            INNER JOIN Videolar v ON o.videoId = v.id
            LEFT JOIN Personeller p ON v.yukleyenPersonelId = p.id
            WHERE o.oynatmaListesiId = ? 
            ORDER BY o.sira ASC, o.eklenmeTarihi ASC
        `);
        stmt.bind([playlistId]);
        const list = [];
        while (stmt.step()) {
            const row = stmt.getAsObject();
            list.push(new OynatmaListesiOgesi({
                ...row,
                kilitliMi: Boolean(row.kilitliMi)
            }));
        }
        stmt.free();
        return list;
    },

    async addPlaylistItem(item) {
        if (!item.sira || item.sira <= 0) {
            const maxStmt = this.db.prepare('SELECT MAX(sira) as ms FROM OynatmaListesiOgeleri WHERE oynatmaListesiId = ?');
            maxStmt.bind([item.oynatmaListesiId]);
            let maxOrder = 0;
            if (maxStmt.step()) {
                const res = maxStmt.getAsObject();
                maxOrder = Number(res.ms) || 0;
            }
            maxStmt.free();
            item.sira = maxOrder + 1;
        }

        this.db.run(`
            INSERT INTO OynatmaListesiOgeleri (id, oynatmaListesiId, videoId, baslik, sira, kilitliMi, eklenmeTarihi)
            VALUES (?, ?, ?, ?, ?, ?, ?)
            ON CONFLICT(oynatmaListesiId, videoId) DO UPDATE SET
                baslik = excluded.baslik,
                sira = excluded.sira,
                kilitliMi = excluded.kilitliMi;
        `, [
            item.id,
            item.oynatmaListesiId,
            item.videoId,
            item.baslik,
            item.sira,
            item.kilitliMi ? 1 : 0,
            item.eklenmeTarihi || new Date().toISOString()
        ]);
        this.persist();
        return item;
    },

    async updatePlaylistItem(item) {
        this.db.run(`
            UPDATE OynatmaListesiOgeleri
            SET baslik = ?, sira = ?, kilitliMi = ?
            WHERE id = ?
        `, [
            item.baslik,
            item.sira,
            item.kilitliMi ? 1 : 0,
            item.id
        ]);
        this.persist();
        return item;
    },

    async deletePlaylistItem(id) {
        this.db.run('DELETE FROM OynatmaListesiOgeleri WHERE id = ?', [id]);
        this.persist();
    },

    async batchUpdatePlaylistItems(items) {
        for (const it of items) {
            this.db.run(`
                UPDATE OynatmaListesiOgeleri
                SET sira = ?, baslik = ?, kilitliMi = ?
                WHERE id = ?
            `, [
                it.sira,
                it.baslik,
                it.kilitliMi ? 1 : 0,
                it.id
            ]);
        }
        this.persist();
    },

    async getPlaylistItemByVideo(playlistId, videoId) {
        const stmt = this.db.prepare(`
            SELECT 
                o.id, 
                o.oynatmaListesiId, 
                o.videoId, 
                COALESCE(NULLIF(o.baslik, ''), v.baslik, v.originalFileName) AS baslik, 
                o.sira, 
                o.kilitliMi, 
                o.eklenmeTarihi,
                v.originalFileName,
                v.durationSeconds,
                v.manifestUrl,
                v.thumbnailUrl,
                v.status,
                v.statusText,
                v.resolution,
                v.videoCodec,
                v.audioCodec,
                v.bitrate,
                v.frameRate,
                v.yukleyenPersonelId,
                COALESCE(p.adSoyad, '') AS yukleyenPersonelAd,
                v.yuklemeTarihi
            FROM OynatmaListesiOgeleri o
            INNER JOIN Videolar v ON o.videoId = v.id
            LEFT JOIN Personeller p ON v.yukleyenPersonelId = p.id
            WHERE o.oynatmaListesiId = ? AND o.videoId = ? 
            LIMIT 1
        `);
        stmt.bind([playlistId, videoId]);
        let result = null;
        if (stmt.step()) {
            const row = stmt.getAsObject();
            result = new OynatmaListesiOgesi({
                ...row,
                kilitliMi: Boolean(row.kilitliMi)
            });
        }
        stmt.free();
        return result;
    },

    // --- VİDEO SQL İŞLEMLERİ (3NF NORMALİZE & JOINLİ) ---
    async getAllVideos() {
        const stmt = this.db.prepare(`
            SELECT 
                v.*,
                COALESCE(p.adSoyad, '') AS yukleyenPersonelAd
            FROM Videolar v
            LEFT JOIN Personeller p ON v.yukleyenPersonelId = p.id
            ORDER BY v.yuklemeTarihi DESC
        `);
        const list = [];
        while (stmt.step()) {
            list.push(new VideoKaydi(stmt.getAsObject()));
        }
        stmt.free();
        return list;
    },

    async getVideoById(id) {
        const stmt = this.db.prepare(`
            SELECT 
                v.*,
                COALESCE(p.adSoyad, '') AS yukleyenPersonelAd
            FROM Videolar v
            LEFT JOIN Personeller p ON v.yukleyenPersonelId = p.id
            WHERE v.id = ? 
            LIMIT 1
        `);
        stmt.bind([id]);
        let result = null;
        if (stmt.step()) {
            result = new VideoKaydi(stmt.getAsObject());
        }
        stmt.free();
        return result;
    },

    async saveVideo(v) {
        this.db.run(`
            INSERT OR REPLACE INTO Videolar (
                id, originalFileName, baslik, fileSizeBytes, durationSeconds,
                manifestUrl, thumbnailUrl, status, statusText, resolution,
                videoCodec, audioCodec, bitrate, frameRate,
                yukleyenPersonelId, yuklemeTarihi
            ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
        `, [
            v.id,
            v.originalFileName || '',
            v.baslik || '',
            Number(v.fileSizeBytes) || 0,
            Number(v.durationSeconds) || 0,
            v.manifestUrl || '',
            v.thumbnailUrl || '',
            Number(v.status) || 0,
            v.statusText || 'Pending',
            v.resolution || '',
            v.videoCodec || '',
            v.audioCodec || '',
            Number(v.bitrate) || 0,
            Number(v.frameRate) || 0,
            v.yukleyenPersonelId ? v.yukleyenPersonelId : null,
            v.yuklemeTarihi || new Date().toISOString()
        ]);
        this.persist();
        return v;
    },

    async deleteVideo(id) {
        this.db.run('DELETE FROM Videolar WHERE id = ?', [id]);
        this.persist();
    },

    async syncVideosFromServer(serverVideos) {
        if (!Array.isArray(serverVideos)) return;

        const serverVideoIds = new Set(serverVideos.map(sv => sv.id));

        // 1. Sunucudan silinmiş videoları yerel SQLite'tan kaldır
        const localVideos = await this.getAllVideos();
        for (const lv of localVideos) {
            if (!serverVideoIds.has(lv.id)) {
                this.db.run('DELETE FROM Videolar WHERE id = ?', [lv.id]);
            }
        }

        // 2. Sunucudaki güncel videoları kütüphaneye eşitle
        const allPersonels = await this.getAllPersonel();
        for (const sv of serverVideos) {
            const existing = await this.getVideoById(sv.id);
            let uploaderId = sv.uploaderId || existing?.yukleyenPersonelId || null;
            const uploaderAd = sv.uploadedBy || existing?.yukleyenPersonelAd || '';

            // uploaderId boş ise personeller arasından ad ile otomatik eşle
            if (!uploaderId && uploaderAd) {
                const matched = allPersonels.find(p =>
                    uploaderAd.toLowerCase().includes(p.adSoyad.toLowerCase()) ||
                    p.adSoyad.toLowerCase().includes(uploaderAd.toLowerCase())
                );
                if (matched) {
                    uploaderId = matched.id;
                }
            }

            const vk = new VideoKaydi({
                id: sv.id,
                originalFileName: sv.originalFileName || existing?.originalFileName || '',
                baslik: existing?.baslik || (sv.originalFileName ? sv.originalFileName.replace(/\.[^/.]+$/, "") : 'Eğitim Videosu'),
                fileSizeBytes: sv.fileSizeBytes ?? existing?.fileSizeBytes ?? 0,
                durationSeconds: sv.durationSeconds ?? existing?.durationSeconds ?? 0,
                manifestUrl: sv.manifestUrl || existing?.manifestUrl || '',
                thumbnailUrl: sv.thumbnailUrl || existing?.thumbnailUrl || '',
                status: sv.status ?? existing?.status ?? 0,
                statusText: sv.statusText || existing?.statusText || 'Pending',
                resolution: sv.resolution || existing?.resolution || '',
                videoCodec: sv.videoCodec || existing?.videoCodec || '',
                audioCodec: sv.audioCodec || existing?.audioCodec || '',
                bitrate: sv.bitrate ?? existing?.bitrate ?? 0,
                frameRate: sv.frameRate ?? existing?.frameRate ?? 0,
                yukleyenPersonelId: uploaderId,
                yuklemeTarihi: sv.createdAt || existing?.yuklemeTarihi || new Date().toISOString()
            });
            await this.saveVideo(vk);
        }
        this.persist();
    },

    // --- VİDEO TEST & DEĞERLENDİRME İŞLEMLERİ (3NF NORMALİZE & JOINLİ) ---
    async getTestsByVideoId(videoId) {
        const stmt = this.db.prepare(`
            SELECT 
                t.*,
                COALESCE(p.adSoyad, 'Eğitmen') AS olusturanPersonelAd
            FROM VideoTestleri t
            LEFT JOIN Personeller p ON t.olusturanPersonelId = p.id
            WHERE t.videoId = ? 
            ORDER BY t.tur ASC, t.tetiklemeSaniyesi ASC
        `);
        stmt.bind([videoId]);
        const list = [];
        while (stmt.step()) {
            list.push(this._parseTestRow(stmt.getAsObject()));
        }
        stmt.free();
        return list;
    },

    async getTestById(id) {
        const stmt = this.db.prepare(`
            SELECT 
                t.*,
                COALESCE(p.adSoyad, 'Eğitmen') AS olusturanPersonelAd
            FROM VideoTestleri t
            LEFT JOIN Personeller p ON t.olusturanPersonelId = p.id
            WHERE t.id = ? 
            LIMIT 1
        `);
        stmt.bind([id]);
        let result = null;
        if (stmt.step()) {
            result = this._parseTestRow(stmt.getAsObject());
        }
        stmt.free();
        return result;
    },

    async saveTest(t) {
        this.db.run(`
            INSERT OR REPLACE INTO VideoTestleri (
                id, videoId, baslik, aciklama, tur, tetiklemeSaniyesi, gecmeNotu, zorunluMu, sorularJson, olusturanPersonelId, olusturmaTarihi
            ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
        `, [
            t.id,
            t.videoId,
            t.baslik,
            t.aciklama || '',
            t.tur,
            Number(t.tetiklemeSaniyesi) || 0,
            Number(t.gecmeNotu) || 70,
            t.zorunluMu ? 1 : 0,
            JSON.stringify(t.sorular || []),
            t.olusturanPersonelId ? t.olusturanPersonelId : null,
            t.olusturmaTarihi || new Date().toISOString()
        ]);
        this.persist();
        return t;
    },

    async deleteTest(id) {
        this.db.run('DELETE FROM VideoTestleri WHERE id = ?', [id]);
        this.persist();
    },

    async saveTestResult(r) {
        // 3NF: personelAd ve departman TestSonuclari tablosuna yazılmaz; Personeller'den JOIN edilir
        this.db.run(`
            INSERT OR REPLACE INTO TestSonuclari (
                id, testId, videoId, personelId, puan, dogruSayisi, yanlisSayisi, toplamSoru, gectiMi, verilenCevaplarJson, tamamlanmaTarihi
            ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
        `, [
            r.id,
            r.testId,
            r.videoId,
            r.personelId,
            Number(r.puan) || 0,
            Number(r.dogruSayisi) || 0,
            Number(r.yanlisSayisi) || 0,
            Number(r.toplamSoru) || 0,
            r.gectiMi ? 1 : 0,
            JSON.stringify(r.verilenCevaplar || []),
            r.tamamlanmaTarihi || new Date().toISOString()
        ]);
        this.persist();
        return r;
    },

    async getTestResultsByVideoId(videoId) {
        const stmt = this.db.prepare(`
            SELECT 
                r.*,
                COALESCE(p.adSoyad, 'İsimsiz Personel') AS personelAd,
                COALESCE(p.departman, '-') AS personelDepartman,
                COALESCE(t.baslik, 'Test') AS testBaslik
            FROM TestSonuclari r
            LEFT JOIN Personeller p ON r.personelId = p.id
            LEFT JOIN VideoTestleri t ON r.testId = t.id
            WHERE r.videoId = ? 
            ORDER BY r.tamamlanmaTarihi DESC
        `);
        stmt.bind([videoId]);
        const list = [];
        while (stmt.step()) {
            list.push(this._parseTestResultRow(stmt.getAsObject()));
        }
        stmt.free();
        return list;
    },

    async getTestResultsByTestId(testId) {
        const stmt = this.db.prepare(`
            SELECT 
                r.*,
                COALESCE(p.adSoyad, 'İsimsiz Personel') AS personelAd,
                COALESCE(p.departman, '-') AS personelDepartman,
                COALESCE(t.baslik, 'Test') AS testBaslik
            FROM TestSonuclari r
            LEFT JOIN Personeller p ON r.personelId = p.id
            LEFT JOIN VideoTestleri t ON r.testId = t.id
            WHERE r.testId = ? 
            ORDER BY r.tamamlanmaTarihi DESC
        `);
        stmt.bind([testId]);
        const list = [];
        while (stmt.step()) {
            list.push(this._parseTestResultRow(stmt.getAsObject()));
        }
        stmt.free();
        return list;
    },

    async getUserTestResult(testId, userId) {
        const stmt = this.db.prepare(`
            SELECT 
                r.*,
                COALESCE(p.adSoyad, 'İsimsiz Personel') AS personelAd,
                COALESCE(p.departman, '-') AS personelDepartman,
                COALESCE(t.baslik, 'Test') AS testBaslik
            FROM TestSonuclari r
            LEFT JOIN Personeller p ON r.personelId = p.id
            LEFT JOIN VideoTestleri t ON r.testId = t.id
            WHERE r.testId = ? AND r.personelId = ? 
            ORDER BY r.tamamlanmaTarihi DESC 
            LIMIT 1
        `);
        stmt.bind([testId, userId]);
        let result = null;
        if (stmt.step()) {
            result = this._parseTestResultRow(stmt.getAsObject());
        }
        stmt.free();
        return result;
    },

    // --- PARSER YARDIMCILARI ---
    _parseTestRow(row) {
        let sorular = [];
        try { if (row.sorularJson) sorular = JSON.parse(row.sorularJson); } catch { }
        return new VideoTest({
            ...row,
            zorunluMu: Boolean(row.zorunluMu),
            sorular
        });
    },

    _parseTestResultRow(row) {
        let verilenCevaplar = [];
        try { if (row.verilenCevaplarJson) verilenCevaplar = JSON.parse(row.verilenCevaplarJson); } catch { }
        return new TestSonucu({
            ...row,
            gectiMi: Boolean(row.gectiMi),
            verilenCevaplar
        });
    },

    _parseAnalyticsRow(row) {
        let atlamaGecmisi = [];
        let altTabGecmisi = [];
        try { if (row.atlamaGecmisiJson) atlamaGecmisi = JSON.parse(row.atlamaGecmisiJson); } catch { }
        try { if (row.altTabGecmisiJson) altTabGecmisi = JSON.parse(row.altTabGecmisiJson); } catch { }

        return new VideoAnalitik({
            ...row,
            izlendiMi: Boolean(row.izlendiMi),
            atladiMi: Boolean(row.atladiMi),
            atlamaGecmisi,
            altTabGecmisi
        });
    },

    exportDatabaseFile(filename = 'laf_course_database.sqlite') {
        if (!this.db) {
            console.error('[ClientDB] Veritabanı başlatılmamış.');
            alert('Veritabanı henüz başlatılmadı.');
            return;
        }
        try {
            const data = this.db.export();
            const blob = new Blob([data], { type: 'application/x-sqlite3' });
            const url = URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = url;
            a.download = filename;
            document.body.appendChild(a);
            a.click();
            document.body.removeChild(a);
            URL.revokeObjectURL(url);
            console.log('[ClientDB] SQLite veritabanı başarıyla indirildi:', filename);
        } catch (err) {
            console.error('[ClientDB] Veritabanı indirme hatası:', err);
            alert('Veritabanı indirilirken bir hata oluştu: ' + err.message);
        }
    },

    /**
     * Tüm veritabanını drop eder / tamamen sıfırlar.
     */
    async dropDatabase() {
        console.warn('[ClientDB] Tüm veritabanı drop ediliyor...');
        localStorage.removeItem(SQLITE_STORAGE_KEY);
        localStorage.removeItem('laf_active_personel_id');

        const tables = [
            'TestSonuclari',
            'VideoTestleri',
            'VideoAnalitikleri',
            'OynatmaListesiOgeleri',
            'OynatmaListeleri',
            'Videolar',
            'Personeller',
            // Olası eski _norm tabloları
            'TestSonuclari_norm',
            'VideoAnalitikleri_norm',
            'OynatmaListeleri_norm',
            'Videolar_norm'
        ];

        try {
            this.db.run('PRAGMA foreign_keys = OFF;');
            for (const t of tables) {
                this.db.run(`DROP TABLE IF EXISTS ${t};`);
            }
            this.db.run('VACUUM;');
        } catch (e) {
            console.warn('[ClientDB] Drop işlemi sırasında uyarı:', e);
        }

        this._createTables();
        this._createIndices();
        this.db.run('PRAGMA foreign_keys = ON;');
        this.persist();

        console.log('[ClientDB] Tüm veritabanı başarıyla drop edildi ve temiz 3NF şema oluşturuldu.');
        return true;
    },

    _uint8ToBase64(u8) {
        let binary = '';
        const len = u8.byteLength;
        for (let i = 0; i < len; i++) {
            binary += String.fromCharCode(u8[i]);
        }
        return btoa(binary);
    },

    _base64ToUint8(b64) {
        const bin = atob(b64);
        const len = bin.length;
        const u8 = new Uint8Array(len);
        for (let i = 0; i < len; i++) {
            u8[i] = bin.charCodeAt(i);
        }
        return u8;
    }
};

// Global erişim
if (typeof window !== 'undefined') {
    window.dropDatabase = () => ClientDB.dropDatabase();
    window.ClientDB = ClientDB;
}
