using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

namespace TilaAudioGui
{
    public class AxWMP : AxHost
    {
        public AxWMP() : base("6bf52a52-394a-11d3-b153-00c04f79faa6") { }
    }

    public class GuiPlayer : Form
    {
        [DllImport("shell32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

        private Panel pnlMediaContainer;
        private Panel pnlVideoHolder;
        private Panel pnlSidebar;
        private AxWMP axWmpPlayer;
        private Label lblCenterIcon;
        private Label lblTitle;
        private Label lblSubTitle;
        private Label lblTime;
        private TrackBar tbProgress;
        private TrackBar tbVolume;
        private Label lblVolumeIcon;
        private RoundedButton btnOpen;
        private RoundedButton btnConvert;
        private RoundedButton btnPlayPause;
        private RoundedButton btnStop;

        // Özellik Paneli
        private Label lblSidebarTitle;
        private Label lblPropName;
        private Label lblPropType;
        private Label lblPropAlgo;
        private RoundedButton btnToggleLoop;
        private RoundedButton btnLangSwitch;

        private Timer timerProgress;
        private Timer timerAnim;

        private bool isPlaying = false;
        private bool isVideoMode = false;
        private bool isUserSeeking = false;
        private bool isLoopEnabled = false;
        private string currentLang = "tr";
        private int totalSeconds = 0;
        private int currentSeconds = 0;
        private float animAngle = 0;

        private string currentTempFile = "";
        private List<string> tempFilesToClean = new List<string>();
        private string loadedFileName = "";

        public GuiPlayer(string startupFile = "")
        {
            InitializeComponent();
            UpdateTexts();
            SetAppIcon();
            this.FormClosing += GuiPlayer_FormClosing;

            if (!string.IsNullOrEmpty(startupFile) && File.Exists(startupFile))
            {
                this.Shown += (s, e) => OpenAndPlayMedia(startupFile);
            }
        }

        private void SetAppIcon()
        {
            try
            {
                string iconPath = Path.Combine(Application.StartupPath, "app.ico");
                if (File.Exists(iconPath))
                {
                    this.Icon = new Icon(iconPath);
                }
            }
            catch { }
        }

        public static void RegisterFileAssociations()
{
    try
    {
        string exePath = Application.ExecutablePath;
        string expectedCmd = "\"" + exePath + "\" \"%1\"";

        // Zaten senin oynatıcıya bağlı mı kontrol et
        using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Classes\TilaSoftware.TLS\shell\open\command"))
        {
            if (key != null)
            {
                string currentVal = key.GetValue("") as string;
                if (currentVal == expectedCmd)
                {
                    // Zaten tam olarak bu exe'ye bağlı, masaüstünü YENİLEME ve çık!
                    return;
                }
            }
        }

        // Bağlı değilse ilk defa kaydet
        RegisterExtension(".tls", "TilaSoftware.TLS", "Tıla Ses Dosyası", exePath);
        RegisterExtension(".tlv", "TilaSoftware.TLV", "Tıla Video Dosyası", exePath);

        // Sadece İLK DEFA kayıt yapıldığında Windows simgelerini yenile
        SHChangeNotify(0x08000000, 0x0000, IntPtr.Zero, IntPtr.Zero);
    }
    catch { }
}

        private static void RegisterExtension(string ext, string progId, string description, string exePath)
        {
            using (RegistryKey keyExt = Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + ext))
            {
                keyExt.SetValue("", progId);
            }

            using (RegistryKey keyProg = Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + progId))
            {
                keyProg.SetValue("", description);
                using (RegistryKey keyIcon = keyProg.CreateSubKey("DefaultIcon"))
                {
                    keyIcon.SetValue("", "\"" + exePath + "\",0");
                }
                using (RegistryKey keyCmd = keyProg.CreateSubKey(@"shell\open\command"))
                {
                    keyCmd.SetValue("", "\"" + exePath + "\" \"%1\"");
                }
            }
        }

        private void GuiPlayer_FormClosing(object sender, FormClosingEventArgs e)
        {
            SafelyStopPlayer();
            CleanupAllTempFiles();
        }

