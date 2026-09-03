// CustomLogonUI.cs - БЕЗ ОШИБКИ
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

        private const int SW_HIDE = 0;
        private PictureBox pictureBox;

        public GlitchScreen()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.WindowState = FormWindowState.Maximized;
            this.TopMost = true;
            this.ShowInTaskbar = false;
            this.KeyPreview = false;
            this.Bounds = Screen.PrimaryScreen.Bounds;
            
            pictureBox = new PictureBox();
            pictureBox.Dock = DockStyle.Fill;
            pictureBox.SizeMode = PictureBoxSizeMode.StretchImage;
            
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            using (var stream = assembly.GetManifestResourceStream("CustomLogonUI.glitch_effect.gif"))
            {
                if (stream != null)
                {
                    pictureBox.Image = Image.FromStream(stream);
                }
            }
            
            this.Controls.Add(pictureBox);
            
            // Скрываем таскбар
            IntPtr taskbar = FindWindow("Shell_TrayWnd", null);
            if (taskbar != IntPtr.Zero)
                ShowWindow(taskbar, SW_HIDE);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            foreach (Screen screen in Screen.AllScreens)
            {
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
