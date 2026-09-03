using System;
using System.Drawing;
using System.Windows.Forms;
using System.Media;

namespace logonui
{
    public partial class logonui : Form
    {
        public logonui()
        {
            InitializeComponent();
            Cursor.Hide();
        }

        private void logonui_FormClosing(object sender, FormClosingEventArgs e)
        {
            e.Cancel = true;
        }

        private void logonui_Load(object sender, EventArgs e)
        {
            // Растягиваем картинку на весь экран
            this.pictureBox1.Size = new Size(base.Size.Width, base.Size.Height);
            this.pictureBox1.Dock = DockStyle.Fill;
        }
    }
}