        private void SafelyStopPlayer()
        {
            try
            {
                if (axWmpPlayer != null)
                {
                    dynamic wmp = axWmpPlayer.GetOcx();
                    if (wmp != null && wmp.controls != null)
                    {
                        wmp.controls.stop();
                        wmp.close();
                    }
                }
            }
            catch { }
        }

        private void CleanupAllTempFiles()
        {
            foreach (var file in tempFilesToClean)
            {
                try
                {
                    if (File.Exists(file)) File.Delete(file);
                }
                catch { }
            }
            tempFilesToClean.Clear();
        }

        private void InitializeComponent()
        {
            this.Size = new Size(740, 580);
            this.BackColor = Color.FromArgb(18, 18, 22);
            this.ForeColor = Color.White;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;

            pnlMediaContainer = new Panel()
            {
                Location = new Point(20, 20),
                Size = new Size(464, 250),
                BackColor = Color.FromArgb(28, 28, 35)
            };
            pnlMediaContainer.Paint += PnlMediaContainer_Paint;

            pnlVideoHolder = new Panel()
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black,
                Visible = false
            };
            pnlMediaContainer.Controls.Add(pnlVideoHolder);

            try
            {
                axWmpPlayer = new AxWMP();
                ((System.ComponentModel.ISupportInitialize)(axWmpPlayer)).BeginInit();
                axWmpPlayer.Dock = DockStyle.Fill;
                pnlVideoHolder.Controls.Add(axWmpPlayer);
                ((System.ComponentModel.ISupportInitialize)(axWmpPlayer)).EndInit();
                IntPtr forceHandle = axWmpPlayer.Handle;
            }
            catch { }

            lblCenterIcon = new Label()
            {
                Text = "🎵",
                Font = new Font("Segoe UI Emoji", 48, FontStyle.Regular),
                ForeColor = Color.FromArgb(0, 120, 212),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };
            pnlMediaContainer.Controls.Add(lblCenterIcon);

            pnlSidebar = new Panel()
            {
                Location = new Point(500, 20),
                Size = new Size(205, 470),
                BackColor = Color.FromArgb(24, 24, 30)
            };

            lblSidebarTitle = new Label()
            {
                Location = new Point(12, 12),
                Size = new Size(180, 25),
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 120, 212)
            };

            lblPropName = new Label()
            {
                Location = new Point(12, 55),
                Size = new Size(180, 55),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                ForeColor = Color.FromArgb(200, 200, 210)
            };

            lblPropType = new Label()
            {
                Location = new Point(12, 125),
                Size = new Size(180, 25),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                ForeColor = Color.FromArgb(200, 200, 210)
            };

            lblPropAlgo = new Label()
            {
                Location = new Point(12, 165),
                Size = new Size(180, 25),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                ForeColor = Color.FromArgb(200, 200, 210)
            };

            btnToggleLoop = new RoundedButton()
            {
                Location = new Point(12, 355),
                Size = new Size(180, 42),
                BackColor = Color.FromArgb(45, 45, 55),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                BorderRadius = 15
            };
            btnToggleLoop.Click += BtnToggleLoop_Click;

            btnLangSwitch = new RoundedButton()
            {
                Location = new Point(12, 410),
                Size = new Size(180, 42),
                BackColor = Color.FromArgb(0, 120, 212),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                BorderRadius = 15
            };
            btnLangSwitch.Click += BtnLangSwitch_Click;

            pnlSidebar.Controls.Add(lblSidebarTitle);
            pnlSidebar.Controls.Add(lblPropName);
            pnlSidebar.Controls.Add(lblPropType);
            pnlSidebar.Controls.Add(lblPropAlgo);
            pnlSidebar.Controls.Add(btnToggleLoop);
            pnlSidebar.Controls.Add(btnLangSwitch);

            lblTitle = new Label()
            {
                Location = new Point(20, 280),
                Size = new Size(464, 25),
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter
            };

            lblSubTitle = new Label()
            {
                Location = new Point(20, 308),
                Size = new Size(464, 20),
                Font = new Font("Segoe UI", 9, FontStyle.Regular),
                ForeColor = Color.FromArgb(140, 140, 150),
                TextAlign = ContentAlignment.MiddleCenter
            };

