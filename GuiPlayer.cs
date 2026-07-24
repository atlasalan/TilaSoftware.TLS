using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace TilaAudioGui
{
    public class AxWMP : AxHost
    {
        public AxWMP() : base("6bf52a52-394a-11d3-b153-00c04f79faa6") { }
    }

    public class GuiPlayer : Form
    {
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

        private Label lblSidebarTitle;
        private Label lblPropName;
        private Label lblPropType;
        private Label lblPropAlgo;
        private RoundedButton btnToggleLoop;

        private Timer timerProgress;
        private Timer timerAnim;

        private bool isPlaying = false;
        private bool isVideoMode = false;
        private bool isUserSeeking = false;
        private bool isLoopEnabled = false;
        private int totalSeconds = 0;
        private int currentSeconds = 0;
        private float animAngle = 0;
        private string tempMediaFile = "";

        public GuiPlayer()
        {
            InitializeComponent();
            this.FormClosing += GuiPlayer_FormClosing;
        }

        private void GuiPlayer_FormClosing(object sender, FormClosingEventArgs e)
        {
            CleanupTempFile();
        }

        private void CleanupTempFile()
        {
            try
            {
                if (axWmpPlayer != null)
                {
                    dynamic wmp = axWmpPlayer.GetOcx();
                    if (wmp != null)
                    {
                        wmp.controls.stop();
                        wmp.URL = ""; // WMP dosya kilidini kaldırır
                    }
                }

                System.Threading.Thread.Sleep(50); // Kilidin serbest kalması için kısa bekleme

                if (!string.IsNullOrEmpty(tempMediaFile) && File.Exists(tempMediaFile))
                {
                    File.Delete(tempMediaFile);
                }
            }
            catch { }
        }

        private void InitializeComponent()
        {
            this.Text = "Tıla Medya Oynatıcı & Özellikler";
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
                Text = "📊 Medya Özellikleri",
                Location = new Point(12, 12),
                Size = new Size(180, 25),
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 120, 212)
            };

            lblPropName = new Label()
            {
                Text = "Dosya: Bekleniyor...",
                Location = new Point(12, 55),
                Size = new Size(180, 55),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                ForeColor = Color.FromArgb(200, 200, 210)
            };

            lblPropType = new Label()
            {
                Text = "Biçim: Yok",
                Location = new Point(12, 125),
                Size = new Size(180, 25),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                ForeColor = Color.FromArgb(200, 200, 210)
            };

            lblPropAlgo = new Label()
            {
                Text = "Şifreleme: XOR (0x5A)",
                Location = new Point(12, 165),
                Size = new Size(180, 25),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                ForeColor = Color.FromArgb(200, 200, 210)
            };

            btnToggleLoop = new RoundedButton()
            {
                Text = "🔁 Döngü: Kapalı",
                Location = new Point(12, 410),
                Size = new Size(180, 42),
                BackColor = Color.FromArgb(45, 45, 55),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                BorderRadius = 15
            };
            btnToggleLoop.Click += BtnToggleLoop_Click;

            pnlSidebar.Controls.Add(lblSidebarTitle);
            pnlSidebar.Controls.Add(lblPropName);
            pnlSidebar.Controls.Add(lblPropType);
            pnlSidebar.Controls.Add(lblPropAlgo);
            pnlSidebar.Controls.Add(btnToggleLoop);

            lblTitle = new Label()
            {
                Text = "Medya Seçilmedi",
                Location = new Point(20, 280),
                Size = new Size(464, 25),
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter
            };

            lblSubTitle = new Label()
            {
                Text = "Tıla Medya Biçimi (.tls / .tlv)",
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
                Text = "📂 Aç",
                Location = new Point(15, 420),
                Size = new Size(70, 42),
                BackColor = Color.FromArgb(40, 40, 50),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                BorderRadius = 20
            };

            btnConvert = new RoundedButton()
            {
                Text = "🔄 Dönüştür",
                Location = new Point(90, 420),
                Size = new Size(95, 42),
                BackColor = Color.FromArgb(0, 120, 212),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                BorderRadius = 20
            };

            btnPlayPause = new RoundedButton()
            {
                Text = "▶ Oynat",
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
                Text = "⏹ Durdur",
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

        private void BtnToggleLoop_Click(object sender, EventArgs e)
        {
            isLoopEnabled = !isLoopEnabled;
            if (isLoopEnabled)
            {
                btnToggleLoop.Text = "🔁 Döngü: Açık";
                btnToggleLoop.BackColor = Color.FromArgb(0, 120, 212);
            }
            else
            {
                btnToggleLoop.Text = "🔁 Döngü: Kapalı";
                btnToggleLoop.BackColor = Color.FromArgb(45, 45, 55);
            }
        }

        private void BtnConvert_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Title = "Dönüştürülecek Medya Dosyasını Seçin";
            ofd.Filter = "Tüm Desteklenenler|*.mp3;*.wav;*.mp4;*.jpg;*.png|Ses Dosyaları (*.mp3;*.wav)|*.mp3;*.wav|Video ve Resim (*.mp4;*.jpg;*.png)|*.mp4;*.jpg;*.png";

            if (ofd.ShowDialog() == DialogResult.OK)
            {
                string ext = Path.GetExtension(ofd.FileName).ToLower();
                bool isAudio = (ext == ".mp3" || ext == ".wav");

                SaveFileDialog sfd = new SaveFileDialog();
                if (isAudio)
                {
                    sfd.Title = ".TLS (Ses) Olarak Kaydet";
                    sfd.Filter = "Tıla Ses Dosyası (*.tls)|*.tls";
                    sfd.FileName = Path.GetFileNameWithoutExtension(ofd.FileName) + ".tls";
                }
                else
                {
                    sfd.Title = ".TLV (Video) Olarak Kaydet";
                    sfd.Filter = "Tıla Video Dosyası (*.tlv)|*.tlv";
                    sfd.FileName = Path.GetFileNameWithoutExtension(ofd.FileName) + ".tlv";
                }

                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        if (isAudio)
                        {
                            ConvertFileToTls(ofd.FileName, sfd.FileName);
                            MessageBox.Show("Ses dosyası başarıyla .tls formatına dönüştürüldü!", "Tamamlandı", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        else
                        {
                            ConvertFileToTlv(ofd.FileName, sfd.FileName);
                            MessageBox.Show("Video/Resim dosyası başarıyla .tlv formatına dönüştürüldü!", "Tamamlandı", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Dönüştürme hatası: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void ConvertFileToTls(string inputPath, string outputPath)
        {
            byte[] rawBytes = File.ReadAllBytes(inputPath);
            byte key = 0x5A;
            byte[] payload = new byte[rawBytes.Length];
            for (int i = 0; i < payload.Length; i++) { payload[i] = (byte)(rawBytes[i] ^ key); }

            using (FileStream fs = new FileStream(outputPath, FileMode.Create))
            using (BinaryWriter bw = new BinaryWriter(fs))
            {
                bw.Write(Encoding.ASCII.GetBytes("TLS2"));
                bw.Write((int)44100);
                bw.Write((short)2);
                bw.Write((short)16);
                bw.Write((int)payload.Length);
                bw.Write(payload);
            }
        }

        private void ConvertFileToTlv(string inputPath, string outputPath)
        {
            byte[] rawBytes = File.ReadAllBytes(inputPath);
            byte key = 0x5A;
            byte[] payload = new byte[rawBytes.Length];
            for (int i = 0; i < payload.Length; i++) { payload[i] = (byte)(rawBytes[i] ^ key); }

            using (FileStream fs = new FileStream(outputPath, FileMode.Create))
            using (BinaryWriter bw = new BinaryWriter(fs))
            {
                bw.Write(Encoding.ASCII.GetBytes("TLV1"));
                bw.Write((int)1920);
                bw.Write((int)1080);
                bw.Write((int)0);
                bw.Write(payload);
            }
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

        private void TbProgress_MouseDown(object sender, MouseEventArgs e)
        {
            isUserSeeking = true;
        }

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

        private void BtnOpen_Click(object sender, EventArgs e)
        {
            // Önceki medyayı tamamen durdurup temizle ki donma yaşanmasın
            StopPlayback();
            CleanupTempFile();

            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "Tıla Medya Dosyaları (*.tls;*.tlv)|*.tls;*.tlv";

            if (ofd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    FileInfo fi = new FileInfo(ofd.FileName);
                    lblPropName.Text = "Dosya: " + fi.Name;

                    byte[] fileBytes = File.ReadAllBytes(ofd.FileName);
                    if (fileBytes.Length < 16)
                    {
                        MessageBox.Show("Dosya yapısı bozuk!", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    string magic = Encoding.ASCII.GetString(fileBytes, 0, 4);

                    if (magic == "TLS2")
                    {
                        isVideoMode = false;
                        pnlVideoHolder.Visible = false;
                        lblCenterIcon.Visible = true;
                        lblSubTitle.Text = "Tıla Ses Dosyası (.tls)";
                        lblPropType.Text = "Biçim: Tıla Ses (.tls)";
                        LoadTlsAudio(fileBytes);
                    }
                    else if (magic == "TLV1")
                    {
                        isVideoMode = true;
                        lblCenterIcon.Visible = false;
                        pnlVideoHolder.Visible = true;
                        lblSubTitle.Text = "Tıla Video Dosyası (.tlv)";
                        lblPropType.Text = "Biçim: Tıla Video (.tlv)";
                        LoadTlvVideo(fileBytes);
                    }
                    else
                    {
                        MessageBox.Show("Geçersiz Tıla medya dosyası!", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    lblTitle.Text = Path.GetFileNameWithoutExtension(ofd.FileName);
                    btnPlayPause.Enabled = true;
                    btnStop.Enabled = true;

                    StartPlayback();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Hata: " + ex.Message);
                }
            }
        }

        private void LoadTlsAudio(byte[] fileBytes)
        {
            int dataSize = BitConverter.ToInt32(fileBytes, 12);
            int pcmDataLen = dataSize;
            if (fileBytes.Length < 16 + pcmDataLen) { pcmDataLen = fileBytes.Length - 16; }

            byte[] encryptedPcm = new byte[pcmDataLen];
            Array.Copy(fileBytes, 16, encryptedPcm, 0, pcmDataLen);

            byte key = 0x5A;
            byte[] pcmData = new byte[encryptedPcm.Length];
            for (int i = 0; i < encryptedPcm.Length; i++)
            {
                pcmData[i] = (byte)(encryptedPcm[i] ^ key);
            }

            tempMediaFile = Path.Combine(Path.GetTempPath(), "tila_audio_" + Guid.NewGuid().ToString("N") + ".mp3");
            File.WriteAllBytes(tempMediaFile, pcmData);

            if (axWmpPlayer != null)
            {
                try
                {
                    dynamic wmp = axWmpPlayer.GetOcx();
                    if (wmp != null)
                    {
                        wmp.uiMode = "none";
                        wmp.URL = tempMediaFile;
                    }
                }
                catch { }
            }
        }

        private void LoadTlvVideo(byte[] fileBytes)
        {
            int audioSize = BitConverter.ToInt32(fileBytes, 12);
            byte key = 0x5A;

            int frameOffset = 16 + audioSize;
            int mediaSize = fileBytes.Length - frameOffset;

            if (mediaSize > 0)
            {
                byte[] encryptedMedia = new byte[mediaSize];
                Array.Copy(fileBytes, frameOffset, encryptedMedia, 0, mediaSize);

                byte[] mediaData = new byte[encryptedMedia.Length];
                for (int i = 0; i < encryptedMedia.Length; i++)
                {
                    mediaData[i] = (byte)(encryptedMedia[i] ^ key);
                }

                tempMediaFile = Path.Combine(Path.GetTempPath(), "tila_video_" + Guid.NewGuid().ToString("N") + ".mp4");
                File.WriteAllBytes(tempMediaFile, mediaData);

                if (axWmpPlayer != null)
                {
                    try
                    {
                        dynamic wmp = axWmpPlayer.GetOcx();
                        if (wmp != null)
                        {
                            wmp.uiMode = "none";
                            wmp.stretchToFit = true;
                            wmp.URL = tempMediaFile;
                        }
                    }
                    catch { }
                }
            }
        }

        private void StartPlayback()
        {
            if (axWmpPlayer != null && !string.IsNullOrEmpty(tempMediaFile))
            {
                try
                {
                    dynamic wmp = axWmpPlayer.GetOcx();
                    if (wmp != null && wmp.controls != null)
                    {
                        wmp.controls.play();
                        wmp.stretchToFit = true;
                    }
                }
                catch { }
            }

            ApplyVolume();
            isPlaying = true;
            btnPlayPause.Text = "⏸ Duraklat";
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
            btnPlayPause.Text = "▶ Oynat";
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
                btnPlayPause.Text = "▶ Oynat";
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

                        if (isVideoMode) wmp.stretchToFit = true;
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
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new GuiPlayer());
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