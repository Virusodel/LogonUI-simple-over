using System;
using System.Drawing;
using System.Windows.Forms;
using System.IO;

namespace HorrorTrojan
{
    public partial class VideoForm : Form
    {
        private PictureBox pictureBox;
        private Image gifImage;

        public VideoForm(string gifPath)
        {
            InitializeComponent();
            LoadGif(gifPath);
        }

        private void InitializeComponent()
        {
            this.pictureBox = new PictureBox();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox)).BeginInit();
            this.SuspendLayout();

            // pictureBox
            this.pictureBox.Dock = DockStyle.Fill;
            this.pictureBox.SizeMode = PictureBoxSizeMode.StretchImage;
            this.pictureBox.BackColor = System.Drawing.Color.Black;

            // VideoForm
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 600);
            this.Controls.Add(this.pictureBox);
            this.FormBorderStyle = FormBorderStyle.None;
            this.WindowState = FormWindowState.Maximized;
            this.TopMost = true;
            this.ShowInTaskbar = false;
            this.BackColor = System.Drawing.Color.Black;
            this.Name = "VideoForm";
            this.Text = "";

            ((System.ComponentModel.ISupportInitialize)(this.pictureBox)).EndInit();
            this.ResumeLayout(false);
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