            tbProgress = new TrackBar()
            {
                Location = new Point(15, 335),
                Size = new Size(474, 30),
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                TickStyle = TickStyle.None
            };
            tbProgress.MouseDown += TbProgress_MouseDown;
            tbProgress.MouseUp += TbProgress_MouseUp;
            tbProgress.Scroll += TbProgress_Scroll;

            lblTime = new Label()
            {
                Text = "00:00 / 00:00",
                Location = new Point(20, 368),
                Size = new Size(464, 30),
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Color.FromArgb(200, 200, 210),
                TextAlign = ContentAlignment.MiddleCenter
            };

            btnOpen = new RoundedButton()
            {
                Location = new Point(15, 420),
                Size = new Size(70, 42),
                BackColor = Color.FromArgb(40, 40, 50),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                BorderRadius = 20
            };

            btnConvert = new RoundedButton()
            {
                Location = new Point(90, 420),
                Size = new Size(95, 42),
                BackColor = Color.FromArgb(0, 120, 212),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                BorderRadius = 20
            };

            btnPlayPause = new RoundedButton()
            {
                Location = new Point(190, 415),
                Size = new Size(115, 52),
                BackColor = Color.FromArgb(40, 160, 80),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Enabled = false,
                BorderRadius = 25
            };

            btnStop = new RoundedButton()
            {
                Location = new Point(310, 420),
                Size = new Size(80, 42),
                BackColor = Color.FromArgb(190, 50, 50),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Enabled = false,
                BorderRadius = 20
            };

            lblVolumeIcon = new Label()
            {
                Text = "🔊",
                Location = new Point(395, 431),
                Size = new Size(22, 25),
                Font = new Font("Segoe UI Emoji", 10)
            };

            tbVolume = new TrackBar()
            {
                Location = new Point(415, 427),
                Size = new Size(75, 30),
                Minimum = 0,
                Maximum = 100,
                Value = 80,
                TickStyle = TickStyle.None
            };
            tbVolume.Scroll += TbVolume_Scroll;

            timerProgress = new Timer() { Interval = 250 };
            timerProgress.Tick += TimerProgress_Tick;

            timerAnim = new Timer() { Interval = 40 };
            timerAnim.Tick += TimerAnim_Tick;

            btnOpen.Click += BtnOpen_Click;
            btnConvert.Click += BtnConvert_Click;
            btnPlayPause.Click += BtnPlayPause_Click;
            btnStop.Click += BtnStop_Click;

