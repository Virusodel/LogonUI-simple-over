using System;
using System.Windows.Forms;
using AxWMPLib;

namespace HorrorTrojan
{
    public partial class VideoForm : Form
    {
        private AxWindowsMediaPlayer videoPlayer;

        public VideoForm(string videoPath)
        {
            InitializeComponent();
            LoadVideo(videoPath);
        }

        private void InitializeComponent()
        {
            this.videoPlayer = new AxWMPLib.AxWindowsMediaPlayer();
            ((System.ComponentModel.ISupportInitialize)(this.videoPlayer)).BeginInit();
            this.SuspendLayout();

            // videoPlayer
            this.videoPlayer.Dock = DockStyle.Fill;
            this.videoPlayer.Enabled = true;
            this.videoPlayer.Name = "videoPlayer";
            this.videoPlayer.OcxState = ((System.Windows.Forms.AxHost.State)(new System.ComponentModel.ComponentResourceManager(typeof(VideoForm)).GetObject("videoPlayer.OcxState")));
            this.videoPlayer.Size = new System.Drawing.Size(800, 600);
            this.videoPlayer.TabIndex = 0;
            this.videoPlayer.uiMode = "none";

            // VideoForm
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 600);
            this.Controls.Add(this.videoPlayer);
            this.FormBorderStyle = FormBorderStyle.None;
            this.WindowState = FormWindowState.Maximized;
            this.TopMost = true;
            this.ShowInTaskbar = false;
            this.BackColor = System.Drawing.Color.Black;
            this.Name = "VideoForm";
            this.Text = "";

            ((System.ComponentModel.ISupportInitialize)(this.videoPlayer)).EndInit();
            this.ResumeLayout(false);
        }

        private void LoadVideo(string videoPath)
        {
            try
            {
                this.videoPlayer.URL = videoPath;
                this.videoPlayer.settings.autoStart = true;
                this.videoPlayer.settings.setMode("loop", false);
                this.videoPlayer.Ctlcontrols.play();
            }
            catch { }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { videoPlayer?.Dispose(); } catch { }
            }
            base.Dispose(disposing);
        }
    }
}
