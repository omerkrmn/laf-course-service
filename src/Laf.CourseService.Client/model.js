/**
 * LAF Course Client - Normalize Veri Modelleri (model.js)
 * 3. Normal Form (3NF) ilkelerine uygun, veri tekrarını önleyen saf ilişkisel modeller.
 */

function generateGuid() {
    if (typeof crypto !== 'undefined' && crypto.randomUUID) {
        return crypto.randomUUID();
    }
    return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function (c) {
        const r = Math.random() * 16 | 0, v = c === 'x' ? r : (r & 0x3 | 0x8);
        return v.toString(16);
    });
}

/**
 * 1. Master Personel Modeli
 */
class Personel {
    constructor({
        id = generateGuid(),
        adSoyad = '',
        email = '',
        telefon = '',
        departman = 'Eğitim & Akademi',
        unvan = '',
        videoYukleyebilir = true,
        durum = 'Aktif',
        kayitTarihi = new Date().toISOString()
    } = {}) {
        this.id = id;
        this.adSoyad = adSoyad;
        this.email = email;
        this.telefon = telefon;
        this.departman = departman;
        this.unvan = unvan;
        this.videoYukleyebilir = Boolean(videoYukleyebilir);
        this.durum = durum;
        this.kayitTarihi = kayitTarihi;
    }
}

/**
 * 2. Master Video Modeli
 * Yükleyici bilgisi ilişkisel olarak Personeller tablosuna (yukleyenPersonelId) bağlıdır.
 */
class VideoKaydi {
    constructor({
        id = '',
        originalFileName = '',
        baslik = '',
        fileSizeBytes = 0,
        durationSeconds = 0,
        manifestUrl = '',
        thumbnailUrl = '',
        status = 0,
        statusText = 'Pending',
        resolution = '',
        videoCodec = '',
        audioCodec = '',
        bitrate = 0,
        frameRate = 0,
        yukleyenPersonelId = '',
        uploaderId = '',
        yukleyenPersonelAd = '',
        uploadedBy = '',
        yuklemeTarihi = new Date().toISOString()
    } = {}) {
        this.id = id;
        this.originalFileName = originalFileName;
        this.baslik = baslik || (originalFileName ? originalFileName.replace(/\.[^/.]+$/, "") : 'Eğitim Videosu');
        this.fileSizeBytes = Number(fileSizeBytes) || 0;
        this.durationSeconds = Number(durationSeconds) || 0;
        this.manifestUrl = manifestUrl || '';
        this.thumbnailUrl = thumbnailUrl || '';
        this.status = Number(status) || 0;
        this.statusText = statusText || 'Pending';
        this.resolution = resolution || '';
        this.videoCodec = videoCodec || '';
        this.audioCodec = audioCodec || '';
        this.bitrate = Number(bitrate) || 0;
        this.frameRate = Number(frameRate) || 0;
        // İlişkisel yabancı anahtar (FK):
        this.yukleyenPersonelId = yukleyenPersonelId || uploaderId || '';
        // SQL JOIN ile doldurulan türetilmiş isim alanı:
        this.yukleyenPersonelAd = yukleyenPersonelAd || uploadedBy || '';
        this.yuklemeTarihi = yuklemeTarihi || new Date().toISOString();
    }

    // Kod tabanı genelinde veri tekrarı yapmadan tam uyumluluk (Single Source of Truth):
    get uploaderId() { return this.yukleyenPersonelId; }
    set uploaderId(val) { this.yukleyenPersonelId = val; }
    get uploadedBy() { return this.yukleyenPersonelAd; }
    set uploadedBy(val) { this.yukleyenPersonelAd = val; }
}

/**
 * 3. Oynatma Listesi Modeli
 * Oluşturan personel Personeller tablosuyla (olusturanPersonelId) ilişkilenir.
 */
