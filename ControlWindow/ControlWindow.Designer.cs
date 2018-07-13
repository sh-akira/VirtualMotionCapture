namespace ControlWindow
{
    partial class ControlWindow
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
            this.ImportVRMButton = new System.Windows.Forms.Button();
            this.CalibrationButton = new System.Windows.Forms.Button();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.SaveSettingsButton = new System.Windows.Forms.Button();
            this.LoadSettingsButton = new System.Windows.Forms.Button();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.WindowBorderCheckBox = new System.Windows.Forms.CheckBox();
            this.TopMostCheckBox = new System.Windows.Forms.CheckBox();
            this.ColorTransparentButton = new System.Windows.Forms.Button();
            this.ColorCustom1Button = new System.Windows.Forms.Button();
            this.ColorWhiteButton = new System.Windows.Forms.Button();
            this.ColorBlueButton = new System.Windows.Forms.Button();
            this.ColorGreenButton = new System.Windows.Forms.Button();
            this.groupBox3 = new System.Windows.Forms.GroupBox();
            this.CameraGridCheckBox = new System.Windows.Forms.CheckBox();
            this.FreeCameraButton = new System.Windows.Forms.Button();
            this.BackCameraButton = new System.Windows.Forms.Button();
            this.FrontCameraButton = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.WindowClickThroughCheckBox = new System.Windows.Forms.CheckBox();
            this.groupBox1.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.groupBox3.SuspendLayout();
            this.SuspendLayout();
            // 
            // ImportVRMButton
            // 
            this.ImportVRMButton.Location = new System.Drawing.Point(207, 35);
            this.ImportVRMButton.Margin = new System.Windows.Forms.Padding(4);
            this.ImportVRMButton.Name = "ImportVRMButton";
            this.ImportVRMButton.Size = new System.Drawing.Size(122, 62);
            this.ImportVRMButton.TabIndex = 2;
            this.ImportVRMButton.Text = "VRM読込";
            this.ImportVRMButton.UseVisualStyleBackColor = true;
            this.ImportVRMButton.Click += new System.EventHandler(this.ImportVRMButton_Click);
            // 
            // CalibrationButton
            // 
            this.CalibrationButton.Location = new System.Drawing.Point(337, 35);
            this.CalibrationButton.Margin = new System.Windows.Forms.Padding(4);
            this.CalibrationButton.Name = "CalibrationButton";
            this.CalibrationButton.Size = new System.Drawing.Size(168, 62);
            this.CalibrationButton.TabIndex = 3;
            this.CalibrationButton.Text = "キャリブレーション";
            this.CalibrationButton.UseVisualStyleBackColor = true;
            this.CalibrationButton.Click += new System.EventHandler(this.CalibrationButton_Click);
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.CalibrationButton);
            this.groupBox1.Controls.Add(this.ImportVRMButton);
            this.groupBox1.Controls.Add(this.SaveSettingsButton);
            this.groupBox1.Controls.Add(this.LoadSettingsButton);
            this.groupBox1.Location = new System.Drawing.Point(12, 12);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(521, 110);
            this.groupBox1.TabIndex = 1;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "設定";
            // 
            // SaveSettingsButton
            // 
            this.SaveSettingsButton.Location = new System.Drawing.Point(109, 35);
            this.SaveSettingsButton.Margin = new System.Windows.Forms.Padding(4);
            this.SaveSettingsButton.Name = "SaveSettingsButton";
            this.SaveSettingsButton.Size = new System.Drawing.Size(90, 62);
            this.SaveSettingsButton.TabIndex = 1;
            this.SaveSettingsButton.Text = "保存";
            this.SaveSettingsButton.UseVisualStyleBackColor = true;
            this.SaveSettingsButton.Click += new System.EventHandler(this.SaveSettingsButton_Click);
            // 
            // LoadSettingsButton
            // 
            this.LoadSettingsButton.Location = new System.Drawing.Point(11, 35);
            this.LoadSettingsButton.Margin = new System.Windows.Forms.Padding(4);
            this.LoadSettingsButton.Name = "LoadSettingsButton";
            this.LoadSettingsButton.Size = new System.Drawing.Size(90, 62);
            this.LoadSettingsButton.TabIndex = 0;
            this.LoadSettingsButton.Text = "読込";
            this.LoadSettingsButton.UseVisualStyleBackColor = true;
            this.LoadSettingsButton.Click += new System.EventHandler(this.LoadSettingsButton_Click);
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.WindowClickThroughCheckBox);
            this.groupBox2.Controls.Add(this.WindowBorderCheckBox);
            this.groupBox2.Controls.Add(this.TopMostCheckBox);
            this.groupBox2.Controls.Add(this.ColorTransparentButton);
            this.groupBox2.Controls.Add(this.ColorCustom1Button);
            this.groupBox2.Controls.Add(this.ColorWhiteButton);
            this.groupBox2.Controls.Add(this.ColorBlueButton);
            this.groupBox2.Controls.Add(this.ColorGreenButton);
            this.groupBox2.Location = new System.Drawing.Point(12, 128);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(521, 176);
            this.groupBox2.TabIndex = 2;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "背景色(カスタム変更は右クリック)";
            // 
            // WindowBorderCheckBox
            // 
            this.WindowBorderCheckBox.AutoSize = true;
            this.WindowBorderCheckBox.BackColor = System.Drawing.Color.Transparent;
            this.WindowBorderCheckBox.Location = new System.Drawing.Point(286, 104);
            this.WindowBorderCheckBox.Name = "WindowBorderCheckBox";
            this.WindowBorderCheckBox.Size = new System.Drawing.Size(224, 32);
            this.WindowBorderCheckBox.TabIndex = 10;
            this.WindowBorderCheckBox.Text = "ウインドウ枠の非表示";
            this.WindowBorderCheckBox.UseVisualStyleBackColor = false;
            this.WindowBorderCheckBox.CheckedChanged += new System.EventHandler(this.WindowBorderCheckBox_CheckedChanged);
            // 
            // TopMostCheckBox
            // 
            this.TopMostCheckBox.AutoSize = true;
            this.TopMostCheckBox.BackColor = System.Drawing.Color.Transparent;
            this.TopMostCheckBox.Location = new System.Drawing.Point(7, 104);
            this.TopMostCheckBox.Name = "TopMostCheckBox";
            this.TopMostCheckBox.Size = new System.Drawing.Size(279, 32);
            this.TopMostCheckBox.TabIndex = 9;
            this.TopMostCheckBox.Text = "ウインドウを常に手前に表示";
            this.TopMostCheckBox.UseVisualStyleBackColor = false;
            this.TopMostCheckBox.CheckedChanged += new System.EventHandler(this.TopMostCheckBox_CheckedChanged);
            // 
            // ColorTransparentButton
            // 
            this.ColorTransparentButton.Location = new System.Drawing.Point(406, 35);
            this.ColorTransparentButton.Name = "ColorTransparentButton";
            this.ColorTransparentButton.Size = new System.Drawing.Size(104, 62);
            this.ColorTransparentButton.TabIndex = 8;
            this.ColorTransparentButton.Text = "透過";
            this.ColorTransparentButton.UseVisualStyleBackColor = true;
            this.ColorTransparentButton.Click += new System.EventHandler(this.ColorTransparentButton_Click);
            // 
            // ColorCustom1Button
            // 
            this.ColorCustom1Button.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(174)))), ((int)(((byte)(212)))), ((int)(((byte)(255)))));
            this.ColorCustom1Button.Location = new System.Drawing.Point(295, 35);
            this.ColorCustom1Button.Margin = new System.Windows.Forms.Padding(4);
            this.ColorCustom1Button.Name = "ColorCustom1Button";
            this.ColorCustom1Button.Size = new System.Drawing.Size(104, 62);
            this.ColorCustom1Button.TabIndex = 7;
            this.ColorCustom1Button.Text = "カスタム";
            this.ColorCustom1Button.UseVisualStyleBackColor = false;
            this.ColorCustom1Button.Click += new System.EventHandler(this.ColorCustom1Button_Click);
            this.ColorCustom1Button.MouseDown += new System.Windows.Forms.MouseEventHandler(this.ColorCustom1Button_MouseDown);
            // 
            // ColorWhiteButton
            // 
            this.ColorWhiteButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(240)))), ((int)(((byte)(240)))));
            this.ColorWhiteButton.Location = new System.Drawing.Point(199, 35);
            this.ColorWhiteButton.Margin = new System.Windows.Forms.Padding(4);
            this.ColorWhiteButton.Name = "ColorWhiteButton";
            this.ColorWhiteButton.Size = new System.Drawing.Size(88, 62);
            this.ColorWhiteButton.TabIndex = 6;
            this.ColorWhiteButton.Text = "白240";
            this.ColorWhiteButton.UseVisualStyleBackColor = false;
            this.ColorWhiteButton.Click += new System.EventHandler(this.ColorWhiteButton_Click);
            // 
            // ColorBlueButton
            // 
            this.ColorBlueButton.BackColor = System.Drawing.Color.Blue;
            this.ColorBlueButton.Location = new System.Drawing.Point(103, 35);
            this.ColorBlueButton.Margin = new System.Windows.Forms.Padding(4);
            this.ColorBlueButton.Name = "ColorBlueButton";
            this.ColorBlueButton.Size = new System.Drawing.Size(88, 62);
            this.ColorBlueButton.TabIndex = 5;
            this.ColorBlueButton.Text = "BB";
            this.ColorBlueButton.UseVisualStyleBackColor = false;
            this.ColorBlueButton.Click += new System.EventHandler(this.ColorBlueButton_Click);
            // 
            // ColorGreenButton
            // 
            this.ColorGreenButton.BackColor = System.Drawing.Color.Lime;
            this.ColorGreenButton.Location = new System.Drawing.Point(7, 35);
            this.ColorGreenButton.Margin = new System.Windows.Forms.Padding(4);
            this.ColorGreenButton.Name = "ColorGreenButton";
            this.ColorGreenButton.Size = new System.Drawing.Size(88, 62);
            this.ColorGreenButton.TabIndex = 4;
            this.ColorGreenButton.Text = "GB";
            this.ColorGreenButton.UseVisualStyleBackColor = false;
            this.ColorGreenButton.Click += new System.EventHandler(this.ColorGreenButton_Click);
            // 
            // groupBox3
            // 
            this.groupBox3.Controls.Add(this.CameraGridCheckBox);
            this.groupBox3.Controls.Add(this.FreeCameraButton);
            this.groupBox3.Controls.Add(this.BackCameraButton);
            this.groupBox3.Controls.Add(this.FrontCameraButton);
            this.groupBox3.Controls.Add(this.label1);
            this.groupBox3.Location = new System.Drawing.Point(12, 310);
            this.groupBox3.Name = "groupBox3";
            this.groupBox3.Size = new System.Drawing.Size(521, 169);
            this.groupBox3.TabIndex = 3;
            this.groupBox3.TabStop = false;
            this.groupBox3.Text = "カメラコントロール";
            // 
            // CameraGridCheckBox
            // 
            this.CameraGridCheckBox.AutoSize = true;
            this.CameraGridCheckBox.BackColor = System.Drawing.Color.Transparent;
            this.CameraGridCheckBox.Location = new System.Drawing.Point(350, 131);
            this.CameraGridCheckBox.Name = "CameraGridCheckBox";
            this.CameraGridCheckBox.Size = new System.Drawing.Size(159, 32);
            this.CameraGridCheckBox.TabIndex = 14;
            this.CameraGridCheckBox.Text = "グリッドの表示";
            this.CameraGridCheckBox.UseVisualStyleBackColor = false;
            this.CameraGridCheckBox.CheckedChanged += new System.EventHandler(this.CameraGridCheckBox_CheckedChanged);
            // 
            // FreeCameraButton
            // 
            this.FreeCameraButton.Location = new System.Drawing.Point(347, 35);
            this.FreeCameraButton.Margin = new System.Windows.Forms.Padding(4);
            this.FreeCameraButton.Name = "FreeCameraButton";
            this.FreeCameraButton.Size = new System.Drawing.Size(162, 62);
            this.FreeCameraButton.TabIndex = 13;
            this.FreeCameraButton.Text = "フリー";
            this.FreeCameraButton.UseVisualStyleBackColor = true;
            this.FreeCameraButton.Click += new System.EventHandler(this.FreeCameraButton_Click);
            // 
            // BackCameraButton
            // 
            this.BackCameraButton.Location = new System.Drawing.Point(177, 35);
            this.BackCameraButton.Margin = new System.Windows.Forms.Padding(4);
            this.BackCameraButton.Name = "BackCameraButton";
            this.BackCameraButton.Size = new System.Drawing.Size(162, 62);
            this.BackCameraButton.TabIndex = 12;
            this.BackCameraButton.Text = "バック";
            this.BackCameraButton.UseVisualStyleBackColor = true;
            this.BackCameraButton.Click += new System.EventHandler(this.BackCameraButton_Click);
            // 
            // FrontCameraButton
            // 
            this.FrontCameraButton.Location = new System.Drawing.Point(7, 35);
            this.FrontCameraButton.Margin = new System.Windows.Forms.Padding(4);
            this.FrontCameraButton.Name = "FrontCameraButton";
            this.FrontCameraButton.Size = new System.Drawing.Size(162, 62);
            this.FrontCameraButton.TabIndex = 11;
            this.FrontCameraButton.Text = "フロント";
            this.FrontCameraButton.UseVisualStyleBackColor = true;
            this.FrontCameraButton.Click += new System.EventHandler(this.FrontCameraButton_Click);
            // 
            // label1
            // 
            this.label1.BackColor = System.Drawing.Color.Transparent;
            this.label1.Font = new System.Drawing.Font("Meiryo UI", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(128)));
            this.label1.Location = new System.Drawing.Point(6, 101);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(515, 53);
            this.label1.TabIndex = 3;
            this.label1.Text = "フリーカメラの移動は画面上でマウスホイールのスクロール・ドラッグと右クリックでドラッグ";
            // 
            // WindowClickThroughCheckBox
            // 
            this.WindowClickThroughCheckBox.AutoSize = true;
            this.WindowClickThroughCheckBox.BackColor = System.Drawing.Color.Transparent;
            this.WindowClickThroughCheckBox.Location = new System.Drawing.Point(7, 138);
            this.WindowClickThroughCheckBox.Name = "WindowClickThroughCheckBox";
            this.WindowClickThroughCheckBox.Size = new System.Drawing.Size(193, 32);
            this.WindowClickThroughCheckBox.TabIndex = 11;
            this.WindowClickThroughCheckBox.Text = "マウス操作を透過";
            this.WindowClickThroughCheckBox.UseVisualStyleBackColor = false;
            this.WindowClickThroughCheckBox.CheckedChanged += new System.EventHandler(this.WindowClickThroughCheckBox_CheckedChanged);
            // 
            // ControlWindow
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(13F, 28F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.ClientSize = new System.Drawing.Size(541, 491);
            this.Controls.Add(this.groupBox3);
            this.Controls.Add(this.groupBox2);
            this.Controls.Add(this.groupBox1);
            this.Font = new System.Drawing.Font("Meiryo UI", 11F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(128)));
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.MaximizeBox = false;
            this.Name = "ControlWindow";
            this.Text = "コントロールパネル";
            this.groupBox1.ResumeLayout(false);
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            this.groupBox3.ResumeLayout(false);
            this.groupBox3.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button ImportVRMButton;
        private System.Windows.Forms.Button CalibrationButton;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.Button ColorCustom1Button;
        private System.Windows.Forms.Button ColorWhiteButton;
        private System.Windows.Forms.Button ColorBlueButton;
        private System.Windows.Forms.Button ColorGreenButton;
        private System.Windows.Forms.Button ColorTransparentButton;
        private System.Windows.Forms.CheckBox WindowBorderCheckBox;
        private System.Windows.Forms.CheckBox TopMostCheckBox;
        private System.Windows.Forms.GroupBox groupBox3;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button FreeCameraButton;
        private System.Windows.Forms.Button BackCameraButton;
        private System.Windows.Forms.Button FrontCameraButton;
        private System.Windows.Forms.Button SaveSettingsButton;
        private System.Windows.Forms.Button LoadSettingsButton;
        private System.Windows.Forms.CheckBox CameraGridCheckBox;
        private System.Windows.Forms.CheckBox WindowClickThroughCheckBox;
    }
}