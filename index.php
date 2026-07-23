<?php
$uploadDir = __DIR__ . '/uploads/';

// 1. İNDİRME VE OTOMATİK TEMİZLEME
if (isset($_GET['download'])) {
    $file = basename($_GET['download']);
    $filePath = $uploadDir . $file;

    if (file_exists($filePath) && pathinfo($filePath, PATHINFO_EXTENSION) === 'tls') {
        header('Content-Description: File Transfer');
        header('Content-Type: application/octet-stream');
        header('Content-Disposition: attachment; filename="' . $file . '"');
        header('Expires: 0');
        header('Cache-Control: must-revalidate');
        header('Pragma: public');
        header('Content-Length: ' . filesize($filePath));
        
        readfile($filePath);

        // İndirme sonrası dosyaları sil
        @unlink($filePath);

        $baseName = pathinfo($file, PATHINFO_FILENAME);
        $files = glob($uploadDir . $baseName . '.*');
        foreach ($files as $f) {
            @unlink($f);
        }

        exit;
    } else {
        die("Hata: Dosya bulunamadı veya daha önce indirildiği için silindi.");
    }
}

// 2. DÖNÜŞTÜRME
$message = "";
$downloadFile = "";

if ($_SERVER['REQUEST_METHOD'] === 'POST' && isset($_FILES['audio_file'])) {
    if ($_FILES['audio_file']['error'] !== UPLOAD_ERR_OK) {
        $message = "Dosya yüklenirken bir sorun oluştu.";
    } else {
        if (!is_dir($uploadDir)) {
            mkdir($uploadDir, 0775, true);
        }

        $tmpPath = $_FILES['audio_file']['tmp_name'];
        $originalName = $_FILES['audio_file']['name'];
        $fileNameNoExt = pathinfo($originalName, PATHINFO_FILENAME);
        $ext = pathinfo($originalName, PATHINFO_EXTENSION);
        
        $uploadedOriginalPath = $uploadDir . $fileNameNoExt . '.' . $ext;
        move_uploaded_file($tmpPath, $uploadedOriginalPath);

        $tlsFileName = $fileNameNoExt . '.tls';
        $tlsTargetPath = $uploadDir . $tlsFileName;

        try {
            $rawBytes = file_get_contents($uploadedOriginalPath);

            $offset = 0;
            if (strlen($rawBytes) > 44 && substr($rawBytes, 0, 4) === 'RIFF') {
                $offset = 44;
            } elseif (strlen($rawBytes) > 10 && substr($rawBytes, 0, 3) === 'ID3') {
                $tagSize = ((ord($rawBytes[6]) & 0x7F) << 21) |
                           ((ord($rawBytes[7]) & 0x7F) << 14) |
                           ((ord($rawBytes[8]) & 0x7F) << 7) |
                            (ord($rawBytes[9]) & 0x7F);
                $offset = 10 + $tagSize;
            }

            $pcmData = substr($rawBytes, $offset);
            if ($pcmData === false || strlen($pcmData) === 0) {
                $pcmData = $rawBytes;
            }

            // Bayt Dönüştürme Mantığı (0x5A)
            $key = 0x5A;
            $dataLength = strlen($pcmData);
            for ($i = 0; $i < $dataLength; $i++) {
                $pcmData[$i] = chr(ord($pcmData[$i]) ^ $key);
            }

            // TLS2 Başlığı
            $header = "TLS2";
            $header .= pack("V", 44100);
            $header .= pack("v", 2);
            $header .= pack("v", 16);
            $header .= pack("V", $dataLength);

            $finalContent = $header . $pcmData;
            if (file_put_contents($tlsTargetPath, $finalContent) !== false) {
                $message = "Ses dosyası .tls formatına başarıyla dönüştürüldü!";
                $downloadFile = $tlsFileName;
            } else {
                $message = "Hata: Dosya oluşturulamadı.";
            }

        } catch (Exception $e) {
            $message = "Hata: " . $e->getMessage();
        }
    }
}
?>

