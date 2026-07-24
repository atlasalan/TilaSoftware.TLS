<?php
session_start();

// Dil seçimi yönetimi
if (isset($_GET['lang']) && in_array($_GET['lang'], ['tr', 'en'])) {
    $_SESSION['lang'] = $_GET['lang'];
}
$lang = $_SESSION['lang'] ?? 'tr';

// Çeviri Sözlüğü
$translations = [
    'tr' => [
        'title' => 'Tıla Software TLS & TLV Teknolojisi',
        'subtitle' => 'Müzik ve Video formatı ve oynatıcısı',
        'formats_title' => 'Tıla Medya Biçimleri',
        'formats_desc' => '<b>.TLS</b> (Tıla Audio) ve <b>.TLV</b> (Tıla Video), Tıla Software tarafından geliştirilen bağımsız ikili medya formatlarıdır.',
        'feat_1' => '<b>Çift Format Desteği:</b> Ses için <code>.tls</code>, video yayınları için <code>.tlv</code> ikili mimarisi.',
        'feat_2' => '<b>Tek Oynatıcı:</b> Her iki format da tek bir <b>Tıla Medya Oynatıcı</b> uygulamasıyla çalışır.',
        'feat_3' => '<b>Gelişmiş Başlık Koruması:</b> <code>TLS2</code> ve <code>TLV1</code> doğrulamalı ikili şifreleme.',
        'feat_4' => '<b>Güvenli Temizlik:</b> İndirilen medya dosyaları sunucuda iz bırakmadan anında temizlenir.',
        'converter_title' => 'Medya Dönüştürücü',
        'converter_desc' => 'Dönüştürmek istediğiniz format türünü seçin:',
        'opt_tls' => '🎵 .TLS (Ses Formatına Dönüştür)',
        'opt_tlv' => '🎬 .TLV (Video Formatına Dönüştür)',
        'file_label' => '📁 Medya Dosyası Seçin',
        'file_selected' => '📄 Dosya seçildi: ',
        'file_default' => '📁 Medya Dosyası Seçin',
        'btn_convert' => 'Dönüştür ve İndir',
        'btn_download' => '⬇ İndir',
        'btn_exe' => '💻 Tıla Medya Oynatıcı\'yı İndir (.exe)',
        'bug_title' => '🛠 Hata / Sorun Bildir',
        'bug_desc' => 'Uygulamada veya formatta bir hata ile karşılaştıysan bize bildirebilirsin:',
        'bug_name' => 'Adın veya Rumuzun (İsteğe bağlı)',
        'bug_text' => 'Yaşadığın sorunu veya hatayı detaylı anlat...',
        'bug_btn' => 'Bildirimi Gönder',
        'footer' => 'Tüm hakları saklıdır.',
        'success_conv' => 'Dosyanız başarıyla dönüştürüldü!',
        'success_bug' => 'Hata bildirimin başarıyla gönderildi. Teşekkürler!'
    ],
    'en' => [
        'title' => 'Tila Software TLS & TLV Technology',
        'subtitle' => 'Music & Video format and player',
        'formats_title' => 'Tila Media Formats',
        'formats_desc' => '<b>.TLS</b> (Tıla Audio) and <b>.TLV</b> (Tıla Video) are independent binary media formats developed by Tila Software.',
        'feat_1' => '<b>Dual Format Support:</b> Binary architecture using <code>.tls</code> for audio and <code>.tlv</code> for video.',
        'feat_2' => '<b>Single Player:</b> Both formats run seamlessly on the single <b>Tila Media Player</b> app.',
        'feat_3' => '<b>Advanced Header Protection:</b> Binary encryption validated with <code>TLS2</code> and <code>TLV1</code> headers.',
        'feat_4' => '<b>Secure Cleanup:</b> Downloaded media files are instantly wiped from the server leaving no trace.',
        'converter_title' => 'Media Converter',
        'converter_desc' => 'Select the format type you want to convert to:',
        'opt_tls' => '🎵 .TLS (Convert to Audio)',
        'opt_tlv' => '🎬 .TLV (Convert to Video)',
        'file_label' => '📁 Choose Media File',
        'file_selected' => '📄 File selected: ',
        'file_default' => '📁 Choose Media File',
        'btn_convert' => 'Convert & Download',
        'btn_download' => '⬇ Download',
        'btn_exe' => '💻 Download Tila Media Player (.exe)',
        'bug_title' => '🛠 Report a Bug / Issue',
        'bug_desc' => 'If you encounter any bugs in the app or format, let us know:',
        'bug_name' => 'Your Name or Alias (Optional)',
        'bug_text' => 'Describe the issue or error in detail...',
        'bug_btn' => 'Send Report',
        'footer' => 'All rights reserved.',
        'success_conv' => 'Your file has been successfully converted!',
        'success_bug' => 'Your bug report has been successfully sent. Thanks!'
    ]
];

$t = $translations[$lang];

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
            $message = $t['success_conv'];
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
        $bugMessage = $t['success_bug'];
    } else {
        $bugMessage = "Lütfen hata açıklamasını boş bırakma.";
    }
}
?>

