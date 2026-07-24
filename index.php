<?php
$uploadDir = __DIR__ . '/uploads/';
$reportsFile = __DIR__ . '/reports.json';

// 1. İNDİRME MANTIĞI
if (isset($_GET['download'])) {
    $file = basename($_GET['download']);
    $filePath = $uploadDir . $file;
    $ext = pathinfo($filePath, PATHINFO_EXTENSION);

    if (file_exists($filePath) && ($ext === 'tls' || $ext === 'tlv')) {
        header('Content-Description: File Transfer');
        header('Content-Type: application/octet-stream');
        header('Content-Disposition: attachment; filename="' . $file . '"');
        header('Expires: 0');
        header('Cache-Control: must-revalidate');
        header('Pragma: public');
        header('Content-Length: ' . filesize($filePath));
        
        readfile($filePath);

        @unlink($filePath);
        $baseName = pathinfo($file, PATHINFO_FILENAME);
        $files = glob($uploadDir . $baseName . '.*');
        foreach ($files as $f) { @unlink($f); }

        exit;
    } else {
        die("Hata: Dosya bulunamadı veya silinmiş.");
    }
}

// 2. DÖNÜŞTÜRME MANTIĞI (.TLS ve .TLV)
$message = "";
$downloadFile = "";

if ($_SERVER['REQUEST_METHOD'] === 'POST' && isset($_POST['action']) && $_POST['action'] === 'convert') {
    if (!isset($_FILES['media_file']) || $_FILES['media_file']['error'] !== UPLOAD_ERR_OK) {
        $message = "Dosya yüklenirken bir sorun oluştu.";
    } else {
        if (!is_dir($uploadDir)) {
            mkdir($uploadDir, 0775, true);
        }

        $tmpPath = $_FILES['media_file']['tmp_name'];
        $originalName = $_FILES['media_file']['name'];
        $fileNameNoExt = pathinfo($originalName, PATHINFO_FILENAME);
        $formatType = $_POST['format_type'] ?? 'tls';

        $rawBytes = file_get_contents($tmpPath);
        $dataLength = strlen($rawBytes);

        // XOR Şifreleme (0x5A)
        $key = 0x5A;
        $pcmData = $rawBytes;
        for ($i = 0; $i < $dataLength; $i++) {
            $pcmData[$i] = chr(ord($pcmData[$i]) ^ $key);
        }

        if ($formatType === 'tlv') {
            $outputFile = $fileNameNoExt . '.tlv';
            $header = "TLV1";
            $header .= pack("V", 1920);
            $header .= pack("V", 1080);
            $header .= pack("V", 0);
            $finalContent = $header . $pcmData;
        } else {
            $outputFile = $fileNameNoExt . '.tls';
            $header = "TLS2";
            $header .= pack("V", 44100);
            $header .= pack("v", 2);
            $header .= pack("v", 16);
            $header .= pack("V", $dataLength);
            $finalContent = $header . $pcmData;
        }

        $targetPath = $uploadDir . $outputFile;
        if (file_put_contents($targetPath, $finalContent) !== false) {
            $message = "Dosyanız " . strtoupper($formatType) . " formatına başarıyla dönüştürüldü!";
            $downloadFile = $outputFile;
        } else {
            $message = "Hata: Dosya oluşturulamadı.";
        }
    }
}

// 3. HATA BİLDİRİM MANTIĞI
$bugMessage = "";
if ($_SERVER['REQUEST_METHOD'] === 'POST' && isset($_POST['action']) && $_POST['action'] === 'report_bug') {
    $reporterName = trim($_POST['reporter_name'] ?? 'Anonim');
    $bugDesc = trim($_POST['bug_description'] ?? '');

    if (!empty($bugDesc)) {
        $reports = [];
        if (file_exists($reportsFile)) {
            $reports = json_decode(file_get_contents($reportsFile), true) ?? [];
        }

        $reports[] = [
            'name' => htmlspecialchars($reporterName),
            'description' => htmlspecialchars($bugDesc),
            'date' => date('Y-m-d H:i:s')
        ];

        file_put_contents($reportsFile, json_encode($reports, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE));
        $bugMessage = "Hata bildirimin başarıyla gönderildi. Teşekkürler!";
    } else {
        $bugMessage = "Lütfen hata açıklamasını boş bırakma.";
    }
}
?>

