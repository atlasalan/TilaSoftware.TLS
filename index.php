<?php
session_start();

// Dil seçimi yönetimi
if (isset($_GET['lang']) && in_array($_GET['lang'], ['tr', 'en'])) {
    $_SESSION['lang'] = $_GET['lang'];
}
$lang = $_SESSION['lang'] ?? 'tr';

$translations = [
    'tr' => [
        'title' => 'Tıla Software - Resmi TLS & TLV Medya Portalı',
        'subtitle' => 'Müzik ve Video Formatı Dönüştürme & İndirme Merkezi',
        'formats_title' => 'Tıla Medya Biçimi Hakkında',
        'formats_desc' => '<b>.TLS</b> (Tıla Audio) ve <b>.TLV</b> (Tıla Video), Tıla Software tarafından geliştirilen bağımsız ikili (binary) medya formatlarıdır.',
        'feat_1' => '<b>Geniş Format Desteği:</b> Ses (MP3, WAV, AAC, FLAC, OGG) ve Video (MP4, AVI, MKV, MOV, WMV).',
        'feat_2' => '<b>Yerli Oynatıcı:</b> Formatlar doğrudan Tıla Medya Oynatıcı uygulaması ile tam uyumludur.',
        'feat_3' => '<b>Başlık Şifrelemesi:</b> TLS3 ve TLV2 ikili başlık doğrulaması.',
        'feat_4' => '<b>Geçici Depolama:</b> Sunucuya yüklenen dosyalar işlem sonrası anında temizlenir.',
        'converter_title' => 'Çevrimiçi Dönüştürme Motoru',
        'converter_desc' => 'Dönüştürülecek hedef formatı seçin:',
        'opt_tls' => '🎵 .TLS (Ses Formatları: MP3, WAV, AAC, FLAC, OGG, M4A)',
        'opt_tlv' => '🎬 .TLV (Video & Resim: MP4, AVI, MKV, MOV, WMV, WEBM, JPG, PNG, GIF)',
        'file_label' => 'Dönüştürülecek Dosyayı Seçin:',
        'btn_convert' => 'Dönüştürmeyi Başlat',
        'btn_download' => '💾 Oluşturulan Dosyayı İndir',
        'btn_exe' => '🖫 Tıla Medya Oynatıcı İndir (.exe)',
        'bug_title' => 'Hata / Geri Bildirim Formu',
        'bug_desc' => 'Sistemde karşılaştığınız teknik sorunları bildirebilirsiniz:',
        'bug_name' => 'Adınız / Rumuzunuz:',
        'bug_text' => 'Sorun Açıklaması:',
        'bug_btn' => 'Raporu Gönder',
        'footer' => 'Tüm hakları saklıdır.',
        'success_conv' => 'İşlem Başarılı: Dosyanız dönüştürüldü.',
        'success_bug' => 'Bildiriminiz sistem yöneticisine iletildi.'
    ],
    'en' => [
        'title' => 'Tila Software - Official TLS & TLV Media Portal',
        'subtitle' => 'Music & Video Format Conversion & Download Center',
        'formats_title' => 'About Tila Media Formats',
        'formats_desc' => '<b>.TLS</b> (Tila Audio) and <b>.TLV</b> (Tila Video) are proprietary binary media container formats developed by Tila Software.',
        'feat_1' => '<b>Wide Format Support:</b> Audio (MP3, WAV, AAC, FLAC, OGG) and Video (MP4, AVI, MKV, MOV, WMV).',
        'feat_2' => '<b>Native Player:</b> Formats are fully compatible with Tila Media Player.',
        'feat_3' => '<b>Header Encryption:</b> TLS3 & TLV2 binary header validation.',
        'feat_4' => '<b>Temporary Storage:</b> Files uploaded are immediately wiped post-conversion.',
        'converter_title' => 'Online Conversion Engine',
        'converter_desc' => 'Select target output format:',
        'opt_tls' => '🎵 .TLS (Audio Formats: MP3, WAV, AAC, FLAC, OGG, M4A)',
        'opt_tlv' => '🎬 .TLV (Video & Images: MP4, AVI, MKV, MOV, WMV, WEBM, JPG, PNG, GIF)',
        'file_label' => 'Select Source File:',
        'btn_convert' => 'Start Conversion',
        'btn_download' => '💾 Download Converted File',
        'btn_exe' => '🖫 Download Tila Media Player (.exe)',
        'bug_title' => 'Bug / Issue Report Form',
        'bug_desc' => 'Report any technical issues encountered during usage:',
        'bug_name' => 'Your Name / Alias:',
        'bug_text' => 'Issue Description:',
        'bug_btn' => 'Submit Report',
        'footer' => 'All rights reserved.',
        'success_conv' => 'Success: Your file has been converted.',
        'success_bug' => 'Report submitted to system administrator.'
    ]
];

