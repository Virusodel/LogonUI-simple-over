using System;
using System.Windows.Forms;

namespace CustomLogonUI
{
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
