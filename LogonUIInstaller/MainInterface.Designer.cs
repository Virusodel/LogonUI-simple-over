namespace HorrorTrojan
{
    partial class MainInterface
    {
        private System.ComponentModel.IContainer components = null;

        private void InitializeComponent()
        {
            this.pictureBox = new System.Windows.Forms.PictureBox();
            this.timerLabel = new System.Windows.Forms.Label();
            this.bottomPanel = new System.Windows.Forms.Panel();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox)).BeginInit();
            this.SuspendLayout();

            // pictureBox
            this.pictureBox.Dock = System.Windows.Forms.DockStyle.Top;
            this.pictureBox.Size = new System.Drawing.Size(448, 448);
            this.pictureBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            this.pictureBox.BackColor = System.Drawing.Color.Black;

            // timerLabel
            this.timerLabel.AutoSize = false;
            this.timerLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.timerLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.timerLabel.ForeColor = System.Drawing.Color.Red;
            this.timerLabel.BackColor = System.Drawing.Color.Black;
            this.timerLabel.Font = new System.Drawing.Font("Consolas", 48F, System.Drawing.FontStyle.Bold);
            this.timerLabel.Text = "5:00";

            // bottomPanel
            this.bottomPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.bottomPanel.Height = 72;
            this.bottomPanel.BackColor = System.Drawing.Color.Black;
            this.bottomPanel.Controls.Add(this.timerLabel);

            // MainInterface
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(448, 520);
            this.Controls.Add(this.pictureBox);
            this.Controls.Add(this.bottomPanel);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.TopMost = true;
            this.ShowInTaskbar = false;
            this.BackColor = System.Drawing.Color.Black;
            this.DoubleBuffered = true;
            this.ControlBox = false;
            this.KeyPreview = false;
            this.Name = "MainInterface";
            this.Text = "";

            ((System.ComponentModel.ISupportInitialize)(this.pictureBox)).EndInit();
            this.ResumeLayout(false);
        }

        private System.Windows.Forms.PictureBox pictureBox;
        private System.Windows.Forms.Label timerLabel;
        private System.Windows.Forms.Panel bottomPanel;
    }
}