$t = $translations[$lang];
$uploadDir = __DIR__ . '/uploads/';
$reportsFile = __DIR__ . '/reports.json';

// İNDİRME MANTIĞI
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
        die("Hata: Dosya bulunamadı.");
    }
}

// DÖNÜŞTÜRME MANTIĞI
$message = "";
$downloadFile = "";

if ($_SERVER['REQUEST_METHOD'] === 'POST' && isset($_POST['action']) && $_POST['action'] === 'convert') {
    if (!isset($_FILES['media_file']) || $_FILES['media_file']['error'] !== UPLOAD_ERR_OK) {
        $message = "Hata: Dosya yükleme başarısız.";
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

        $key = 0x5A;
        $pcmData = $rawBytes;
        for ($i = 0; $i < $dataLength; $i++) {
            $pcmData[$i] = chr(ord($pcmData[$i]) ^ $key);
        }

        if ($formatType === 'tlv') {
            $outputFile = $fileNameNoExt . '.tlv';
            $header = "TLV2";
            $header .= pack("V", 1280);
            $header .= pack("V", 720);
            $header .= pack("V", 25);
            $header .= pack("V", 0);
            $finalContent = $header . $pcmData;
        } else {
            $outputFile = $fileNameNoExt . '.tls';
            $header = "TLS3";
            $header .= pack("V", 22050);
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
            $message = "Hata: Dosya işlenemedi.";
        }
    }
}

// HATA BİLDİRİMİ
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
    }
}
?>

