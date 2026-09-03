// CustomLogonUI.cs
using System;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace CustomLogonUI
{
    public class GlitchScreen : Form
    {
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        
        [DllImport("user32.dll")]
        private static extern IntPtr FindWindow(string className, string windowName);
        
        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        private const int SW_HIDE = 0;
        private const int SW_SHOW = 5;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOZORDER = 0x0004;
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

        private PictureBox pictureBox;
        private System.Windows.Forms.Timer timer;

        public GlitchScreen()
        {
            // Настройка формы на полный экран
            this.FormBorderStyle = FormBorderStyle.None;
            this.WindowState = FormWindowState.Maximized;
            this.TopMost = true;
            this.ShowInTaskbar = false;
            
            // Блокировка ввода
            this.KeyPreview = false;
            this.Cursor = Cursors.None;
            
            // Захват всего экрана
            this.Bounds = Screen.PrimaryScreen.Bounds;
            
            // Инициализация PictureBox
            pictureBox = new PictureBox();
            pictureBox.Dock = DockStyle.Fill;
            pictureBox.SizeMode = PictureBoxSizeMode.StretchImage;
            
            // Загрузка GIF из ресурсов
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            using (var stream = assembly.GetManifestResourceStream("CustomLogonUI.glitch_effect.gif"))
            {
                if (stream != null)
                {
                    pictureBox.Image = Image.FromStream(stream);
                }
            }
            
            this.Controls.Add(pictureBox);
            
            // Скрываем стандартные элементы
            HideTaskbar();
            
            // Таймер для анимации
            timer = new System.Windows.Forms.Timer();
            timer.Interval = 100;
            timer.Tick += (s, e) => pictureBox.Invalidate();
            timer.Start();
        }

        private void HideTaskbar()
        {
            IntPtr taskbar = FindWindow("Shell_TrayWnd", null);
            if (taskbar != IntPtr.Zero)
                ShowWindow(taskbar, SW_HIDE);
                
            IntPtr startButton = FindWindow("Button", null);
            if (startButton != IntPtr.Zero)
                ShowWindow(startButton, SW_HIDE);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            // Захват всех мониторов
            foreach (Screen screen in Screen.AllScreens)
            {
                // Создаем форму для каждого монитора
                if (screen != Screen.PrimaryScreen)
                {
                    var secondaryForm = new GlitchScreen();
                    secondaryForm.StartPosition = FormStartPosition.Manual;
                    secondaryForm.Bounds = screen.Bounds;
                    secondaryForm.Show();
                }
            }
        }

        protected override void WndProc(ref Message m)
        {
            // Блокировка всех системных сообщений
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
                return; // Игнорируем ввод
            }
            base.WndProc(ref m);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            timer?.Stop();
            base.OnFormClosing(e);
        }
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new GlitchScreen());
        }
    }
}