<!DOCTYPE html>
<html lang="tr">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Tıla Software - TLS & TLV Engine</title>
    <style>
        * { box-sizing: border-box; margin: 0; padding: 0; }
        body { font-family: 'Segoe UI', Arial, sans-serif; background: #0f0f12; color: #e1e1e6; line-height: 1.6; padding: 40px 20px; }
        .container { max-width: 1100px; margin: 0 auto; }
        
        header { text-align: center; margin-bottom: 40px; }
        header h1 { font-size: 2.2rem; color: #0078d4; margin-bottom: 8px; }
        header p { color: #8a8a93; font-size: 1.05rem; }

        .layout { display: flex; flex-wrap: wrap; gap: 30px; margin-bottom: 30px; }
        
        .info-panel { flex: 1; min-width: 320px; background: #18181c; border-radius: 12px; padding: 30px; border: 1px solid #282830; }
        .info-panel h2 { color: #fff; font-size: 1.4rem; margin-bottom: 15px; border-bottom: 2px solid #0078d4; padding-bottom: 8px; display: inline-block; }
        .info-panel p { color: #b3b3bc; margin-bottom: 15px; font-size: 0.95rem; }
        
        .feature-list { list-style: none; margin: 20px 0; }
        .feature-list li { margin-bottom: 12px; padding-left: 25px; position: relative; color: #d0d0d8; font-size: 0.95rem; }
        .feature-list li::before { content: "✔"; position: absolute; left: 0; color: #0078d4; font-weight: bold; }

        .convert-panel { flex: 1; min-width: 320px; background: #18181c; border-radius: 12px; padding: 30px; border: 1px solid #282830; text-align: center; display: flex; flex-direction: column; justify-content: space-between; }
        .convert-panel h2 { color: #fff; font-size: 1.4rem; margin-bottom: 10px; }
        
        select { width: 100%; padding: 10px; background: #222228; color: #fff; border: 1px solid #0078d4; border-radius: 6px; margin-bottom: 15px; }

        .file-input-wrapper { margin: 15px 0; }
        input[type="file"] { display: none; }
        .file-label { display: block; background: #222228; border: 2px dashed #0078d4; padding: 20px; border-radius: 10px; cursor: pointer; transition: 0.3s; color: #aaa; }
        .file-label:hover { background: #2a2a32; color: #fff; }

        .btn-submit { background: #0078d4; color: #fff; border: none; padding: 14px 28px; border-radius: 8px; cursor: pointer; font-size: 1rem; font-weight: bold; width: 100%; transition: 0.2s; }
        .btn-submit:hover { background: #005a9e; }

        .status-msg { margin-top: 15px; padding: 10px; border-radius: 6px; background: #1c2b20; color: #00ff7f; font-size: 0.85rem; }
        
        .btn-download { display: inline-block; background: #28a745; color: #fff; text-decoration: none; padding: 12px 24px; border-radius: 8px; margin-top: 10px; font-weight: bold; transition: 0.2s; width: 100%; }
        .btn-download:hover { background: #218838; }

        .app-download-box { margin-top: 25px; padding-top: 20px; border-top: 1px solid #282830; text-align: center; }
        .btn-exe { display: inline-block; background: #2d2d30; color: #fff; border: 1px solid #0078d4; text-decoration: none; padding: 10px 20px; border-radius: 8px; font-weight: bold; font-size: 0.9rem; transition: 0.2s; width: 100%; }
        .btn-exe:hover { background: #0078d4; }

        /* Hata Bildirim Kutusu Stili */
        .bug-report-panel { background: #18181c; border-radius: 12px; padding: 30px; border: 1px solid #282830; margin-top: 30px; }
        .bug-report-panel h2 { color: #fff; font-size: 1.4rem; margin-bottom: 10px; }
        .bug-report-panel input[type="text"], .bug-report-panel textarea { width: 100%; padding: 10px; margin-bottom: 15px; background: #222228; color: #fff; border: 1px solid #0078d4; border-radius: 6px; font-family: inherit; }
        .bug-report-panel textarea { height: 100px; resize: none; }
        .bug-msg { margin-top: 15px; padding: 10px; border-radius: 6px; background: #1c2b20; color: #00ff7f; font-size: 0.85rem; }

        footer { text-align: center; margin-top: 50px; color: #555560; font-size: 0.85rem; }
    </style>
</head>
<body>

<div class="container">
    <header>
        <h1>Tıla Software TLS & TLV Teknolojisi</h1>
        <p>Music & Video format and player</p>
    </header>

    <div class="layout">
        <!-- BİLGİ PANELİ -->
        <div class="info-panel">
            <h2>Tıla Medya Biçimleri</h2>
            <p><b>.TLS</b> (Tıla Audio) ve <b>.TLV</b> (Tıla Video), Tıla Software tarafından geliştirilen bağımsız ikili medya formatlarıdır.</p>
            
            <ul class="feature-list">
                <li><b>Çift Format Desteği:</b> Ses için `.tls`, video yayınları için `.tlv` ikili mimarisi.</li>
                <li><b>Tek Oynatıcı:</b> Her iki format da tek bir Tıla Medya Oynatıcı uygulamasıyla çalışır.</li>
                <li><b>Gelişmiş Başlık Koruması:</b> `TLS2` ve `TLV1` doğrulamalı ikili şifreleme.</li>
                <li><b>Güvenli Temizlik:</b> İndirilen medya dosyaları sunucuda iz bırakmadan anında temizlenir.</li>
            </ul>
        </div>

        <!-- DÖNÜŞTÜRME PANELİ -->
        <div class="convert-panel">
            <div>
                <h2>Medya Dönüştürücü</h2>
                <p style="color: #8a8a93; font-size: 0.85rem; margin-bottom: 15px;">Dönüştürmek istediğiniz format türünü seçin:</p>

                <form action="" method="POST" enctype="multipart/form-data">
                    <input type="hidden" name="action" value="convert">
                    <select name="format_type">
                        <option value="tls">🎵 .TLS (Ses Formatına Dönüştür)</option>
                        <option value="tlv">🎬 .TLV (Video Formatına Dönüştür)</option>
                    </select>

                    <div class="file-input-wrapper">
                        <label for="media_file" class="file-label" id="fileLabel">
                            📁 Medya Dosyası Seçin
                        </label>
                        <input type="file" name="media_file" id="media_file" required onchange="updateFileName(this)">
                    </div>

                    <button type="submit" class="btn-submit">Dönüştür ve İndir</button>
                </form>

                <?php if ($message && isset($_POST['action']) && $_POST['action'] === 'convert'): ?>
                    <div class="status-msg"><?= htmlspecialchars($message) ?></div>
                <?php endif; ?>

                <?php if ($downloadFile): ?>
                    <a href="?download=<?= urlencode($downloadFile) ?>" class="btn-download">⬇ İndir</a>
                <?php endif; ?>
            </div>

            <div class="app-download-box">
                <a href="https://github.com/atlasalan/TilaSoftware.TLS/raw/main/TilaPlayerGUI.exe" class="btn-exe" target="_blank">💻 Tıla Medya Oynatıcı'yı İndir (.exe)</a>
            </div>
        </div>
    </div>

    <!-- HATA BİLDİRİM PANELİ -->
    <div class="bug-report-panel">
        <h2>🛠 Hata / Sorun Bildir</h2>
        <p style="color: #8a8a93; font-size: 0.85rem; margin-bottom: 15px;">Uygulamada veya formatta bir hata ile karşılaştıysan bize bildirebilirsin:</p>
        <form action="" method="POST">
            <input type="hidden" name="action" value="report_bug">
            <input type="text" name="reporter_name" placeholder="Adın veya Rumuzun (İsteğe bağlı)">
            <textarea name="bug_description" placeholder="Yaşadığın sorunu veya hatayı detaylı anlat..." required></textarea>
            <button type="submit" class="btn-submit">Bildirimi Gönder</button>
        </form>
        <?php if ($bugMessage && isset($_POST['action']) && $_POST['action'] === 'report_bug'): ?>
            <div class="bug-msg"><?= htmlspecialchars($bugMessage) ?></div>
        <?php endif; ?>
    </div>

    <footer>
        &copy; <?= date('Y') ?> Tıla Software. All rights reserved.
    </footer>
</div>

<script>
function updateFileName(input) {
    var label = document.getElementById('fileLabel');
    if (input.files && input.files[0]) {
        label.innerHTML = "📄 " + input.files[0].name;
        label.style.borderColor = "#28a745";
        label.style.color = "#fff";
    }
}
</script>

</body>
</html>
