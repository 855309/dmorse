namespace dmorse
{
    partial class PlotView
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.plotTabControl = new System.Windows.Forms.TabControl();
            this.SuspendLayout();
            // 
            // plotTabControl
            // 
            this.plotTabControl.Dock = System.Windows.Forms.DockStyle.Fill;
            this.plotTabControl.Location = new System.Drawing.Point(0, 0);
            this.plotTabControl.Name = "plotTabControl";
            this.plotTabControl.SelectedIndex = 0;
            this.plotTabControl.Size = new System.Drawing.Size(751, 414);
            this.plotTabControl.TabIndex = 0;
            // 
            // PlotForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(751, 414);
            this.Controls.Add(this.plotTabControl);
            this.Name = "PlotForm";
            this.Text = "Plot View";
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TabControl plotTabControl;
    }
}