class OynatmaListesi {
    constructor({
        id = generateGuid(),
        baslik = '',
        aciklama = '',
        olusturanPersonelId = '',
        olusturanPersonelAd = '',
        videoSayisi = 0,
        olusturmaTarihi = new Date().toISOString()
    } = {}) {
        this.id = id;
        this.baslik = baslik;
        this.aciklama = aciklama;
        this.olusturanPersonelId = olusturanPersonelId;
        // SQL JOIN üzerinden türetilir (tabloda tekrar saklanmaz):
        this.olusturanPersonelAd = olusturanPersonelAd;
        this.videoSayisi = Number(videoSayisi) || 0;
        this.olusturmaTarihi = olusturmaTarihi;
    }
}

/**
 * 4. Oynatma Listesi Öğesi (Ara İlişki Varlığı - Junction Entity)
 */
class OynatmaListesiOgesi {
    constructor({
        id = generateGuid(),
        oynatmaListesiId = '',
        videoId = '',
        baslik = '',
        sira = 1,
        kilitliMi = false,
        eklenmeTarihi = new Date().toISOString(),
        // JOIN ile çekilen video özellikleri (ekranda doğrudan sunum için):
        originalFileName = '',
        durationSeconds = 0,
        manifestUrl = '',
        thumbnailUrl = '',
        status = 0,
        statusText = 'Pending',
        resolution = '',
        videoCodec = '',
        audioCodec = '',
        bitrate = 0,
        frameRate = 0,
        yukleyenPersonelId = '',
        yukleyenPersonelAd = '',
        yuklemeTarihi = ''
    } = {}) {
        this.id = id;
        this.oynatmaListesiId = oynatmaListesiId;
        this.videoId = videoId;
        this.baslik = baslik;
        this.sira = Number(sira) || 1;
        this.kilitliMi = Boolean(kilitliMi);
        this.eklenmeTarihi = eklenmeTarihi;

        // JOIN sunum alanları:
        this.originalFileName = originalFileName;
        this.durationSeconds = Number(durationSeconds) || 0;
        this.manifestUrl = manifestUrl;
        this.thumbnailUrl = thumbnailUrl;
        this.status = Number(status) || 0;
        this.statusText = statusText;
        this.resolution = resolution;
        this.videoCodec = videoCodec;
        this.audioCodec = audioCodec;
        this.bitrate = Number(bitrate) || 0;
        this.frameRate = Number(frameRate) || 0;
        this.yukleyenPersonelId = yukleyenPersonelId;
        this.yukleyenPersonelAd = yukleyenPersonelAd;
        this.yuklemeTarihi = yuklemeTarihi;
    }

    get uploaderId() { return this.yukleyenPersonelId; }
    get uploadedBy() { return this.yukleyenPersonelAd; }
}

/**
 * 5. Video İzleme & Telemetri Analitik Modeli (3NF Normalize Edilmiş)
 * Kullanıcı adı, departman, video adı gibi tekrarlayan alanlar veritabanında saklanmaz;
 * Video ve Personel tabloları ile JOIN yapılarak çalışma anında dinamik oluşturulur.
 */
