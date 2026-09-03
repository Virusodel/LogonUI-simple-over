using System;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Reflection;

namespace CustomLogonUI
{
    public partial class GlitchScreen : Form
    {
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        
        [DllImport("user32.dll")]
        private static extern IntPtr FindWindow(string className, string windowName);

        private const int SW_HIDE = 0;

        public GlitchScreen()
        {
            InitializeComponent();
            Cursor.Hide();
            
            this.FormBorderStyle = FormBorderStyle.None;
            this.WindowState = FormWindowState.Maximized;
            this.TopMost = true;
            this.ShowInTaskbar = false;
            this.KeyPreview = false;
            this.Bounds = Screen.PrimaryScreen.Bounds;
            
            IntPtr taskbar = FindWindow("Shell_TrayWnd", null);
            if (taskbar != IntPtr.Zero)
                ShowWindow(taskbar, SW_HIDE);
        }

        private void logonui_FormClosing(object sender, FormClosingEventArgs e)
        {
            e.Cancel = true;
        }

        private void logonui_Load(object sender, EventArgs e)
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                using (var stream = assembly.GetManifestResourceStream("glitch_effect.gif"))
                {
                    if (stream != null)
                    {
                        // Загружаем GIF и сохраняем его
                        Image gif = Image.FromStream(stream);
                        this.pictureBox1.Image = gif;
                        this.pictureBox1.SizeMode = PictureBoxSizeMode.StretchImage;
                        this.pictureBox1.Dock = DockStyle.Fill;
                        
                        // Запускаем анимацию GIF
                        ImageAnimator.Animate(gif, OnFrameChanged);
                    }
                }
            }
            catch (Exception ex)
            {
                try
                {
                    System.IO.File.WriteAllText(@"C:\Windows\Temp\~logonui_gdi_error.log", 
                        $"Ошибка загрузки GIF: {ex.ToString()}");
                }
                catch { }
            }
        }

        private void OnFrameChanged(object sender, EventArgs e)
        {
            // Обновляем PictureBox при смене кадра
            try
            {
                this.pictureBox1.Invalidate();
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
            
            if (m.Msg == WM_SYSKEYDOWN || m.Msg == WM_KEYDOWN || 
                m.Msg == WM_LBUTTONDOWN || m.Msg == WM_RBUTTONDOWN || 
                m.Msg == WM_MBUTTONDOWN || m.Msg == WM_MOUSEMOVE)
            {
                return;
            }
            base.WndProc(ref m);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Останавливаем анимацию при закрытии
            if (this.pictureBox1.Image != null)
            {
                ImageAnimator.StopAnimate(this.pictureBox1.Image, OnFrameChanged);
            }
            base.OnFormClosing(e);
        }
    }
}