<!DOCTYPE html>
<html lang="<?= $lang ?>">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title><?= $t['title'] ?></title>
    <style>
        * { box-sizing: border-box; margin: 0; padding: 0; }
        body { font-family: 'Segoe UI', Arial, sans-serif; background: #0f0f12; color: #e1e1e6; line-height: 1.6; padding: 40px 20px; }
        .container { max-width: 1100px; margin: 0 auto; position: relative; }
        
        /* Dil Seçici Stili */
        .lang-switcher { position: absolute; top: 0; right: 0; display: flex; gap: 8px; }
        .lang-switcher a { background: #18181c; color: #8a8a93; border: 1px solid #282830; padding: 6px 12px; border-radius: 6px; text-decoration: none; font-size: 0.85rem; font-weight: bold; transition: 0.2s; }
        .lang-switcher a.active, .lang-switcher a:hover { background: #0078d4; color: #fff; border-color: #0078d4; }

        header { text-align: center; margin-bottom: 40px; margin-top: 20px; }
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
    <!-- DİL DEĞİŞTİRME BUTONLARI -->
    <div class="lang-switcher">
        <a href="?lang=tr" class="<?= $lang === 'tr' ? 'active' : '' ?>">TR</a>
        <a href="?lang=en" class="<?= $lang === 'en' ? 'active' : '' ?>">EN</a>
    </div>

    <header>
        <h1>Tıla Software TLS & TLV Teknolojisi</h1>
        <p><?= $t['subtitle'] ?></p>
    </header>

    <div class="layout">
        <!-- BİLGİ PANELİ -->
        <div class="info-panel">
            <h2><?= $t['formats_title'] ?></h2>
            <p><?= $t['formats_desc'] ?></p>
            
            <ul class="feature-list">
                <li><?= $t['feat_1'] ?></li>
                <li><?= $t['feat_2'] ?></li>
                <li><?= $t['feat_3'] ?></li>
                <li><?= $t['feat_4'] ?></li>
            </ul>
        </div>

        <!-- DÖNÜŞTÜRME PANELİ -->
        <div class="convert-panel">
            <div>
                <h2><?= $t['converter_title'] ?></h2>
                <p style="color: #8a8a93; font-size: 0.85rem; margin-bottom: 15px;"><?= $t['converter_desc'] ?></p>

                <form action="" method="POST" enctype="multipart/form-data">
                    <input type="hidden" name="action" value="convert">
                    <select name="format_type">
                        <option value="tls"><?= $t['opt_tls'] ?></option>
                        <option value="tlv"><?= $t['opt_tlv'] ?></option>
                    </select>

                    <div class="file-input-wrapper">
                        <label for="media_file" class="file-label" id="fileLabel">
                            <?= $t['file_label'] ?>
                        </label>
                        <input type="file" name="media_file" id="media_file" required onchange="updateFileName(this, '<?= $t['file_selected'] ?>')">
                    </div>

                    <button type="submit" class="btn-submit"><?= $t['btn_convert'] ?></button>
                </form>

                <?php if ($message && isset($_POST['action']) && $_POST['action'] === 'convert'): ?>
                    <div class="status-msg"><?= htmlspecialchars($message) ?></div>
                <?php endif; ?>

                <?php if ($downloadFile): ?>
                    <a href="?download=<?= urlencode($downloadFile) ?>" class="btn-download"><?= $t['btn_download'] ?></a>
                <?php endif; ?>
            </div>

            <div class="app-download-box">
                <a href="https://github.com/atlasalan/TilaSoftware.TLS/raw/main/TilaPlayerGUI.exe" class="btn-exe" target="_blank"><?= $t['btn_exe'] ?></a>
            </div>
        </div>
    </div>

    <!-- HATA BİLDİRİM PANELİ -->
    <div class="bug-report-panel">
        <h2><?= $t['bug_title'] ?></h2>
        <p style="color: #8a8a93; font-size: 0.85rem; margin-bottom: 15px;"><?= $t['bug_desc'] ?></p>
        <form action="" method="POST">
            <input type="hidden" name="action" value="report_bug">
            <input type="text" name="reporter_name" placeholder="<?= $t['bug_name'] ?>">
            <textarea name="bug_description" placeholder="<?= $t['bug_text'] ?>" required></textarea>
            <button type="submit" class="btn-submit"><?= $t['bug_btn'] ?></button>
        </form>
        <?php if ($bugMessage && isset($_POST['action']) && $_POST['action'] === 'report_bug'): ?>
            <div class="bug-msg"><?= htmlspecialchars($bugMessage) ?></div>
        <?php endif; ?>
    </div>

    <footer>
        &copy; <?= date('Y') ?> Tıla Software. <?= $t['footer'] ?>
    </footer>
</div>

<script>
function updateFileName(input, prefix) {
    var label = document.getElementById('fileLabel');
    if (input.files && input.files[0]) {
        label.innerHTML = prefix + input.files[0].name;
        label.style.borderColor = "#28a745";
        label.style.color = "#fff";
    }
}
</script>

</body>
</html>
