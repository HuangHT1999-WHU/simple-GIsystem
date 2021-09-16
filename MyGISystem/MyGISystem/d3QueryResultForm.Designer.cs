namespace MyGISystem
{
    partial class d3QueryResultForm
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
            this.d3QueryResultTreeView = new System.Windows.Forms.TreeView();
            this.SuspendLayout();
            // 
            // d3QueryResultTreeView
            // 
            this.d3QueryResultTreeView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.d3QueryResultTreeView.Location = new System.Drawing.Point(0, 0);
            this.d3QueryResultTreeView.Name = "d3QueryResultTreeView";
            this.d3QueryResultTreeView.Size = new System.Drawing.Size(284, 261);
            this.d3QueryResultTreeView.TabIndex = 0;
            // 
            // d3QueryResultForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(284, 261);
            this.Controls.Add(this.d3QueryResultTreeView);
            this.Name = "d3QueryResultForm";
            this.Text = "三维点查询结果";
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TreeView d3QueryResultTreeView;
    }
}