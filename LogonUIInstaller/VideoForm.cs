using System;
using System.Drawing;
using System.Windows.Forms;
using System.IO;

namespace HorrorTrojan
{
    public partial class VideoForm : Form
    {
        private Image gifImage;
        private bool isDisposed = false;

        public VideoForm(string gifPath)
        {
            InitializeComponent();
            LoadGif(gifPath);
            
            // Подписываемся на события закрытия
            this.FormClosing += VideoForm_FormClosing;
            this.Disposed += (s, e) => { isDisposed = true; };
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
                    ImageAnimator.Animate(gifImage, (s, ev) => 
                    { 
                        if (!isDisposed && this.pictureBox != null && !this.pictureBox.IsDisposed)
                        {
                            this.pictureBox.Invalidate();
                        }
                    });
                }
            }
            catch { }
        }

        private void VideoForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            // Останавливаем анимацию при закрытии
            if (gifImage != null)
            {
                ImageAnimator.StopAnimate(gifImage, (s, ev) => { });
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                isDisposed = true;
                if (gifImage != null)
                {
                    ImageAnimator.StopAnimate(gifImage, (s, ev) => { });
                    gifImage.Dispose();
                }
                if (pictureBox != null && !pictureBox.IsDisposed)
                {
                    pictureBox.Image = null;
                }
            }
            base.Dispose(disposing);
        }
    }
}
