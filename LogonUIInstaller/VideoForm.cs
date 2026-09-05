using System;
using System.Windows.Forms;
using AxWMPLib;

namespace HorrorTrojan
{
    public partial class VideoForm : Form
    {
        public VideoForm(string videoPath)
        {
            InitializeComponent();
            LoadVideo(videoPath);
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
    }
}