<!DOCTYPE html>
<html lang="tr">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Tıla Software - TLS Audio Engine</title>
    <style>
        * { box-sizing: border-box; margin: 0; padding: 0; }
        body { font-family: 'Segoe UI', Arial, sans-serif; background: #0f0f12; color: #e1e1e6; line-height: 1.6; padding: 40px 20px; }
        .container { max-width: 1100px; margin: 0 auto; }
        
        header { text-align: center; margin-bottom: 40px; }
        header h1 { font-size: 2.2rem; color: #0078d4; margin-bottom: 8px; }
        header p { color: #8a8a93; font-size: 1.05rem; }

        .layout { display: flex; flex-wrap: wrap; gap: 30px; }
        
        /* Sol Taraf: Format Detayları ve Anlatım */
        .info-panel { flex: 1; min-width: 320px; background: #18181c; border-radius: 12px; padding: 30px; border: 1px solid #282830; }
        .info-panel h2 { color: #fff; font-size: 1.4rem; margin-bottom: 15px; border-bottom: 2px solid #0078d4; padding-bottom: 8px; display: inline-block; }
        .info-panel p { color: #b3b3bc; margin-bottom: 15px; font-size: 0.95rem; }
        
        .feature-list { list-style: none; margin: 20px 0; }
        .feature-list li { margin-bottom: 12px; padding-left: 25px; position: relative; color: #d0d0d8; font-size: 0.95rem; }
        .feature-list li::before { content: "✔"; position: absolute; left: 0; color: #0078d4; font-weight: bold; }

        .tech-box { background: #111114; padding: 15px; border-radius: 8px; border-left: 4px solid #0078d4; margin-top: 20px; }
        .tech-box h4 { color: #fff; font-size: 0.9rem; margin-bottom: 5px; }
        .tech-box p { font-size: 0.85rem; color: #8a8a93; margin: 0; }

        /* Sağ Taraf: Dönüştürücü Paneli */
        .convert-panel { flex: 1; min-width: 320px; background: #18181c; border-radius: 12px; padding: 30px; border: 1px solid #282830; text-align: center; display: flex; flex-direction: column; justify-content: space-between; }
        .convert-panel h2 { color: #fff; font-size: 1.4rem; margin-bottom: 10px; }
        
        .file-input-wrapper { margin: 15px 0; }
        input[type="file"] { display: none; }
        .file-label { display: block; background: #222228; border: 2px dashed #0078d4; padding: 20px; border-radius: 10px; cursor: pointer; transition: 0.3s; color: #aaa; }
        .file-label:hover { background: #2a2a32; color: #fff; }

        .btn-submit { background: #0078d4; color: #fff; border: none; padding: 14px 28px; border-radius: 8px; cursor: pointer; font-size: 1rem; font-weight: bold; width: 100%; transition: 0.2s; }
        .btn-submit:hover { background: #005a9e; }

        .status-msg { margin-top: 15px; padding: 10px; border-radius: 6px; background: #1c2b20; color: #00ff7f; font-size: 0.85rem; }
        
        .btn-download { display: inline-block; background: #28a745; color: #fff; text-decoration: none; padding: 12px 24px; border-radius: 8px; margin-top: 10px; font-weight: bold; transition: 0.2s; width: 100%; }
        .btn-download:hover { background: #218838; }

        /* EXE İndirme Kutusu */
        .app-download-box { margin-top: 25px; padding-top: 20px; border-top: 1px solid #282830; text-align: center; }
        .app-download-box p { font-size: 0.85rem; color: #8a8a93; margin-bottom: 10px; }
        .btn-exe { display: inline-block; background: #2d2d30; color: #fff; border: 1px solid #0078d4; text-decoration: none; padding: 10px 20px; border-radius: 8px; font-weight: bold; font-size: 0.9rem; transition: 0.2s; width: 100%; }
        .btn-exe:hover { background: #0078d4; }

        footer { text-align: center; margin-top: 50px; color: #555560; font-size: 0.85rem; }
    </style>
</head>
<body>

<div class="container">
    <header>
        <h1>Tıla Software TLS Formatı</h1>
        <p>Music format and player</p>
    </header>

    <div class="layout">
        <!-- SOL KOLON: FORMAT BİLGİSİ -->
        <div class="info-panel">
            <h2>TLS Formatı Nedir?</h2>
            <p><b>.TLS</b> (Tıla Audio Format), ses verilerini standart medya oynatıcılarının doğrudan okuyamayacağı özel bir başlık mimarisiyle işleyen bağımsız bir ses formatıdır.</p>
            
            <ul class="feature-list">
                <li><b>Özel Başlık İmzası:</b> Her `.tls` dosyası ilk baytlarında kendine özgü `TLS2` doğrulama kodunu taşır.</li>
                <li><b>Kendi Oynatıcısı:</b> Dosya yapısı ve ses bayt dizilimi standart olmadığı için sadece Tıla Ses Oynatıcısı ile açılabilir.</li>
                <li><b>Yüksek Ses Kalitesi:</b> Ses örnekleme oranını (44.100 Hz Stereo) koruyarak ses kalitesinde kayıp yaşatmaz.</li>
                <li><b>Güvenli İndirme:</b> Dönüştürülen dosyalar bilgisayarınıza indirildiği an sunucudaki geçici kayıtları tamamen silinir.</li>
            </ul>

            <div class="tech-box">
                <h4>Nasıl Dinlenir?</h4>
                <p>İndirdiğiniz `.tls` uzantılı dosyaları masaüstünüzde bulunan <b>Tıla Ses Oynatıcı</b> uygulamasının üzerine sürükleyerek veya uygulama içinden açarak dinleyebilirsiniz.</p>
            </div>
        </div>

        <!-- SAĞ KOLON: DÖNÜŞTÜRÜCÜ VE EXE İNDİRME PANELİ -->
        <div class="convert-panel">
            <div>
                <h2>Ses Dönüştürücü</h2>
                <p style="color: #8a8a93; font-size: 0.85rem; margin-bottom: 15px;">Bilgisayarınızdaki MP3 veya WAV dosyasını seçip hızlıca .tls formatına dönüştürün.</p>

                <form action="" method="POST" enctype="multipart/form-data">
                    <div class="file-input-wrapper">
                        <label for="audio_file" class="file-label" id="fileLabel">
                            📁 Ses Dosyası Seçin<br>
                            <span style="font-size: 0.75rem; color: #666;">(MP3, WAV, OGG)</span>
                        </label>
                        <input type="file" name="audio_file" id="audio_file" required onchange="updateFileName(this)">
                    </div>

                    <button type="submit" class="btn-submit">.TLS Formatına Dönüştür</button>
                </form>

                <?php if ($message): ?>
                    <div class="status-msg"><?= htmlspecialchars($message) ?></div>
                <?php endif; ?>

                <?php if ($downloadFile): ?>
                    <a href="?download=<?= urlencode($downloadFile) ?>" class="btn-download">⬇ .TLS Dosyasını İndir</a>
                <?php endif; ?>
            </div>

            <!-- MASAÜSTÜ OYNATICI İNDİRME ALANI -->
            <div class="app-download-box">
                <p>.TLS müziklerini bilgisayarınızda çalmak için:</p>
                <a href="TilaPlayerGUI.exe" class="btn-exe" download>💻 Tıla Ses Oynatıcı'yı İndir (.exe)</a>
            </div>
        </div>
    </div>

    <footer>
        &copy; <?= date('Y') ?> Tıla Software. All rights reserved.
    </footer>
</div>

<script>
function updateFileName(input) {
    var label = document.getElementById('fileLabel');
    if (input.files && input.files[0]) {
        label.innerHTML = "🎵 " + input.files[0].name;
        label.style.borderColor = "#28a745";
        label.style.color = "#fff";
    }
}
</script>

</body>
</html>