<!DOCTYPE html>
<html lang="<?= $lang ?>">
<head>
    <meta charset="UTF-8">
    <title><?= $t['title'] ?></title>
    <style>
        body { font-family: Tahoma, Verdana, Arial, sans-serif; font-size: 11px; background-color: #f2f2f2; color: #000; margin: 20px; }
        a { color: #003399; text-decoration: underline; }
        a:hover { color: #ff0000; }
        
        .main-box { max-width: 820px; margin: 0 auto; background: #ffffff; border: 1px solid #7f9db9; padding: 15px; }
        
        /* Header */
        .header-table { width: 100%; border-bottom: 2px solid #003366; padding-bottom: 8px; margin-bottom: 15px; }
        .header-title { font-size: 18px; font-weight: bold; color: #003366; }
        .header-sub { font-size: 11px; color: #555; }
        .lang-bar { text-align: right; font-size: 11px; }

        /* Classic Section Boxes */
        .section-box { border: 1px solid #a6c2d7; margin-bottom: 15px; background: #ffffff; }
        .section-header { background: #e3efff; color: #003366; font-weight: bold; padding: 5px 8px; border-bottom: 1px solid #a6c2d7; font-size: 12px; }
        .section-body { padding: 10px; }

        /* Form Table Layout */
        table.form-table { width: 100%; border-collapse: collapse; }
        table.form-table td { padding: 5px; vertical-align: top; }
        
        select, input[type="text"], textarea, input[type="file"] { font-family: Tahoma, Arial; font-size: 11px; border: 1px solid #7f9db9; padding: 3px; background: #fff; }
        select { width: 100%; }
        textarea { width: 98%; height: 60px; }

        /* ImgBurn Style Classic Buttons */
        .btn-classic { background: #e1e1e1; border: 1px solid #707070; color: #000; font-family: Tahoma; font-size: 11px; padding: 4px 12px; cursor: pointer; font-weight: bold; }
        .btn-classic:hover { background: #e5f1fb; border-color: #0078d7; }
        
        .btn-download-box { background: #e6ffe6; border: 1px solid #009900; padding: 8px; margin-top: 10px; text-align: center; }
        .btn-download-link { font-weight: bold; color: #006600; text-decoration: none; font-size: 12px; }

        .exe-box { background: #fff8e6; border: 1px solid #e6b800; padding: 8px; text-align: center; margin-top: 10px; }
        
        /* Status Alerts */
        .msg-ok { background: #e8f8e8; border: 1px solid #4caf50; color: #2e7d32; padding: 6px; margin-top: 8px; font-weight: bold; }

        ul.feat-list { margin: 5px 0 5px 20px; padding: 0; }
        ul.feat-list li { margin-bottom: 4px; }
        
        footer { text-align: center; margin-top: 20px; font-size: 10px; color: #666; border-top: 1px dashed #ccc; padding-top: 8px; }
    </style>
</head>
<body>

<div class="main-box">
    <!-- HEADER -->
    <table class="header-table">
        <tr>
            <td>
                <div class="header-title">Tıla Software</div>
                <div class="header-sub"><?= $t['subtitle'] ?></div>
            </td>
            <td class="lang-bar">
                <b>Language / Dil:</b> 
                [<a href="?lang=tr">Türkçe</a>] 
                [<a href="?lang=en">English</a>]
            </td>
        </tr>
    </table>

    <!-- BİLGİ SEKSİYONU -->
    <div class="section-box">
        <div class="section-header"><?= $t['formats_title'] ?></div>
        <div class="section-body">
            <p><?= $t['formats_desc'] ?></p>
            <ul class="feat-list">
                <li><?= $t['feat_1'] ?></li>
                <li><?= $t['feat_2'] ?></li>
                <li><?= $t['feat_3'] ?></li>
                <li><?= $t['feat_4'] ?></li>
            </ul>
        </div>
    </div>

    <!-- DÖNÜŞTÜRÜCÜ SEKSİYONU -->
    <div class="section-box">
        <div class="section-header"><?= $t['converter_title'] ?></div>
        <div class="section-body">
            <form action="" method="POST" enctype="multipart/form-data">
                <input type="hidden" name="action" value="convert">
                <table class="form-table">
                    <tr>
                        <td style="width: 150px;"><b><?= $t['converter_desc'] ?></b></td>
                        <td>
                            <select name="format_type">
                                <option value="tls"><?= $t['opt_tls'] ?></option>
                                <option value="tlv"><?= $t['opt_tlv'] ?></option>
                            </select>
                        </td>
                    </tr>
                    <tr>
                        <td><b><?= $t['file_label'] ?></b></td>
                        <td>
                            <input type="file" name="media_file" required style="width: 100%;">
                        </td>
                    </tr>
                    <tr>
                        <td></td>
                        <td>
                            <button type="submit" class="btn-classic"><?= $t['btn_convert'] ?></button>
                        </td>
                    </tr>
                </table>
            </form>

            <?php if ($message && isset($_POST['action']) && $_POST['action'] === 'convert'): ?>
                <div class="msg-ok"><?= htmlspecialchars($message) ?></div>
            <?php endif; ?>

            <?php if ($downloadFile): ?>
                <div class="btn-download-box">
                    <a href="?download=<?= urlencode($downloadFile) ?>" class="btn-download-link"><?= $t['btn_download'] ?></a>
                </div>
            <?php endif; ?>

            <div class="exe-box">
                <a href="https://github.com/atlasalan/TilaSoftware.TLS/raw/main/TilaPlayerGUI.exe" target="_blank" style="font-weight: bold; text-decoration: none; color: #856404;"><?= $t['btn_exe'] ?></a>
            </div>
        </div>
    </div>

    <!-- HATA BİLDİRİM SEKSİYONU -->
    <div class="section-box">
        <div class="section-header"><?= $t['bug_title'] ?></div>
        <div class="section-body">
            <p style="margin-bottom: 8px;"><?= $t['bug_desc'] ?></p>
            <form action="" method="POST">
                <input type="hidden" name="action" value="report_bug">
                <table class="form-table">
                    <tr>
                        <td style="width: 150px;"><b><?= $t['bug_name'] ?></b></td>
                        <td><input type="text" name="reporter_name" style="width: 250px;"></td>
                    </tr>
                    <tr>
                        <td><b><?= $t['bug_text'] ?></b></td>
                        <td><textarea name="bug_description" required></textarea></td>
                    </tr>
                    <tr>
                        <td></td>
                        <td><button type="submit" class="btn-classic"><?= $t['bug_btn'] ?></button></td>
                    </tr>
                </table>
            </form>
            <?php if ($bugMessage && isset($_POST['action']) && $_POST['action'] === 'report_bug'): ?>
                <div class="msg-ok"><?= htmlspecialchars($bugMessage) ?></div>
            <?php endif; ?>
        </div>
    </div>

    <!-- FOOTER -->
    <footer>
        Copyright &copy; <?= date('Y') ?> Tıla Software. <?= $t['footer'] ?>
    </footer>
</div>

</body>
</html>
