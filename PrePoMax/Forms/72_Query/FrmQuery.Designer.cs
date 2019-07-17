﻿namespace PrePoMax.Forms
{
    partial class FrmQuery
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
            System.Windows.Forms.ListViewItem listViewItem1 = new System.Windows.Forms.ListViewItem("Bounding box size");
            System.Windows.Forms.ListViewItem listViewItem2 = new System.Windows.Forms.ListViewItem("Assembly");
            System.Windows.Forms.ListViewItem listViewItem3 = new System.Windows.Forms.ListViewItem("Part");
            System.Windows.Forms.ListViewItem listViewItem4 = new System.Windows.Forms.ListViewItem("Point/Node");
            System.Windows.Forms.ListViewItem listViewItem5 = new System.Windows.Forms.ListViewItem("Element");
            System.Windows.Forms.ListViewItem listViewItem6 = new System.Windows.Forms.ListViewItem("Distance");
            System.Windows.Forms.ListViewItem listViewItem7 = new System.Windows.Forms.ListViewItem("Angle");
            System.Windows.Forms.ListViewItem listViewItem8 = new System.Windows.Forms.ListViewItem("Circle");
            this.btnClose = new System.Windows.Forms.Button();
            this.gbQueries = new System.Windows.Forms.GroupBox();
            this.lvQueries = new System.Windows.Forms.ListView();
            this.gbQueries.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnClose
            // 
            this.btnClose.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnClose.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnClose.Location = new System.Drawing.Point(107, 270);
            this.btnClose.Name = "btnClose";
            this.btnClose.Size = new System.Drawing.Size(75, 23);
            this.btnClose.TabIndex = 9;
            this.btnClose.Text = "Close";
            this.btnClose.UseVisualStyleBackColor = true;
            this.btnClose.Click += new System.EventHandler(this.btnClose_Click);
            // 
            // gbQueries
            // 
            this.gbQueries.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.gbQueries.Controls.Add(this.lvQueries);
            this.gbQueries.Location = new System.Drawing.Point(12, 12);
            this.gbQueries.Name = "gbQueries";
            this.gbQueries.Size = new System.Drawing.Size(170, 252);
            this.gbQueries.TabIndex = 10;
            this.gbQueries.TabStop = false;
            this.gbQueries.Text = "Queries";
            // 
            // lvQueries
            // 
            this.lvQueries.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lvQueries.FullRowSelect = true;
            this.lvQueries.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.None;
            this.lvQueries.HideSelection = false;
            listViewItem1.ToolTipText = "Bounding box size";
            listViewItem2.ToolTipText = "Assembly";
            listViewItem3.ToolTipText = "Part";
            listViewItem4.ToolTipText = "Point/Node";
            listViewItem5.ToolTipText = "Element";
            listViewItem6.ToolTipText = "Distance";
            listViewItem7.ToolTipText = "Angle";
            listViewItem8.ToolTipText = "Circle";
            this.lvQueries.Items.AddRange(new System.Windows.Forms.ListViewItem[] {
            listViewItem1,
            listViewItem2,
            listViewItem3,
            listViewItem4,
            listViewItem5,
            listViewItem6,
            listViewItem7,
            listViewItem8});
            this.lvQueries.Location = new System.Drawing.Point(6, 22);
            this.lvQueries.MultiSelect = false;
            this.lvQueries.Name = "lvQueries";
            this.lvQueries.ShowGroups = false;
            this.lvQueries.Size = new System.Drawing.Size(158, 224);
            this.lvQueries.TabIndex = 11;
            this.lvQueries.UseCompatibleStateImageBehavior = false;
            this.lvQueries.View = System.Windows.Forms.View.List;
            this.lvQueries.SelectedIndexChanged += new System.EventHandler(this.lvQueries_SelectedIndexChanged);
            this.lvQueries.MouseDown += new System.Windows.Forms.MouseEventHandler(this.lvQueries_MouseDown);
            this.lvQueries.MouseUp += new System.Windows.Forms.MouseEventHandler(this.lvQueries_MouseUp);
            // 
            // FrmQuery
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(194, 305);
            this.Controls.Add(this.gbQueries);
            this.Controls.Add(this.btnClose);
            this.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "FrmQuery";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.Manual;
            this.Text = "Query";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.FrmQuery_FormClosing);
            this.VisibleChanged += new System.EventHandler(this.FrmQuery_VisibleChanged);
            this.gbQueries.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button btnClose;
        private System.Windows.Forms.GroupBox gbQueries;
        private System.Windows.Forms.ListView lvQueries;
    }
}