class VideoAnalitik {
    constructor({
        id = generateGuid(),
        videoId = '',
        izleyenPersonelId = '',
        izlendiMi = false,
        toplamVideoSuresi = 0,
        sonPozisyon = 0,
        netIzlemeSuresi = 0,
        izlenenMaksimumSaniye = 0,
        tamamlanmaOrani = 0,
        durum = 'Başlamadı',
        oynatmaHizi = 1,
        atladiMi = false,
        atlamaSayisi = 0,
        atlamaGecmisi = [],
        altTabSayisi = 0,
        altTabGecmisi = [],
        toplamOdakDisiSure = 0,
        sonIzlemeTarihi = new Date().toISOString(),
        // Normalized JOIN ile gelen türetilmiş alanlar:
        videoTitle = '',
        videoUploader = '',
        izleyenPersonelAd = '',
        izleyenDepartman = ''
    } = {}) {
        this.id = id;
        this.videoId = videoId;
        this.izleyenPersonelId = izleyenPersonelId;
        this.izlendiMi = Boolean(izlendiMi);
        this.toplamVideoSuresi = Number(toplamVideoSuresi) || 0;
        this.sonPozisyon = Number(sonPozisyon) || 0;
        this.netIzlemeSuresi = Number(netIzlemeSuresi) || 0;
        this.izlenenMaksimumSaniye = Number(izlenenMaksimumSaniye) || 0;
        this.tamamlanmaOrani = Number(tamamlanmaOrani) || 0;
        this.durum = durum;
        this.oynatmaHizi = Number(oynatmaHizi) || 1;
        this.atladiMi = Boolean(atladiMi);
        this.atlamaSayisi = Number(atlamaSayisi) || 0;
        this.atlamaGecmisi = Array.isArray(atlamaGecmisi) ? atlamaGecmisi : [];
        this.altTabSayisi = Number(altTabSayisi) || 0;
        this.altTabGecmisi = Array.isArray(altTabGecmisi) ? altTabGecmisi : [];
        this.toplamOdakDisiSure = Number(toplamOdakDisiSure) || 0;
        this.sonIzlemeTarihi = sonIzlemeTarihi;

        // JOIN üzerinden gelen sunum alanları:
        this.videoTitle = videoTitle;
        this.videoUploader = videoUploader;
        this.izleyenPersonelAd = izleyenPersonelAd;
        this.izleyenDepartman = izleyenDepartman;
    }
}

/**
 * 6. Video Değerlendirme Testi Modeli
 */
class VideoTest {
    constructor({
        id = generateGuid(),
        videoId = '',
        baslik = '',
        aciklama = '',
        tur = 'video_sonu', // 'video_ici' | 'video_sonu'
        tetiklemeSaniyesi = 0,
        gecmeNotu = 70,
        zorunluMu = true,
        sorular = [],
        olusturanPersonelId = '',
        olusturmaTarihi = new Date().toISOString(),
        olusturanPersonelAd = ''
    } = {}) {
        this.id = id;
        this.videoId = videoId;
        this.baslik = baslik;
        this.aciklama = aciklama;
        this.tur = tur;
        this.tetiklemeSaniyesi = Number(tetiklemeSaniyesi) || 0;
        this.gecmeNotu = Number(gecmeNotu) || 70;
        this.zorunluMu = Boolean(zorunluMu);
        this.sorular = Array.isArray(sorular) ? sorular : [];
        this.olusturanPersonelId = olusturanPersonelId;
        this.olusturmaTarihi = olusturmaTarihi;
        this.olusturanPersonelAd = olusturanPersonelAd;
    }
}

/**
 * 7. Test Sonucu Modeli (3NF Normalize Edilmiş)
 * Personel adı ve departman bilgisi Personeller tablosundan JOIN ile gelir.
 */
class TestSonucu {
    constructor({
        id = generateGuid(),
        testId = '',
        videoId = '',
        personelId = '',
        puan = 0,
        dogruSayisi = 0,
        yanlisSayisi = 0,
        toplamSoru = 0,
        gectiMi = false,
        verilenCevaplar = [],
        tamamlanmaTarihi = new Date().toISOString(),
        // Normalized JOIN türetilmiş alanlar:
        personelAd = '',
        personelDepartman = '',
        testBaslik = ''
    } = {}) {
        this.id = id;
        this.testId = testId;
        this.videoId = videoId;
        this.personelId = personelId;
        this.puan = Math.round(Number(puan) || 0);
        this.dogruSayisi = Number(dogruSayisi) || 0;
        this.yanlisSayisi = Number(yanlisSayisi) || 0;
        this.toplamSoru = Number(toplamSoru) || 0;
        this.gectiMi = Boolean(gectiMi);
        this.verilenCevaplar = Array.isArray(verilenCevaplar) ? verilenCevaplar : [];
        this.tamamlanmaTarihi = tamamlanmaTarihi;

        this.personelAd = personelAd;
        this.personelDepartman = personelDepartman;
        this.testBaslik = testBaslik;
    }
}