            this.Controls.Add(pnlMediaContainer);
            this.Controls.Add(pnlSidebar);
            this.Controls.Add(lblTitle);
            this.Controls.Add(lblSubTitle);
            this.Controls.Add(tbProgress);
            this.Controls.Add(lblTime);
            this.Controls.Add(btnOpen);
            this.Controls.Add(btnConvert);
            this.Controls.Add(btnPlayPause);
            this.Controls.Add(btnStop);
            this.Controls.Add(lblVolumeIcon);
            this.Controls.Add(tbVolume);
        }

        private void UpdateTexts()
        {
            if (currentLang == "tr")
            {
                this.Text = "Tıla Medya Oynatıcı & Özellikler";
                lblSidebarTitle.Text = "📊 Medya Özellikleri";
                lblPropName.Text = string.IsNullOrEmpty(loadedFileName) ? "Dosya: Bekleniyor..." : "Dosya: " + loadedFileName;
                lblPropType.Text = isVideoMode ? "Biçim: Tıla Video (.tlv)" : (string.IsNullOrEmpty(loadedFileName) ? "Biçim: Yok" : "Biçim: Tıla Ses (.tls)");
                lblPropAlgo.Text = "Şifreleme: XOR (0x5A)";
                btnToggleLoop.Text = isLoopEnabled ? "🔁 Döngü: Açık" : "🔁 Döngü: Kapalı";
                btnLangSwitch.Text = "🌐 Dil: TR (EN Yap)";
                lblTitle.Text = string.IsNullOrEmpty(loadedFileName) ? "Medya Seçilmedi" : Path.GetFileNameWithoutExtension(loadedFileName);
                lblSubTitle.Text = isVideoMode ? "Tıla Video Dosyası (.tlv)" : "Tıla Ses Dosyası (.tls)";
                btnOpen.Text = "📂 Aç";
                btnConvert.Text = "🔄 Dönüştür";
                btnPlayPause.Text = isPlaying ? "⏸ Duraklat" : "▶ Oynat";
                btnStop.Text = "⏹ Durdur";
            }
            else
            {
                this.Text = "Tila Media Player & Properties";
                lblSidebarTitle.Text = "📊 Media Properties";
                lblPropName.Text = string.IsNullOrEmpty(loadedFileName) ? "File: Waiting..." : "File: " + loadedFileName;
                lblPropType.Text = isVideoMode ? "Format: Tila Video (.tlv)" : (string.IsNullOrEmpty(loadedFileName) ? "Format: None" : "Format: Tila Audio (.tls)");
                lblPropAlgo.Text = "Encryption: XOR (0x5A)";
                btnToggleLoop.Text = isLoopEnabled ? "🔁 Loop: On" : "🔁 Loop: Off";
                btnLangSwitch.Text = "🌐 Lang: EN (Switch to TR)";
                lblTitle.Text = string.IsNullOrEmpty(loadedFileName) ? "Media Not Selected" : Path.GetFileNameWithoutExtension(loadedFileName);
                lblSubTitle.Text = isVideoMode ? "Tila Video File (.tlv)" : "Tila Audio File (.tls)";
                btnOpen.Text = "📂 Open";
                btnConvert.Text = "🔄 Convert";
                btnPlayPause.Text = isPlaying ? "⏸ Pause" : "▶ Play";
                btnStop.Text = "⏹ Stop";
            }
        }

        private void BtnLangSwitch_Click(object sender, EventArgs e)
        {
            currentLang = (currentLang == "tr") ? "en" : "tr";
            UpdateTexts();
        }

        private void BtnToggleLoop_Click(object sender, EventArgs e)
        {
            isLoopEnabled = !isLoopEnabled;
            btnToggleLoop.BackColor = isLoopEnabled ? Color.FromArgb(0, 120, 212) : Color.FromArgb(45, 45, 55);
            UpdateTexts();
        }

        private void OpenAndPlayMedia(string filePath)
        {
            try
            {
                StopPlayback();

                FileInfo fi = new FileInfo(filePath);
                loadedFileName = fi.Name;

                byte[] fileBytes = File.ReadAllBytes(filePath);
                if (fileBytes.Length < 16) return;

                string magic = Encoding.ASCII.GetString(fileBytes, 0, 4);

                if (magic == "TLS3" || magic == "TLS2")
                {
                    isVideoMode = false;
                    pnlVideoHolder.Visible = false;
                    lblCenterIcon.Visible = true;
                    LoadTlsMedia(fileBytes);
                }
                else if (magic == "TLV2" || magic == "TLV1")
                {
                    isVideoMode = true;
                    lblCenterIcon.Visible = false;
                    pnlVideoHolder.Visible = true;
                    LoadTlvMedia(fileBytes, magic);
                }
                else
                {
                    return;
                }

                UpdateTexts();
                btnPlayPause.Enabled = true;
                btnStop.Enabled = true;

                StartPlayback();
            }
            catch { }
        }

        private void BtnOpen_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "Tıla Medya Dosyaları (*.tls;*.tlv)|*.tls;*.tlv";

            if (ofd.ShowDialog() == DialogResult.OK)
            {
                OpenAndPlayMedia(ofd.FileName);
            }
        }

        private void LoadTlsMedia(byte[] fileBytes)
        {
            byte key = 0x5A;
            int headerSize = 16;
            int payloadLen = fileBytes.Length - headerSize;

            byte[] decrypted = new byte[payloadLen];
            for (int i = 0; i < payloadLen; i++)
            {
                decrypted[i] = (byte)(fileBytes[headerSize + i] ^ key);
            }

            string tempFile = Path.Combine(Path.GetTempPath(), "tila_snd_" + Guid.NewGuid().ToString("N") + ".mp3");
            File.WriteAllBytes(tempFile, decrypted);

            currentTempFile = tempFile;
            tempFilesToClean.Add(tempFile);

            if (axWmpPlayer != null)
            {
                try
                {
                    dynamic wmp = axWmpPlayer.GetOcx();
                    if (wmp != null)
                    {
                        wmp.uiMode = "none";
                        wmp.URL = tempFile;
                    }
                }
                catch { }
            }
        }

        private void LoadTlvMedia(byte[] fileBytes, string magic)
        {
            byte key = 0x5A;
            int headerSize = (magic == "TLV2") ? 20 : 16;
            int payloadLen = fileBytes.Length - headerSize;

            byte[] decrypted = new byte[payloadLen];
            for (int i = 0; i < payloadLen; i++)
            {
                decrypted[i] = (byte)(fileBytes[headerSize + i] ^ key);
            }

            string tempFile = Path.Combine(Path.GetTempPath(), "tila_vid_" + Guid.NewGuid().ToString("N") + ".mp4");
            File.WriteAllBytes(tempFile, decrypted);

            currentTempFile = tempFile;
            tempFilesToClean.Add(tempFile);

            if (axWmpPlayer != null)
            {
                try
                {
                    dynamic wmp = axWmpPlayer.GetOcx();
                    if (wmp != null)
                    {
                        wmp.uiMode = "none";
                        wmp.stretchToFit = true;
                        wmp.URL = tempFile;
                    }
                }
                catch { }
            }
        }

        private void StartPlayback()
        {
            if (axWmpPlayer != null && !string.IsNullOrEmpty(currentTempFile))
            {
                try
                {
                    dynamic wmp = axWmpPlayer.GetOcx();
                    if (wmp != null && wmp.controls != null)
                    {
                        wmp.controls.play();
                        if (isVideoMode) wmp.stretchToFit = true;
                    }
                }
                catch { }
            }

            ApplyVolume();
            isPlaying = true;
            btnPlayPause.Text = (currentLang == "tr") ? "⏸ Duraklat" : "⏸ Pause";
            timerProgress.Start();
            if (!isVideoMode) timerAnim.Start();
        }

        private void StopPlayback()
        {
            if (axWmpPlayer != null)
            {
                try
                {
                    dynamic wmp = axWmpPlayer.GetOcx();
                    if (wmp != null && wmp.controls != null)
                    {
                        wmp.controls.stop();
                    }
                }
                catch { }
            }

            isPlaying = false;
            timerProgress.Stop();
            timerAnim.Stop();
            currentSeconds = 0;
            tbProgress.Value = 0;
            btnPlayPause.Text = (currentLang == "tr") ? "▶ Oynat" : "▶ Play";
            UpdateTimerLabel();
            pnlMediaContainer.Invalidate();
        }

        private void BtnPlayPause_Click(object sender, EventArgs e)
        {
            if (isPlaying)
            {
                if (axWmpPlayer != null)
                {
                    try
                    {
                        dynamic wmp = axWmpPlayer.GetOcx();
                        if (wmp != null && wmp.controls != null)
                        {
                            wmp.controls.pause();
                        }
                    }
                    catch { }
                }

                isPlaying = false;
                btnPlayPause.Text = (currentLang == "tr") ? "▶ Oynat" : "▶ Play";
                timerProgress.Stop();
                timerAnim.Stop();
            }
            else
            {
                StartPlayback();
            }
        }

        private void BtnStop_Click(object sender, EventArgs e)
        {
            StopPlayback();
        }

        private void TbVolume_Scroll(object sender, EventArgs e)
        {
            ApplyVolume();
        }

        private void ApplyVolume()
        {
            if (axWmpPlayer != null)
            {
                try
                {
                    dynamic wmp = axWmpPlayer.GetOcx();
                    if (wmp != null && wmp.settings != null)
                    {
                        wmp.settings.volume = tbVolume.Value;
                    }
                }
                catch { }
            }
        }

        private void TbProgress_MouseDown(object sender, MouseEventArgs e) { isUserSeeking = true; }

        private void TbProgress_MouseUp(object sender, MouseEventArgs e)
        {
            if (isUserSeeking && axWmpPlayer != null)
            {
                try
                {
                    dynamic wmp = axWmpPlayer.GetOcx();
                    if (wmp != null && wmp.controls != null)
                    {
                        wmp.controls.currentPosition = tbProgress.Value;
                        currentSeconds = tbProgress.Value;
                        UpdateTimerLabel();
                    }
                }
                catch { }
            }
            isUserSeeking = false;
        }

        private void TbProgress_Scroll(object sender, EventArgs e)
        {
            if (isUserSeeking)
            {
                currentSeconds = tbProgress.Value;
                UpdateTimerLabel();
            }
        }

        private void TimerProgress_Tick(object sender, EventArgs e)
        {
            if (axWmpPlayer != null && !isUserSeeking)
            {
                try
                {
                    dynamic wmp = axWmpPlayer.GetOcx();
                    if (wmp != null && wmp.currentMedia != null && wmp.currentMedia.duration > 0)
                    {
                        totalSeconds = (int)wmp.currentMedia.duration;
                        currentSeconds = (int)wmp.controls.currentPosition;

                        if (isLoopEnabled && currentSeconds >= totalSeconds - 1 && totalSeconds > 0)
                        {
                            wmp.controls.currentPosition = 0;
                            wmp.controls.play();
                        }

                        tbProgress.Maximum = totalSeconds > 0 ? totalSeconds : 100;
                        tbProgress.Value = Math.Min(currentSeconds, tbProgress.Maximum);
                        UpdateTimerLabel();
                    }
                }
                catch { }
            }
        }

        private void UpdateTimerLabel()
        {
            TimeSpan current = TimeSpan.FromSeconds(currentSeconds);
            TimeSpan total = TimeSpan.FromSeconds(totalSeconds);
            lblTime.Text = string.Format("{0:D2}:{1:D2} / {2:D2}:{3:D2}", current.Minutes, current.Seconds, total.Minutes, total.Seconds);
        }

        private void TimerAnim_Tick(object sender, EventArgs e)
        {
            animAngle = (animAngle + 4) % 360;
            pnlMediaContainer.Invalidate();
        }

        private void PnlMediaContainer_Paint(object sender, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = pnlMediaContainer.ClientRectangle;
            rect.Width--; rect.Height--;
            GraphicsPath path = GetRoundedPath(rect, 16);
            pnlMediaContainer.Region = new Region(path);

            if (isPlaying && !isVideoMode)
            {
                using (Pen pen = new Pen(Color.FromArgb(0, 120, 212), 4))
                {
                    pen.DashStyle = DashStyle.Dot;
                    int cx = pnlMediaContainer.Width / 2;
                    int cy = pnlMediaContainer.Height / 2;
                    int size = 120;

                    e.Graphics.TranslateTransform(cx, cy);
                    e.Graphics.RotateTransform(animAngle);
                    e.Graphics.DrawEllipse(pen, -size / 2, -size / 2, size, size);
                    e.Graphics.ResetTransform();
                }
            }
        }

        private void BtnConvert_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Title = (currentLang == "tr") ? "Dönüştürülecek Medya Dosyasını Seçin" : "Select Media File to Convert";
            ofd.Filter = (currentLang == "tr") ?
                "Tüm Desteklenen Medyalar|*.mp3;*.wav;*.aac;*.flac;*.ogg;*.m4a;*.mp4;*.avi;*.mkv;*.mov;*.wmv;*.webm;*.jpg;*.png;*.gif;*.bmp|Ses Dosyaları (*.mp3;*.wav;*.aac;*.flac;*.ogg;*.m4a)|*.mp3;*.wav;*.aac;*.flac;*.ogg;*.m4a|Video ve Resim Dosyaları (*.mp4;*.avi;*.mkv;*.mov;*.wmv;*.webm;*.jpg;*.png;*.gif;*.bmp)|*.mp4;*.avi;*.mkv;*.mov;*.wmv;*.webm;*.jpg;*.png;*.gif;*.bmp" :
                "All Supported Media|*.mp3;*.wav;*.aac;*.flac;*.ogg;*.m4a;*.mp4;*.avi;*.mkv;*.mov;*.wmv;*.webm;*.jpg;*.png;*.gif;*.bmp|Audio Files (*.mp3;*.wav;*.aac;*.flac;*.ogg;*.m4a)|*.mp3;*.wav;*.aac;*.flac;*.ogg;*.m4a|Video & Image Files (*.mp4;*.avi;*.mkv;*.mov;*.wmv;*.webm;*.jpg;*.png;*.gif;*.bmp)|*.mp4;*.avi;*.mkv;*.mov;*.wmv;*.webm;*.jpg;*.png;*.gif;*.bmp";

            if (ofd.ShowDialog() == DialogResult.OK)
            {
                string ext = Path.GetExtension(ofd.FileName).ToLower();
                bool isAudio = (ext == ".mp3" || ext == ".wav" || ext == ".aac" || ext == ".flac" || ext == ".ogg" || ext == ".m4a");

                SaveFileDialog sfd = new SaveFileDialog();
                if (isAudio)
                {
                    sfd.Title = (currentLang == "tr") ? ".TLS (Tıla Ses) Olarak Kaydet" : "Save as .TLS (Tila Audio)";
                    sfd.Filter = "Tıla Ses Dosyası (*.tls)|*.tls";
                    sfd.FileName = Path.GetFileNameWithoutExtension(ofd.FileName) + ".tls";
                }
                else
                {
                    sfd.Title = (currentLang == "tr") ? ".TLV (Tıla Video) Olarak Kaydet" : "Save as .TLV (Tila Video)";
                    sfd.Filter = "Tıla Video Dosyası (*.tlv)|*.tlv";
                    sfd.FileName = Path.GetFileNameWithoutExtension(ofd.FileName) + ".tlv";
                }

                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        byte[] rawBytes = File.ReadAllBytes(ofd.FileName);
                        byte key = 0x5A;
                        byte[] encrypted = new byte[rawBytes.Length];
                        for (int i = 0; i < rawBytes.Length; i++) { encrypted[i] = (byte)(rawBytes[i] ^ key); }

                        using (FileStream fs = new FileStream(sfd.FileName, FileMode.Create))
                        using (BinaryWriter bw = new BinaryWriter(fs))
                        {
                            if (isAudio)
                            {
                                bw.Write(Encoding.ASCII.GetBytes("TLS3"));
                                bw.Write((int)22050);
                                bw.Write((short)2);
                                bw.Write((short)16);
                                bw.Write((int)encrypted.Length);
                                bw.Write(encrypted);
                                MessageBox.Show((currentLang == "tr") ? "Ses dosyası başarıyla .tls formatına paketlendi!" : "Audio packed into .tls format!", (currentLang == "tr") ? "Başarılı" : "Success");
                            }
                            else
                            {
                                bw.Write(Encoding.ASCII.GetBytes("TLV2"));
                                bw.Write((int)1280);
                                bw.Write((int)720);
                                bw.Write((int)25);
                                bw.Write((int)0);
                                bw.Write(encrypted);
                                MessageBox.Show((currentLang == "tr") ? "Video/Resim dosyası başarıyla .tlv formatına paketlendi!" : "Video/Image packed into .tlv format!", (currentLang == "tr") ? "Başarılı" : "Success");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(((currentLang == "tr") ? "Dönüştürme hatası: " : "Conversion error: ") + ex.Message, (currentLang == "tr") ? "Hata" : "Error");
                    }
                }
            }
        }

        private GraphicsPath GetRoundedPath(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            float diameter = radius * 2f;
            path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
            path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        [STAThread]
        static void Main(string[] args)
        {
            RegisterFileAssociations();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string startupFile = (args != null && args.Length > 0) ? args[0] : "";
            Application.Run(new GuiPlayer(startupFile));
        }
    }

    public class RoundedButton : Button
    {
        public int BorderRadius { get; set; }
        public RoundedButton()
        {
            this.BorderRadius = 20;
            this.FlatStyle = FlatStyle.Flat;
            this.FlatAppearance.BorderSize = 0;
            this.Cursor = Cursors.Hand;
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            pevent.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(0, 0, this.Width, this.Height);
            using (GraphicsPath path = GetPath(rect, BorderRadius))
            {
                this.Region = new Region(path);
                using (SolidBrush brush = new SolidBrush(this.BackColor))
                {
                    pevent.Graphics.FillPath(brush, path);
                }
            }
            TextRenderer.DrawText(pevent.Graphics, this.Text, this.Font, rect, this.ForeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        private GraphicsPath GetPath(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            float diameter = radius * 2f;
            path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
            path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
