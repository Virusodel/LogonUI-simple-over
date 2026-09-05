using System;
using System.Drawing;
using System.Windows.Forms;
using System.IO;

namespace HorrorTrojan
{
    public partial class VideoForm : Form
    {
        private Image gifImage;

        public VideoForm(string gifPath)
        {
            InitializeComponent();
            LoadGif(gifPath);
        }

        private void LoadGif(string gifPath)
        {
            try
            {
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

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                gifImage?.Dispose();
                pictureBox?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
