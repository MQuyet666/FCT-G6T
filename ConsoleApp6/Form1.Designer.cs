using System.Windows.Forms;

namespace ConsoleApp6
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;

        private SplitContainer splitMain;
        private PictureBox picCamera;
        private Panel pnlLedStatus;
        private Panel pnlStartResult;
        private Label lblStartResult;
        private Button btnStart;

        private GroupBox grpLog;
        private RichTextBox txtLog;
        private Button btnClearLog;
        private Label lblSerial;
        private TextBox txtSerial;
        private Label lblQrCom;
        private ComboBox cmbQrComPort;
        private Button btnQrConnect;
        private Label lblDetectorCom;
        private ComboBox cmbDetectorComPort;
        private Button btnDetectorConnect;

        private GroupBox grpTests;
        private Label lblLedTestTitle;
        private Panel pnlLedTestResult;
        private Label lblLedTestResult;
        private Label lblButtonTestTitle;
        private Panel pnlButtonTestResult;
        private Label lblButtonTestResult;
        private Label lblRssiTestTitle;
        private Panel pnlRssiTestResult;
        private Label lblRssiTestResult;
        private Label lblReadValueTestTitle;
        private Panel pnlReadValueTestResult;
        private Label lblReadValueTestResult;
        private Label lblWdiTestTitle;
        private Panel pnlWdiTestResult;
        private Label lblWdiTestResult;

        private Label lblMainCom;
        private ComboBox cmbComPort;
        private Button btnConnect;
        private RadioButton rdoSmokeDetector;
        private RadioButton rdoHeatDetector;
        private RadioButton rdoPushButton;
        private RadioButton rdoHornStrobe;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            splitMain = new SplitContainer();
            btnStart = new Button();
            pnlStartResult = new Panel();
            lblStartResult = new Label();
            pnlLedStatus = new Panel();
            picCamera = new PictureBox();
            btnConnect = new Button();
            cmbComPort = new ComboBox();
            lblMainCom = new Label();
            rdoPushButton = new RadioButton();
            rdoHornStrobe = new RadioButton();
            rdoHeatDetector = new RadioButton();
            rdoSmokeDetector = new RadioButton();
            grpTests = new GroupBox();
            pnlWdiTestResult = new Panel();
            lblWdiTestResult = new Label();
            lblWdiTestTitle = new Label();
            pnlReadValueTestResult = new Panel();
            lblReadValueTestResult = new Label();
            lblReadValueTestTitle = new Label();
            pnlRssiTestResult = new Panel();
            lblRssiTestResult = new Label();
            lblRssiTestTitle = new Label();
            pnlButtonTestResult = new Panel();
            lblButtonTestResult = new Label();
            lblButtonTestTitle = new Label();
            pnlLedTestResult = new Panel();
            lblLedTestResult = new Label();
            lblLedTestTitle = new Label();
            btnDetectorConnect = new Button();
            cmbDetectorComPort = new ComboBox();
            lblDetectorCom = new Label();
            btnQrConnect = new Button();
            cmbQrComPort = new ComboBox();
            lblQrCom = new Label();
            txtSerial = new TextBox();
            lblSerial = new Label();
            grpLog = new GroupBox();
            btnClearLog = new Button();
            txtLog = new RichTextBox();
            ((System.ComponentModel.ISupportInitialize)splitMain).BeginInit();
            splitMain.Panel1.SuspendLayout();
            splitMain.Panel2.SuspendLayout();
            splitMain.SuspendLayout();
            pnlStartResult.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)picCamera).BeginInit();
            grpTests.SuspendLayout();
            pnlWdiTestResult.SuspendLayout();
            pnlReadValueTestResult.SuspendLayout();
            pnlRssiTestResult.SuspendLayout();
            pnlButtonTestResult.SuspendLayout();
            pnlLedTestResult.SuspendLayout();
            grpLog.SuspendLayout();
            SuspendLayout();
            // 
            // splitMain
            // 
            splitMain.Dock = DockStyle.Fill;
            splitMain.Location = new Point(0, 0);
            splitMain.Margin = new Padding(3, 4, 3, 4);
            splitMain.Name = "splitMain";
            // 
            // splitMain.Panel1
            // 
            splitMain.Panel1.Controls.Add(btnStart);
            splitMain.Panel1.Controls.Add(pnlStartResult);
            splitMain.Panel1.Controls.Add(pnlLedStatus);
            splitMain.Panel1.Controls.Add(picCamera);
            // 
            // splitMain.Panel2
            // 
            splitMain.Panel2.Controls.Add(btnConnect);
            splitMain.Panel2.Controls.Add(cmbComPort);
            splitMain.Panel2.Controls.Add(lblMainCom);
            splitMain.Panel2.Controls.Add(rdoPushButton);
            splitMain.Panel2.Controls.Add(rdoHornStrobe);
            splitMain.Panel2.Controls.Add(rdoHeatDetector);
            splitMain.Panel2.Controls.Add(rdoSmokeDetector);
            splitMain.Panel2.Controls.Add(grpTests);
            splitMain.Panel2.Controls.Add(btnDetectorConnect);
            splitMain.Panel2.Controls.Add(cmbDetectorComPort);
            splitMain.Panel2.Controls.Add(lblDetectorCom);
            splitMain.Panel2.Controls.Add(btnQrConnect);
            splitMain.Panel2.Controls.Add(cmbQrComPort);
            splitMain.Panel2.Controls.Add(lblQrCom);
            splitMain.Panel2.Controls.Add(txtSerial);
            splitMain.Panel2.Controls.Add(lblSerial);
            splitMain.Panel2.Controls.Add(grpLog);
            splitMain.Size = new Size(1316, 826);
            splitMain.SplitterDistance = 744;
            splitMain.TabIndex = 0;
            // 
            // btnStart
            // 
            btnStart.Location = new Point(200, 726);
            btnStart.Margin = new Padding(3, 4, 3, 4);
            btnStart.Name = "btnStart";
            btnStart.Size = new Size(344, 69);
            btnStart.TabIndex = 3;
            btnStart.Text = "Start";
            btnStart.UseVisualStyleBackColor = true;
            // 
            // pnlStartResult
            // 
            pnlStartResult.BackColor = Color.DimGray;
            pnlStartResult.BorderStyle = BorderStyle.FixedSingle;
            pnlStartResult.Controls.Add(lblStartResult);
            pnlStartResult.Location = new Point(100, 636);
            pnlStartResult.Margin = new Padding(3, 4, 3, 4);
            pnlStartResult.Name = "pnlStartResult";
            pnlStartResult.Size = new Size(633, 77);
            pnlStartResult.TabIndex = 2;
            // 
            // lblStartResult
            // 
            lblStartResult.Dock = DockStyle.Fill;
            lblStartResult.ForeColor = Color.White;
            lblStartResult.Location = new Point(0, 0);
            lblStartResult.Name = "lblStartResult";
            lblStartResult.Size = new Size(631, 75);
            lblStartResult.TabIndex = 0;
            lblStartResult.Text = "Result";
            lblStartResult.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // pnlLedStatus
            // 
            pnlLedStatus.BackColor = Color.DarkRed;
            pnlLedStatus.BorderStyle = BorderStyle.FixedSingle;
            pnlLedStatus.Location = new Point(13, 650);
            pnlLedStatus.Margin = new Padding(3, 4, 3, 4);
            pnlLedStatus.Name = "pnlLedStatus";
            pnlLedStatus.Size = new Size(44, 50);
            pnlLedStatus.TabIndex = 1;
            // 
            // picCamera
            // 
            picCamera.BorderStyle = BorderStyle.FixedSingle;
            picCamera.Location = new Point(13, 15);
            picCamera.Margin = new Padding(3, 4, 3, 4);
            picCamera.Name = "picCamera";
            picCamera.Size = new Size(718, 602);
            picCamera.SizeMode = PictureBoxSizeMode.Zoom;
            picCamera.TabIndex = 0;
            picCamera.TabStop = false;
            // 
            // btnConnect
            // 
            btnConnect.Location = new Point(273, 102);
            btnConnect.Margin = new Padding(3, 4, 3, 4);
            btnConnect.Name = "btnConnect";
            btnConnect.Size = new Size(122, 40);
            btnConnect.TabIndex = 11;
            btnConnect.Text = "G6T Connect";
            btnConnect.UseVisualStyleBackColor = true;
            // 
            // cmbComPort
            // 
            cmbComPort.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbComPort.FormattingEnabled = true;
            cmbComPort.Location = new Point(100, 105);
            cmbComPort.Margin = new Padding(3, 4, 3, 4);
            cmbComPort.Name = "cmbComPort";
            cmbComPort.Size = new Size(166, 33);
            cmbComPort.TabIndex = 10;
            // 
            // lblMainCom
            // 
            lblMainCom.AutoSize = true;
            lblMainCom.Location = new Point(14, 109);
            lblMainCom.Name = "lblMainCom";
            lblMainCom.Size = new Size(93, 25);
            lblMainCom.TabIndex = 9;
            lblMainCom.Text = "G6T COM:";
            // 
            // rdoPushButton
            // 
            rdoPushButton.AutoSize = true;
            rdoPushButton.Location = new Point(311, 156);
            rdoPushButton.Margin = new Padding(3, 4, 3, 4);
            rdoPushButton.Name = "rdoPushButton";
            rdoPushButton.Size = new Size(110, 29);
            rdoPushButton.TabIndex = 14;
            rdoPushButton.Text = "Nút nhấn";
            rdoPushButton.UseVisualStyleBackColor = true;
            rdoPushButton.CheckedChanged += rdoPushButton_CheckedChanged;
            // 
            // rdoHornStrobe
            // 
            rdoHornStrobe.AutoSize = true;
            rdoHornStrobe.Location = new Point(433, 156);
            rdoHornStrobe.Margin = new Padding(3, 4, 3, 4);
            rdoHornStrobe.Name = "rdoHornStrobe";
            rdoHornStrobe.Size = new Size(135, 29);
            rdoHornStrobe.TabIndex = 15;
            rdoHornStrobe.Text = "Chuông đèn";
            rdoHornStrobe.UseVisualStyleBackColor = true;
            // 
            // rdoHeatDetector
            // 
            rdoHeatDetector.AutoSize = true;
            rdoHeatDetector.Location = new Point(156, 156);
            rdoHeatDetector.Margin = new Padding(3, 4, 3, 4);
            rdoHeatDetector.Name = "rdoHeatDetector";
            rdoHeatDetector.Size = new Size(149, 29);
            rdoHeatDetector.TabIndex = 13;
            rdoHeatDetector.Text = "Đầu báo nhiệt";
            rdoHeatDetector.UseVisualStyleBackColor = true;
            // 
            // rdoSmokeDetector
            // 
            rdoSmokeDetector.AutoSize = true;
            rdoSmokeDetector.Checked = true;
            rdoSmokeDetector.Location = new Point(19, 156);
            rdoSmokeDetector.Margin = new Padding(3, 4, 3, 4);
            rdoSmokeDetector.Name = "rdoSmokeDetector";
            rdoSmokeDetector.Size = new Size(144, 29);
            rdoSmokeDetector.TabIndex = 12;
            rdoSmokeDetector.TabStop = true;
            rdoSmokeDetector.Text = "Đầu báo khói";
            rdoSmokeDetector.UseVisualStyleBackColor = true;
            // 
            // grpTests
            // 
            grpTests.Controls.Add(pnlWdiTestResult);
            grpTests.Controls.Add(lblWdiTestTitle);
            grpTests.Controls.Add(pnlReadValueTestResult);
            grpTests.Controls.Add(lblReadValueTestTitle);
            grpTests.Controls.Add(pnlRssiTestResult);
            grpTests.Controls.Add(lblRssiTestTitle);
            grpTests.Controls.Add(pnlButtonTestResult);
            grpTests.Controls.Add(lblButtonTestTitle);
            grpTests.Controls.Add(pnlLedTestResult);
            grpTests.Controls.Add(lblLedTestTitle);
            grpTests.Location = new Point(14, 256);
            grpTests.Margin = new Padding(3, 4, 3, 4);
            grpTests.Name = "grpTests";
            grpTests.Padding = new Padding(3, 4, 3, 4);
            grpTests.Size = new Size(539, 220);
            grpTests.TabIndex = 3;
            grpTests.TabStop = false;
            grpTests.Text = "Test Results";
            // 
            // pnlWdiTestResult
            // 
            pnlWdiTestResult.BackColor = Color.DimGray;
            pnlWdiTestResult.BorderStyle = BorderStyle.FixedSingle;
            pnlWdiTestResult.Controls.Add(lblWdiTestResult);
            pnlWdiTestResult.Location = new Point(498, 112);
            pnlWdiTestResult.Margin = new Padding(3, 4, 3, 4);
            pnlWdiTestResult.Name = "pnlWdiTestResult";
            pnlWdiTestResult.Size = new Size(24, 27);
            pnlWdiTestResult.TabIndex = 9;
            // 
            // lblWdiTestResult
            // 
            lblWdiTestResult.Dock = DockStyle.Fill;
            lblWdiTestResult.ForeColor = Color.White;
            lblWdiTestResult.Location = new Point(0, 0);
            lblWdiTestResult.Name = "lblWdiTestResult";
            lblWdiTestResult.Size = new Size(22, 25);
            lblWdiTestResult.TabIndex = 0;
            lblWdiTestResult.Text = "WAIT";
            lblWdiTestResult.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // lblWdiTestTitle
            // 
            lblWdiTestTitle.AutoSize = true;
            lblWdiTestTitle.Location = new Point(278, 112);
            lblWdiTestTitle.Name = "lblWdiTestTitle";
            lblWdiTestTitle.Size = new Size(101, 25);
            lblWdiTestTitle.TabIndex = 8;
            lblWdiTestTitle.Text = "5. WDI Test";
            // 
            // pnlReadValueTestResult
            // 
            pnlReadValueTestResult.BackColor = Color.DimGray;
            pnlReadValueTestResult.BorderStyle = BorderStyle.FixedSingle;
            pnlReadValueTestResult.Controls.Add(lblReadValueTestResult);
            pnlReadValueTestResult.Location = new Point(498, 39);
            pnlReadValueTestResult.Margin = new Padding(3, 4, 3, 4);
            pnlReadValueTestResult.Name = "pnlReadValueTestResult";
            pnlReadValueTestResult.Size = new Size(24, 27);
            pnlReadValueTestResult.TabIndex = 7;
            // 
            // lblReadValueTestResult
            // 
            lblReadValueTestResult.Dock = DockStyle.Fill;
            lblReadValueTestResult.ForeColor = Color.White;
            lblReadValueTestResult.Location = new Point(0, 0);
            lblReadValueTestResult.Name = "lblReadValueTestResult";
            lblReadValueTestResult.Size = new Size(22, 25);
            lblReadValueTestResult.TabIndex = 0;
            lblReadValueTestResult.Text = "WAIT";
            lblReadValueTestResult.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // lblReadValueTestTitle
            // 
            lblReadValueTestTitle.AutoSize = true;
            lblReadValueTestTitle.Location = new Point(278, 41);
            lblReadValueTestTitle.Name = "lblReadValueTestTitle";
            lblReadValueTestTitle.Size = new Size(152, 25);
            lblReadValueTestTitle.TabIndex = 6;
            lblReadValueTestTitle.Text = "4. Read Value Test";
            // 
            // pnlRssiTestResult
            // 
            pnlRssiTestResult.BackColor = Color.DimGray;
            pnlRssiTestResult.BorderStyle = BorderStyle.FixedSingle;
            pnlRssiTestResult.Controls.Add(lblRssiTestResult);
            pnlRssiTestResult.Location = new Point(222, 181);
            pnlRssiTestResult.Margin = new Padding(3, 4, 3, 4);
            pnlRssiTestResult.Name = "pnlRssiTestResult";
            pnlRssiTestResult.Size = new Size(24, 27);
            pnlRssiTestResult.TabIndex = 5;
            // 
            // lblRssiTestResult
            // 
            lblRssiTestResult.Dock = DockStyle.Fill;
            lblRssiTestResult.ForeColor = Color.White;
            lblRssiTestResult.Location = new Point(0, 0);
            lblRssiTestResult.Name = "lblRssiTestResult";
            lblRssiTestResult.Size = new Size(22, 25);
            lblRssiTestResult.TabIndex = 0;
            lblRssiTestResult.Text = "WAIT";
            lblRssiTestResult.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // lblRssiTestTitle
            // 
            lblRssiTestTitle.AutoSize = true;
            lblRssiTestTitle.Location = new Point(18, 184);
            lblRssiTestTitle.Name = "lblRssiTestTitle";
            lblRssiTestTitle.Size = new Size(100, 25);
            lblRssiTestTitle.TabIndex = 4;
            lblRssiTestTitle.Text = "3. Lora Test";
            // 
            // pnlButtonTestResult
            // 
            pnlButtonTestResult.BackColor = Color.DimGray;
            pnlButtonTestResult.BorderStyle = BorderStyle.FixedSingle;
            pnlButtonTestResult.Controls.Add(lblButtonTestResult);
            pnlButtonTestResult.Location = new Point(222, 112);
            pnlButtonTestResult.Margin = new Padding(3, 4, 3, 4);
            pnlButtonTestResult.Name = "pnlButtonTestResult";
            pnlButtonTestResult.Size = new Size(24, 27);
            pnlButtonTestResult.TabIndex = 3;
            // 
            // lblButtonTestResult
            // 
            lblButtonTestResult.Dock = DockStyle.Fill;
            lblButtonTestResult.ForeColor = Color.White;
            lblButtonTestResult.Location = new Point(0, 0);
            lblButtonTestResult.Name = "lblButtonTestResult";
            lblButtonTestResult.Size = new Size(22, 25);
            lblButtonTestResult.TabIndex = 0;
            lblButtonTestResult.Text = "WAIT";
            lblButtonTestResult.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // lblButtonTestTitle
            // 
            lblButtonTestTitle.AutoSize = true;
            lblButtonTestTitle.Location = new Point(18, 112);
            lblButtonTestTitle.Name = "lblButtonTestTitle";
            lblButtonTestTitle.Size = new Size(119, 25);
            lblButtonTestTitle.TabIndex = 2;
            lblButtonTestTitle.Text = "2. Button Test";
            // 
            // pnlLedTestResult
            // 
            pnlLedTestResult.BackColor = Color.DimGray;
            pnlLedTestResult.BorderStyle = BorderStyle.FixedSingle;
            pnlLedTestResult.Controls.Add(lblLedTestResult);
            pnlLedTestResult.Location = new Point(222, 39);
            pnlLedTestResult.Margin = new Padding(3, 4, 3, 4);
            pnlLedTestResult.Name = "pnlLedTestResult";
            pnlLedTestResult.Size = new Size(24, 27);
            pnlLedTestResult.TabIndex = 1;
            // 
            // lblLedTestResult
            // 
            lblLedTestResult.Dock = DockStyle.Fill;
            lblLedTestResult.ForeColor = Color.White;
            lblLedTestResult.Location = new Point(0, 0);
            lblLedTestResult.Name = "lblLedTestResult";
            lblLedTestResult.Size = new Size(22, 25);
            lblLedTestResult.TabIndex = 0;
            lblLedTestResult.Text = "WAIT";
            lblLedTestResult.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // lblLedTestTitle
            // 
            lblLedTestTitle.AutoSize = true;
            lblLedTestTitle.Location = new Point(18, 41);
            lblLedTestTitle.Name = "lblLedTestTitle";
            lblLedTestTitle.Size = new Size(96, 25);
            lblLedTestTitle.TabIndex = 0;
            lblLedTestTitle.Text = "1. LED Test";
            // 
            // btnDetectorConnect
            // 
            btnDetectorConnect.Location = new Point(273, 57);
            btnDetectorConnect.Margin = new Padding(3, 4, 3, 4);
            btnDetectorConnect.Name = "btnDetectorConnect";
            btnDetectorConnect.Size = new Size(122, 40);
            btnDetectorConnect.TabIndex = 8;
            btnDetectorConnect.Text = "DT Connect";
            btnDetectorConnect.UseVisualStyleBackColor = true;
            // 
            // cmbDetectorComPort
            // 
            cmbDetectorComPort.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbDetectorComPort.FormattingEnabled = true;
            cmbDetectorComPort.Location = new Point(100, 61);
            cmbDetectorComPort.Margin = new Padding(3, 4, 3, 4);
            cmbDetectorComPort.Name = "cmbDetectorComPort";
            cmbDetectorComPort.Size = new Size(166, 33);
            cmbDetectorComPort.TabIndex = 7;
            // 
            // lblDetectorCom
            // 
            lblDetectorCom.AutoSize = true;
            lblDetectorCom.Location = new Point(14, 65);
            lblDetectorCom.Name = "lblDetectorCom";
            lblDetectorCom.Size = new Size(83, 25);
            lblDetectorCom.TabIndex = 6;
            lblDetectorCom.Text = "DT COM:";
            // 
            // btnQrConnect
            // 
            btnQrConnect.Location = new Point(273, 15);
            btnQrConnect.Margin = new Padding(3, 4, 3, 4);
            btnQrConnect.Name = "btnQrConnect";
            btnQrConnect.Size = new Size(122, 40);
            btnQrConnect.TabIndex = 5;
            btnQrConnect.Text = "QR Connect";
            btnQrConnect.UseVisualStyleBackColor = true;
            // 
            // cmbQrComPort
            // 
            cmbQrComPort.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbQrComPort.FormattingEnabled = true;
            cmbQrComPort.Location = new Point(100, 18);
            cmbQrComPort.Margin = new Padding(3, 4, 3, 4);
            cmbQrComPort.Name = "cmbQrComPort";
            cmbQrComPort.Size = new Size(166, 33);
            cmbQrComPort.TabIndex = 4;
            // 
            // lblQrCom
            // 
            lblQrCom.AutoSize = true;
            lblQrCom.Location = new Point(14, 21);
            lblQrCom.Name = "lblQrCom";
            lblQrCom.Size = new Size(87, 25);
            lblQrCom.TabIndex = 3;
            lblQrCom.Text = "QR COM:";
            // 
            // txtSerial
            // 
            txtSerial.Location = new Point(80, 212);
            txtSerial.Margin = new Padding(3, 4, 3, 4);
            txtSerial.Name = "txtSerial";
            txtSerial.Size = new Size(473, 31);
            txtSerial.TabIndex = 2;
            // 
            // lblSerial
            // 
            lblSerial.AutoSize = true;
            lblSerial.Location = new Point(14, 216);
            lblSerial.Name = "lblSerial";
            lblSerial.Size = new Size(58, 25);
            lblSerial.TabIndex = 1;
            lblSerial.Text = "Serial:";
            // 
            // grpLog
            // 
            grpLog.Controls.Add(btnClearLog);
            grpLog.Controls.Add(txtLog);
            grpLog.Location = new Point(14, 484);
            grpLog.Margin = new Padding(3, 4, 3, 4);
            grpLog.Name = "grpLog";
            grpLog.Padding = new Padding(3, 4, 3, 4);
            grpLog.Size = new Size(539, 328);
            grpLog.TabIndex = 0;
            grpLog.TabStop = false;
            grpLog.Text = "Log";
            // 
            // btnClearLog
            // 
            btnClearLog.Location = new Point(408, 282);
            btnClearLog.Margin = new Padding(3, 4, 3, 4);
            btnClearLog.Name = "btnClearLog";
            btnClearLog.Size = new Size(122, 38);
            btnClearLog.TabIndex = 1;
            btnClearLog.Text = "Clear Log";
            btnClearLog.UseVisualStyleBackColor = true;
            // 
            // txtLog
            // 
            txtLog.Location = new Point(8, 34);
            txtLog.Margin = new Padding(3, 4, 3, 4);
            txtLog.Name = "txtLog";
            txtLog.ReadOnly = true;
            txtLog.Size = new Size(522, 240);
            txtLog.TabIndex = 0;
            txtLog.Text = "";
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(10F, 25F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1316, 826);
            Controls.Add(splitMain);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            Margin = new Padding(3, 4, 3, 4);
            MaximizeBox = false;
            Name = "Form1";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "FCT G6T: HMQ";
            splitMain.Panel1.ResumeLayout(false);
            splitMain.Panel2.ResumeLayout(false);
            splitMain.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)splitMain).EndInit();
            splitMain.ResumeLayout(false);
            pnlStartResult.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)picCamera).EndInit();
            grpTests.ResumeLayout(false);
            grpTests.PerformLayout();
            pnlWdiTestResult.ResumeLayout(false);
            pnlReadValueTestResult.ResumeLayout(false);
            pnlRssiTestResult.ResumeLayout(false);
            pnlButtonTestResult.ResumeLayout(false);
            pnlLedTestResult.ResumeLayout(false);
            grpLog.ResumeLayout(false);
            ResumeLayout(false);
        }
    }
}
