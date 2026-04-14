using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ConsoleApp6
{
    public partial class Form1 : Form
    {
        private readonly LedDetector _ledDetector;
        private readonly CameraService _cameraService;
        private readonly SerialService _serialService;
        private readonly SerialService _qrSerialService;
        private readonly SerialService _detectorSerialService;

        private static readonly OpenCvSharp.Rect DefaultLedRoi = new OpenCvSharp.Rect(1300, 1200, 200, 200);
        private static readonly OpenCvSharp.Rect PushButtonLedRoi1 = new OpenCvSharp.Rect(1050, 700, 200, 200);
        private static readonly OpenCvSharp.Rect PushButtonLedRoi2 = new OpenCvSharp.Rect(1000, 900, 200, 200);
        private static readonly OpenCvSharp.Rect PushButtonLedRoi3 = new OpenCvSharp.Rect(950, 1100, 200, 200);
        private const int DetectorDefaultBaudRate = 9600;
        private const int DetectorHornStrobeBaudRate = 115200;

        private readonly object _testLock = new object();
        private readonly object _blinkLock = new object();
        private readonly object _frameLock = new object();
        private readonly System.Windows.Forms.Timer _resultSpinnerTimer;
        private readonly System.Windows.Forms.Timer _comPortRefreshTimer;
        private bool _isTesting;
        private bool _latestLedOn;
        private bool _isStartBlinkCounting;
        private bool _startBlinkDetected;
        private bool _lastLedStateForBlink;
        private readonly bool[] _startBlinkDetectedRois = new bool[3];
        private readonly bool[] _lastLedStateForBlinkRois = new bool[3];
        private readonly bool[] _latestPushButtonLedStates = new bool[3];
        private byte? _lastLedTestResultCode;
        private byte? _lastButtonTestResultCode;
        private bool _suppressOverallResultUpdate;
        private bool _resultWritten;
        private bool _finalPowerCommandSent;
        private int _spinnerAngle;
        private string _lastComPortsKey;
        private readonly List<byte> _g6tRxBuffer = new List<byte>();
        private readonly List<byte> _detectorRxBuffer = new List<byte>();
        private readonly List<byte> _detectorG6tRxBuffer = new List<byte>();
        private TaskCompletionSource<byte?> _pendingResponseTcs;
        private byte _pendingFrameCmd;
        private TaskCompletionSource<byte?> _pendingDetectorResponseTcs;
        private byte _pendingDetectorFrameCmd;
        private TaskCompletionSource<string> _pendingQrScanTcs;
        private TaskCompletionSource<bool?> _pendingDetectorRssiTcs;
        private TaskCompletionSource<bool?> _pendingDetectorReadValueTcs;
        private string _pendingReadValuePattern;
        private readonly StringBuilder _detectorTextBuffer = new StringBuilder();
        private readonly Queue<string> _logQueue = new Queue<string>();
        private bool _logFlushScheduled;
        private const int MaxLogTextLength = 200000;
        private const int LogTrimChunkLength = 50000;
        private string _lastG6tFrameHex;
        private DateTime _lastCameraFrameAt;

        private enum DeviceType
        {
            SmokeDetector,
            HeatDetector,
            PushButton,
            HornStrobe
        }

        private enum MainTestStep
        {
            Led = 1,
            Button = 2,
            Rssi = 3,
            ReadValue = 4,
            Wdi = 5
        }

        private sealed class DeviceTestState
        {
            public bool? LedTestPassed;
            public bool? ButtonTestPassed;
            public bool? RssiTestPassed;
            public bool? ReadValueTestPassed;
            public bool? WdiTestPassed;

            public void Reset()
            {
                LedTestPassed = null;
                ButtonTestPassed = null;
                RssiTestPassed = null;
                ReadValueTestPassed = null;
                WdiTestPassed = null;
            }
        }

        private readonly Dictionary<DeviceType, DeviceTestState> _deviceTestStates =
            new Dictionary<DeviceType, DeviceTestState>
            {
                { DeviceType.SmokeDetector, new DeviceTestState() },
                { DeviceType.HeatDetector, new DeviceTestState() },
                { DeviceType.PushButton, new DeviceTestState() },
                { DeviceType.HornStrobe, new DeviceTestState() }
            };

        public Form1()
        {
            InitializeComponent();

            MakeLedStatusCircle();

            _ledDetector = new LedDetector();
            _cameraService = new CameraService(_ledDetector);
            _serialService = new SerialService();
            _qrSerialService = new SerialService();
            _detectorSerialService = new SerialService();

            _resultSpinnerTimer = new System.Windows.Forms.Timer();
            _resultSpinnerTimer.Interval = 80;
            _resultSpinnerTimer.Tick += ResultSpinnerTimer_Tick;

            _comPortRefreshTimer = new System.Windows.Forms.Timer();
            _comPortRefreshTimer.Interval = 1000;
            _comPortRefreshTimer.Tick += ComPortRefreshTimer_Tick;

            RegisterUiEvents();
            RegisterServiceEvents();
        }

        private void RegisterUiEvents()
        {
            this.Load += Form1_Load;
            this.FormClosing += Form1_FormClosing;
            pnlLedStatus.Resize += (s, e) => MakeLedStatusCircle();

            pnlLedTestResult.Paint += ResultIndicatorPanel_Paint;
            pnlButtonTestResult.Paint += ResultIndicatorPanel_Paint;
            pnlRssiTestResult.Paint += ResultIndicatorPanel_Paint;
            pnlReadValueTestResult.Paint += ResultIndicatorPanel_Paint;
            pnlWdiTestResult.Paint += ResultIndicatorPanel_Paint;

            pnlLedTestResult.Resize += (s, e) => MakeResultPanelCircle(pnlLedTestResult);
            pnlButtonTestResult.Resize += (s, e) => MakeResultPanelCircle(pnlButtonTestResult);
            pnlRssiTestResult.Resize += (s, e) => MakeResultPanelCircle(pnlRssiTestResult);
            pnlReadValueTestResult.Resize += (s, e) => MakeResultPanelCircle(pnlReadValueTestResult);
            pnlWdiTestResult.Resize += (s, e) => MakeResultPanelCircle(pnlWdiTestResult);

            btnStart.Click += async (s, e) => await RunSelectedTestAsync("UI Start");
            btnConnect.Click += BtnConnect_Click;
            btnQrConnect.Click += BtnQrConnect_Click;
            btnDetectorConnect.Click += BtnDetectorConnect_Click;
            btnClearLog.Click += BtnClearLog_Click;

            rdoSmokeDetector.CheckedChanged += DeviceTypeRadio_CheckedChanged;
            rdoHeatDetector.CheckedChanged += DeviceTypeRadio_CheckedChanged;
            rdoPushButton.CheckedChanged += DeviceTypeRadio_CheckedChanged;
            rdoHornStrobe.CheckedChanged += DeviceTypeRadio_CheckedChanged;
        }

        private void MakeLedStatusCircle()
        {
            if (pnlLedStatus.Width <= 0 || pnlLedStatus.Height <= 0)
            {
                return;
            }

            var path = new GraphicsPath();
            path.AddEllipse(0, 0, pnlLedStatus.Width, pnlLedStatus.Height);

            var oldRegion = pnlLedStatus.Region;
            pnlLedStatus.Region = new Region(path);
            oldRegion?.Dispose();
            path.Dispose();
        }

        private void RegisterServiceEvents()
        {
            _cameraService.FrameReady += CameraService_FrameReady;
            _cameraService.LedStatusChanged += CameraService_LedStatusChanged;
            _cameraService.RoiLedStatusUpdated += CameraService_RoiLedStatusUpdated;
            _cameraService.Error += (s, message) => Log("Camera: " + message);

            _serialService.DataReceived += SerialService_DataReceived;
            _serialService.DataReceivedRaw += (s, data) => Log($"G6T ({_serialService.PortName}) RX RAW: {data}");
            _serialService.DataReceivedBytes += SerialService_DataReceivedBytes;
            _serialService.Error += (s, message) => Log($"G6T ({_serialService.PortName}): {message}");
            _serialService.ConnectionChanged += SerialService_ConnectionChanged;

            _qrSerialService.DataReceived += QrSerialService_DataReceived;
            _qrSerialService.DataReceivedRaw += (s, data) => Log($"QR ({_qrSerialService.PortName}) RX RAW: {data}");
            _qrSerialService.Error += (s, message) => Log($"QR ({_qrSerialService.PortName}): {message}");
            _qrSerialService.ConnectionChanged += QrSerialService_ConnectionChanged;

            _detectorSerialService.DataReceived += DetectorSerialService_DataReceived;
            _detectorSerialService.DataReceivedBytes += DetectorSerialService_DataReceivedBytes;
            _detectorSerialService.Error += (s, message) => Log($"DT ({_detectorSerialService.PortName}): {message}");
            _detectorSerialService.ConnectionChanged += DetectorSerialService_ConnectionChanged;
        }

        private void SetDeviceSelectionEnabled(bool enabled)
        {
            if (!IsHandleCreated)
            {
                return;
            }

            if (InvokeRequired)
            {
                BeginInvoke((Action)(() => SetDeviceSelectionEnabled(enabled)));
                return;
            }

            rdoSmokeDetector.Enabled = enabled;
            rdoHeatDetector.Enabled = enabled;
            rdoPushButton.Enabled = enabled;
            rdoHornStrobe.Enabled = enabled;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            LogEvent(nameof(Form1_Load));
            _suppressOverallResultUpdate = true;
            btnConnect.Text = "G6T Connect";
            txtSerial.Text = "SN001";
            SetStartResultDisplay("WAIT", Color.DimGray);
            SetLedTestResultDisplay("WAIT", Color.DimGray);
            SetButtonTestResultDisplay("WAIT", Color.DimGray);
            SetRssiTestResultDisplay("WAIT", Color.DimGray);
            SetReadValueTestResultDisplay("WAIT", Color.DimGray);
            SetWdiTestResultDisplay("WAIT", Color.DimGray);
            _lastLedTestResultCode = null;
            _lastButtonTestResultCode = null;
            ResetCurrentDeviceState();

            lblLedTestResult.Visible = false;
            lblButtonTestResult.Visible = false;
            lblRssiTestResult.Visible = false;
            lblReadValueTestResult.Visible = false;
            lblWdiTestResult.Visible = false;

            MakeResultPanelCircle(pnlLedTestResult);
            MakeResultPanelCircle(pnlButtonTestResult);
            MakeResultPanelCircle(pnlRssiTestResult);
            MakeResultPanelCircle(pnlReadValueTestResult);
            MakeResultPanelCircle(pnlWdiTestResult);

            RefreshComPortList();
            RefreshQrComPortList();
            RefreshDetectorComPortList();
            _lastComPortsKey = BuildComPortsKey(_serialService.GetPortNames());
            _comPortRefreshTimer.Start();
            _suppressOverallResultUpdate = false;
            UpdateTestResultsLayoutForDevice();
            UpdateLedDetectorRoiMode();
            ApplyCurrentDeviceStateToUi();
            var cameraStarted = _cameraService.Start(0);
            Log(cameraStarted ? "Camera started." : "Camera start failed.");
        }

        private void DeviceTypeRadio_CheckedChanged(object sender, EventArgs e)
        {
            var radio = sender as RadioButton;
            LogEvent($"{nameof(DeviceTypeRadio_CheckedChanged)}: {radio?.Name} Checked={radio?.Checked}");
            if (radio == null || !radio.Checked)
            {
                return;
            }

            UpdateTestResultsLayoutForDevice();
            UpdateLedDetectorRoiMode();
            ApplyCurrentDeviceStateToUi();
        }

        private void UpdateLedDetectorRoiMode()
        {
            var isPushButton = GetSelectedDeviceType() == DeviceType.PushButton;
            _ledDetector.UseMultipleRois = isPushButton;

            if (isPushButton)
            {
                _ledDetector.Roi1 = PushButtonLedRoi1;
                _ledDetector.Roi2 = PushButtonLedRoi2;
                _ledDetector.Roi3 = PushButtonLedRoi3;
            }
            else
            {
                _ledDetector.Roi = DefaultLedRoi;
                _ledDetector.Roi2 = default;
                _ledDetector.Roi3 = default;
            }
        }

        private void ApplyCurrentDeviceStateToUi()
        {
            var state = GetCurrentDeviceState();
            var device = GetSelectedDeviceType();

            _suppressOverallResultUpdate = true;
            var led = IsStepRequiredForDevice(device, MainTestStep.Led) ? state.LedTestPassed : (bool?)null;
            var button = IsStepRequiredForDevice(device, MainTestStep.Button) ? state.ButtonTestPassed : (bool?)null;
            var rssi = IsStepRequiredForDevice(device, MainTestStep.Rssi) ? state.RssiTestPassed : (bool?)null;
            var readValue = IsStepRequiredForDevice(device, MainTestStep.ReadValue) ? state.ReadValueTestPassed : (bool?)null;
            var wdi = IsStepRequiredForDevice(device, MainTestStep.Wdi) ? state.WdiTestPassed : (bool?)null;

            SetLedTestResultDisplay(ToResultText(led), ToResultColor(led));
            SetButtonTestResultDisplay(ToResultText(button), ToResultColor(button));
            SetRssiTestResultDisplay(ToResultText(rssi), ToResultColor(rssi));
            SetReadValueTestResultDisplay(ToResultText(readValue), ToResultColor(readValue));
            SetWdiTestResultDisplay(ToResultText(wdi), ToResultColor(wdi));
            _suppressOverallResultUpdate = false;

            var reqLed = IsStepRequiredForDevice(device, MainTestStep.Led) ? state.LedTestPassed : true;
            var reqButton = IsStepRequiredForDevice(device, MainTestStep.Button) ? state.ButtonTestPassed : true;
            var reqRssi = IsStepRequiredForDevice(device, MainTestStep.Rssi) ? state.RssiTestPassed : true;
            var reqReadValue = IsStepRequiredForDevice(device, MainTestStep.ReadValue) ? state.ReadValueTestPassed : true;
            var reqWdi = IsStepRequiredForDevice(device, MainTestStep.Wdi) ? state.WdiTestPassed : true;

            if (reqLed == true && reqButton == true && reqRssi == true && reqReadValue == true && reqWdi == true)
            {
                SetStartResultDisplay(" PASS", Color.SeaGreen);
                return;
            }

            if (reqLed == false || reqButton == false || reqRssi == false || reqReadValue == false || reqWdi == false)
            {
                SetStartResultDisplay(" FAIL", Color.Firebrick);
                return;
            }

            SetStartResultDisplay("WAIT", Color.DimGray);
        }

        private void UpdateTestResultsLayoutForDevice()
        {
            var device = GetSelectedDeviceType();
            var isPushButton = device == DeviceType.PushButton;
            var isHornStrobe = device == DeviceType.HornStrobe;

            lblRssiTestTitle.Visible = !isPushButton;
            pnlRssiTestResult.Visible = !isPushButton;
            lblReadValueTestTitle.Visible = !isPushButton && !isHornStrobe;
            pnlReadValueTestResult.Visible = !isPushButton && !isHornStrobe;
            lblWdiTestTitle.Visible = false;
            pnlWdiTestResult.Visible = false;

            if (isPushButton)
            {
                lblButtonTestTitle.Text = "2. Button Emergency";
                return;
            }

            if (isHornStrobe)
            {
                lblButtonTestTitle.Text = "2. Test nút nhấn 2";
                lblRssiTestTitle.Text = "3. Test nút nhấn";
                lblWdiTestTitle.Text = "4. Test Chuông đèn";
                return;
            }

            lblButtonTestTitle.Text = "2. Button Test";
            lblRssiTestTitle.Text = "3. Lora Test";
            lblReadValueTestTitle.Text = "4. Read Value Test";
            lblWdiTestTitle.Text = "5. WDI Test";
        }

        private static string ToResultText(bool? value)
        {
            if (value == true)
            {
                return "PASS";
            }

            if (value == false)
            {
                return "ERROR";
            }

            return "WAIT";
        }

        private static Color ToResultColor(bool? value)
        {
            if (value == true)
            {
                return Color.SeaGreen;
            }

            if (value == false)
            {
                return Color.Firebrick;
            }

            return Color.DimGray;
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            LogEvent(nameof(Form1_FormClosing));
            _comPortRefreshTimer.Stop();
            _resultSpinnerTimer.Stop();
            _cameraService.Dispose();
            _serialService.Dispose();
            _qrSerialService.Dispose();
            _detectorSerialService.Dispose();

            if (picCamera.Image != null)
            {
                var old = picCamera.Image;
                picCamera.Image = null;
                old.Dispose();
            }
        }

        private void BtnQrConnect_Click(object sender, EventArgs e)
        {
            LogEvent(nameof(BtnQrConnect_Click));
            if (_qrSerialService.IsConnected)
            {
                var portName = _qrSerialService.PortName;
                _qrSerialService.Disconnect();
                Log($"QR ({portName}) disconnected.");
                return;
            }

            if (cmbQrComPort.SelectedItem == null)
            {
                Log("Please select QR COM port.");
                return;
            }

            var port = cmbQrComPort.SelectedItem.ToString();
            var connected = _qrSerialService.Connect(port, 9600);
            Log(connected ? $"QR ({port}) connected." : $"QR ({port}) connect failed.");
        }

        private void BtnDetectorConnect_Click(object sender, EventArgs e)
        {
            LogEvent(nameof(BtnDetectorConnect_Click));
            if (_detectorSerialService.IsConnected)
            {
                var portName = _detectorSerialService.PortName;
                _detectorSerialService.Disconnect();
                Log($"DT ({portName}) disconnected.");
                return;
            }

            if (cmbDetectorComPort.SelectedItem == null)
            {
                Log("Please select detector COM port.");
                return;
            }

            var port = cmbDetectorComPort.SelectedItem.ToString();
            var baudRate = GetDetectorBaudRateForSelectedDevice();
            var connected = _detectorSerialService.Connect(port, baudRate);
            Log(connected ? $"DT ({port}) connected at {baudRate} baud." : $"DT ({port}) connect failed at {baudRate} baud.");
        }

        private void BtnClearLog_Click(object sender, EventArgs e)
        {
            LogEvent(nameof(BtnClearLog_Click));
            txtLog.Clear();
        }

        private async void BtnConnect_Click(object sender, EventArgs e)
        {
            LogEvent(nameof(BtnConnect_Click));
            if (_serialService.IsConnected)
            {
                var portName = _serialService.PortName;
                _serialService.Disconnect();
                Log($"G6T ({portName}) disconnected.");
                return;
            }

            if (cmbComPort.SelectedItem == null)
            {
                Log("Please select G6T COM port.");
                return;
            }

            var port = cmbComPort.SelectedItem.ToString();
            var connected = _serialService.Connect(port, 9600);
            Log(connected ? $"G6T ({port}) connected." : $"G6T ({port}) connect failed.");

            if (connected)
            {
                var connectAck = await SendFrameAndWaitResponseAsync(0x01, 0x00);
                if (connectAck == 0x06)
                {
                    Log("G6T connect ACK received: 0x06 (success)");
                    await SendExtendedCommand0805Async("after G6T connect", 0x00);
                }
                else if (connectAck.HasValue)
                {
                    Log($"G6T connect ACK failed: 0x{connectAck.Value:X2}");
                }
                else
                {
                    Log("G6T connect ACK timeout.");
                }
            }
        }

        private async Task RunSelectedTestAsync(string source)
        {
            LogEvent($"{nameof(RunSelectedTestAsync)}: {source}");
            SetDeviceSelectionEnabled(false);
            try
            {
                if (!_serialService.IsConnected)
                {
                    Log("Please connect G6T COM port before starting test.");
                    return;
                }
                const int selected = 1;

                _resultWritten = false;
                _finalPowerCommandSent = false;

                Log($"Running test {selected} ({source})");

                var serialValue = txtSerial.Text.Trim();
                if (string.IsNullOrWhiteSpace(serialValue))
                {
                    serialValue = await TryReadQrSerialAsync();
                    if (!string.IsNullOrWhiteSpace(serialValue))
                    {
                        txtSerial.Text = serialValue;
                    }
                    else
                    {
                        FailAndStopAllTests("QR scan timeout.");
                        return;
                    }
                }

                if (string.Equals(source, "UI Start", StringComparison.Ordinal))
                {
                    if (GetSelectedDeviceType() == DeviceType.HornStrobe)
                    {
                        await RunHornStrobeTestFlowAsync();
                        return;
                    }

                    SetStartResultDisplay("WAIT", Color.DimGray);
                    SetLedTestResultDisplay("WAIT", Color.DimGray);
                    SetButtonTestResultDisplay("WAIT", Color.DimGray);
                    SetRssiTestResultDisplay("WAIT", Color.DimGray);
                    SetReadValueTestResultDisplay("WAIT", Color.DimGray);
                    SetWdiTestResultDisplay("WAIT", Color.DimGray);
                    ResetCurrentDeviceState();

                    var powerData = (byte)0x01;
                    StartBlinkCounting();
                    var powerAckTask = ExecuteStartFramesAsync(powerData);
                    var blinkWindowTask = Task.Delay(TimeSpan.FromSeconds(3));
                    var powerAck = await powerAckTask;

                    // After Start (cmd=0x01) ACK, send cmd=0x03 data=0x00 and wait ACK 3s.
                    // TX example: 1F 2F 3F FF 00 03 00 EC
                    // RX ACK:     1F 2F 3F FF 01 03 {0x06: success, 0x15: failed} <BCC>
                    if (powerAck)
                    {
                        await SendStartExtraFrame03Async();

                        // Additional frame requested when Start is pressed.
                        await SendStartExtraFrame04Async();
                    }

                    await blinkWindowTask;
                    var blinkDetected = StopBlinkCounting();

                    if (!powerAck)
                    {
                        SetTestResultsErrorFrom(MainTestStep.Led);
                        FailAndStopAllTests("Power ACK failed.");
                        return;
                    }

                    SetStartResultDisplay("RUNNING", Color.SteelBlue);

                    if (blinkDetected)
                    {
                        var resultCode = (byte)0x02;
                        SetStartResultDisplay(" PASS", Color.SeaGreen);
                        ApplyLedTestResultCode(resultCode);

                        await RunButtonTestAsync();
                    }
                    else
                    {
                        var resultCode = (byte)0x03;
                        _serialService.SendByte(resultCode);
                        Log($"G6T ({_serialService.PortName}) TX: 0x03 (no LED blink detected)");
                        ApplyLedTestResultCode(resultCode);
                        SetTestResultsErrorFrom(MainTestStep.Button);
                        FailAndStopAllTests("LED blink not detected.");
                    }

                    return;
                }

                await RunTestInternalAsync(selected, source);
            }
            finally
            {
                SetDeviceSelectionEnabled(true);
            }
        }

        private void SerialService_DataReceivedBytes(object sender, byte[] data)
        {
            LogEvent(nameof(SerialService_DataReceivedBytes));
            if (data == null || data.Length == 0)
            {
                return;
            }

            Log($"G6T ({_serialService.PortName}) RX RAW HEX: {ToHex(data)}");

            ParseG6tFrames(data, _serialService.PortName);
        }

        private void StartBlinkCounting()
        {
            lock (_blinkLock)
            {
                _startBlinkDetected = false;
                _lastLedStateForBlink = _latestLedOn;
                _isStartBlinkCounting = true;

                if (GetSelectedDeviceType() == DeviceType.PushButton)
                {
                    Array.Clear(_startBlinkDetectedRois, 0, _startBlinkDetectedRois.Length);
                    Array.Copy(_latestPushButtonLedStates, _lastLedStateForBlinkRois, _lastLedStateForBlinkRois.Length);
                }
            }

            Log("Monitor LED blink...");
        }

        private bool StopBlinkCounting()
        {
            bool detected;
            lock (_blinkLock)
            {
                if (GetSelectedDeviceType() == DeviceType.PushButton)
                {
                    detected = _startBlinkDetectedRois.All(blinked => blinked);
                    Array.Clear(_startBlinkDetectedRois, 0, _startBlinkDetectedRois.Length);
                }
                else
                {
                    detected = _startBlinkDetected;
                }

                _isStartBlinkCounting = false;
                _startBlinkDetected = false;
            }

            return detected;
        }

        private bool StopBlinkCountingRoi3()
        {
            lock (_blinkLock)
            {
                var detected = GetSelectedDeviceType() == DeviceType.PushButton && _startBlinkDetectedRois.Length > 2 && _startBlinkDetectedRois[2];
                _isStartBlinkCounting = false;
                _startBlinkDetected = false;
                Array.Clear(_startBlinkDetectedRois, 0, _startBlinkDetectedRois.Length);
                return detected;
            }
        }

        private async Task RunTestInternalAsync(int testNumber, string source)
        {
            lock (_testLock)
            {
                if (_isTesting)
                {
                    Log("A test is already running.");
                    return;
                }
                _isTesting = true;
            }

            try
            {
                var serial = txtSerial.Text.Trim();
                if (string.IsNullOrWhiteSpace(serial))
                {
                    Log("Serial is required.");
                    return;
                }

                Log(string.Format("Start Test {0} ({1})...", testNumber, source));
                await Task.Delay(300);

                var result = new TestResult
                {
                    Serial = serial,
                    TestNumber = testNumber,
                    LedOn = _latestLedOn,
                    Pass = _latestLedOn,
                    Timestamp = DateTime.Now
                };

                UpdateLedUi(result.LedOn);
                Log(result.ToLogLine());

                if (!string.Equals(source, "UI Start", StringComparison.Ordinal))
                {
                    SetLedTestResultDisplay(result.Pass ? "PASS" : "ERROR", result.Pass ? Color.SeaGreen : Color.Firebrick);
                }

                if (_serialService.IsConnected && !string.Equals(source, "UI Start", StringComparison.Ordinal))
                {
                    var payload = result.ToComPayload();
                    _serialService.Send(payload);
                    Log($"G6T ({_serialService.PortName}) TX: {payload.Trim()}");
                }
            }
            finally
            {
                lock (_testLock)
                {
                    _isTesting = false;
                }
            }
        }

        private void RefreshComPortList()
        {
            var current = cmbComPort.SelectedItem != null ? cmbComPort.SelectedItem.ToString() : string.Empty;
            var ports = _serialService.GetPortNames().OrderBy(p => p).ToArray();

            cmbComPort.Items.Clear();
            cmbComPort.Items.AddRange(ports);

            if (!string.IsNullOrEmpty(current) && ports.Contains(current))
            {
                cmbComPort.SelectedItem = current;
            }
            else if (ports.Length > 0)
            {
                cmbComPort.SelectedIndex = 0;
            }
        }

        private void RefreshQrComPortList()
        {
            var current = cmbQrComPort.SelectedItem != null ? cmbQrComPort.SelectedItem.ToString() : string.Empty;
            var ports = _serialService.GetPortNames().OrderBy(p => p).ToArray();

            cmbQrComPort.Items.Clear();
            cmbQrComPort.Items.AddRange(ports);

            if (!string.IsNullOrEmpty(current) && ports.Contains(current))
            {
                cmbQrComPort.SelectedItem = current;
            }
            else if (ports.Length > 0)
            {
                cmbQrComPort.SelectedIndex = 0;
            }
        }

        private void RefreshDetectorComPortList()
        {
            var current = cmbDetectorComPort.SelectedItem != null ? cmbDetectorComPort.SelectedItem.ToString() : string.Empty;
            var ports = _serialService.GetPortNames().OrderBy(p => p).ToArray();

            cmbDetectorComPort.Items.Clear();
            cmbDetectorComPort.Items.AddRange(ports);

            if (!string.IsNullOrEmpty(current) && ports.Contains(current))
            {
                cmbDetectorComPort.SelectedItem = current;
            }
            else if (ports.Length > 0)
            {
                cmbDetectorComPort.SelectedIndex = 0;
            }
        }

        private void ComPortRefreshTimer_Tick(object sender, EventArgs e)
        {
            var ports = _serialService.GetPortNames();
            var key = BuildComPortsKey(ports);
            if (string.Equals(key, _lastComPortsKey, StringComparison.Ordinal))
            {
                return;
            }

            _lastComPortsKey = key;

            RefreshComPortList();
            RefreshQrComPortList();
            RefreshDetectorComPortList();
        }

        private static string BuildComPortsKey(IEnumerable<string> ports)
        {
            return string.Join("|", ports.OrderBy(p => p));
        }

        private void CameraService_FrameReady(object sender, Bitmap bitmap)
        {
            _lastCameraFrameAt = DateTime.UtcNow;

            if (!IsHandleCreated)
            {
                bitmap.Dispose();
                return;
            }

            BeginInvoke((Action)(() =>
            {
                var old = picCamera.Image;
                picCamera.Image = bitmap;
                if (old != null)
                {
                    old.Dispose();
                }
            }));
        }

        private void CameraService_LedStatusChanged(object sender, bool ledOn)
        {
            LogEvent($"{nameof(CameraService_LedStatusChanged)}: LedOn={ledOn}");
            lock (_blinkLock)
            {
                if (_isStartBlinkCounting && ledOn && !_lastLedStateForBlink)
                {
                    _startBlinkDetected = true;
                }

                _lastLedStateForBlink = ledOn;
            }

            _latestLedOn = ledOn;
            UpdateLedUi(ledOn);
        }

        private void CameraService_RoiLedStatusUpdated(object sender, IReadOnlyList<LedDetector.LedRoiDetection> detections)
        {
            if (detections == null || detections.Count == 0)
            {
                return;
            }

            lock (_blinkLock)
            {
                var count = Math.Min(3, detections.Count);
                for (var i = 0; i < count; i++)
                {
                    var ledOn = detections[i].LedOn;
                    _latestPushButtonLedStates[i] = ledOn;

                    if (_isStartBlinkCounting && GetSelectedDeviceType() == DeviceType.PushButton)
                    {
                        if (ledOn && !_lastLedStateForBlinkRois[i])
                        {
                            _startBlinkDetectedRois[i] = true;
                        }

                        _lastLedStateForBlinkRois[i] = ledOn;
                    }
                }
            }
        }

        private void UpdateLedUi(bool ledOn)
        {
            if (!IsHandleCreated)
            {
                return;
            }

            if (InvokeRequired)
            {
                BeginInvoke((Action)(() => UpdateLedUi(ledOn)));
                return;
            }

            pnlLedStatus.BackColor = ledOn ? Color.LimeGreen : Color.DarkRed;
        }

        private void SetStartResultDisplay(string text, Color backColor)
        {
            if (!IsHandleCreated)
            {
                return;
            }

            if (InvokeRequired)
            {
                BeginInvoke((Action)(() => SetStartResultDisplay(text, backColor)));
                return;
            }

            pnlStartResult.BackColor = backColor;
            lblStartResult.Text = text;
        }

        private void SetLedTestResultDisplay(string text, Color backColor)
        {
            SetResultIndicatorDisplay(pnlLedTestResult, text, backColor);
        }

        private void SerialService_DataReceived(object sender, string data)
        {
            LogEvent(nameof(SerialService_DataReceived));
            Log($"G6T ({_serialService.PortName}) RX: {data}");

            if (data.IndexOf("0x02", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                ApplyLedTestResultCode(0x02);
            }
            else if (data.IndexOf("0x03", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                ApplyLedTestResultCode(0x03);
            }

            if (data.IndexOf("0x04", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                ApplyButtonTestResultCode(0x04);
            }
            else if (data.IndexOf("0x05", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                ApplyButtonTestResultCode(0x05);
            }

            if (data.IndexOf("Start", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (!IsHandleCreated)
                {
                    return;
                }

                BeginInvoke((Action)(async () => await RunSelectedTestAsync($"UART Start ({_serialService.PortName})")));
            }
        }

        private void SerialService_ConnectionChanged(object sender, bool connected)
        {
            LogEvent($"{nameof(SerialService_ConnectionChanged)}: Connected={connected}");
            if (!IsHandleCreated)
            {
                return;
            }

            if (InvokeRequired)
            {
                BeginInvoke((Action)(() => SerialService_ConnectionChanged(sender, connected)));
                return;
            }

            btnConnect.Text = connected ? "G6T" : "G6T Connect";

            if (!connected)
            {
                lock (_frameLock)
                {
                    _g6tRxBuffer.Clear();
                    if (_pendingResponseTcs != null)
                    {
                        _pendingResponseTcs.TrySetResult(null);
                        _pendingResponseTcs = null;
                    }
                }
            }
        }

        private void QrSerialService_ConnectionChanged(object sender, bool connected)
        {
            LogEvent($"{nameof(QrSerialService_ConnectionChanged)}: Connected={connected}");
            if (!IsHandleCreated)
            {
                return;
            }

            if (InvokeRequired)
            {
                BeginInvoke((Action)(() => QrSerialService_ConnectionChanged(sender, connected)));
                return;
            }

            btnQrConnect.Text = connected ? "QR Disconnect" : "QR Connect";
        }

        private void DetectorSerialService_ConnectionChanged(object sender, bool connected)
        {
            LogEvent($"{nameof(DetectorSerialService_ConnectionChanged)}: Connected={connected}");
            if (!IsHandleCreated)
            {
                return;
            }

            if (InvokeRequired)
            {
                BeginInvoke((Action)(() => DetectorSerialService_ConnectionChanged(sender, connected)));
                return;
            }

            btnDetectorConnect.Text = connected ? "DT Disconnect" : "DT Connect";

            if (!connected && _pendingDetectorRssiTcs != null)
            {
                _pendingDetectorRssiTcs.TrySetResult(null);
                _pendingDetectorRssiTcs = null;
            }

            if (!connected && _pendingDetectorReadValueTcs != null)
            {
                _pendingDetectorReadValueTcs.TrySetResult(null);
                _pendingDetectorReadValueTcs = null;
                _pendingReadValuePattern = null;
            }

            if (!connected)
            {
                _detectorTextBuffer.Clear();
            }

            if (!connected)
            {
                lock (_frameLock)
                {
                    _detectorG6tRxBuffer.Clear();
                    if (_pendingDetectorResponseTcs != null)
                    {
                        _pendingDetectorResponseTcs.TrySetResult(null);
                        _pendingDetectorResponseTcs = null;
                    }
                }
            }
        }

        private void DetectorSerialService_DataReceivedBytes(object sender, byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                return;
            }

            Log($"DT ({_detectorSerialService.PortName}) RX RAW HEX: {ToHex(data)}");

            ParseDetectorG6tFrames(data, _detectorSerialService.PortName);

            ParseDetectorFrames(data);
        }

        private void QrSerialService_DataReceived(object sender, string data)
        {
            LogEvent(nameof(QrSerialService_DataReceived));
            Log($"QR ({_qrSerialService.PortName}) RX RAW: {data}");
            var qrValue = data == null ? string.Empty : data.Trim();
            if (string.IsNullOrWhiteSpace(qrValue))
            {
                return;
            }

            var tcs = _pendingQrScanTcs;
            if (tcs != null)
            {
                tcs.TrySetResult(qrValue);
            }

            if (!IsHandleCreated)
            {
                return;
            }

            BeginInvoke((Action)(() =>
            {
                txtSerial.Text = qrValue;
                Log($"QR ({_qrSerialService.PortName}) Serial filled: {qrValue}");
            }));
        }

        private async Task<string> TryReadQrSerialAsync()
        {
            if (!_qrSerialService.IsConnected)
            {
                Log("QR COM is not connected. Please connect QR COM or enter Serial.");
                return string.Empty;
            }

            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pendingQrScanTcs = tcs;

            SendQrCommand('Z');
            Log($"QR ({_qrSerialService.PortName}) trigger scan.");

            var completed = await Task.WhenAny(tcs.Task, Task.Delay(3000));
            _pendingQrScanTcs = null;

            if (completed != tcs.Task)
            {
                Log($"QR ({_qrSerialService.PortName}) scan timeout.");
                SendQrCommand('Y');
                return string.Empty;
            }

            var value = await tcs.Task;
            SendQrCommand('B');
            SendQrCommand('Y');
            return value;
        }

        private void SendQrCommand(char commandId)
        {
            var packet = new[] { (byte)0x1B, (byte)commandId, (byte)0x0D };
            _qrSerialService.SendBytes(packet);
        }

        private void DetectorSerialService_DataReceived(object sender, string data)
        {
            if (string.IsNullOrWhiteSpace(data))
            {
                return;
            }

            var text = data.Trim();
            Log($"DT ({_detectorSerialService.PortName}) RX RAW: {text}");

            _detectorTextBuffer.Append(text);
            if (_detectorTextBuffer.Length > 2048)
            {
                _detectorTextBuffer.Remove(0, _detectorTextBuffer.Length - 2048);
            }

            var mergedText = _detectorTextBuffer.ToString();

            var rssiParsed = ParseDetectorRssiAck(mergedText);
            if (rssiParsed.HasValue && _pendingDetectorRssiTcs != null)
            {
                _pendingDetectorRssiTcs.TrySetResult(rssiParsed.Value);
                _pendingDetectorRssiTcs = null;
                _detectorTextBuffer.Clear();
            }

            var readValueParsed = ParseDetectorReadValueAck(mergedText);
            if (readValueParsed.HasValue && _pendingDetectorReadValueTcs != null)
            {
                _pendingDetectorReadValueTcs.TrySetResult(readValueParsed.Value);
                _pendingDetectorReadValueTcs = null;
                _detectorTextBuffer.Clear();
            }
        }

        private void ParseDetectorFrames(byte[] data)
        {
            lock (_frameLock)
            {
                _detectorRxBuffer.AddRange(data);

                while (_detectorRxBuffer.Count > 0)
                {
                    var consumed = TryProcessDetectorAsciiFrame();
                    if (consumed)
                    {
                        continue;
                    }

                    consumed = TryProcessDetectorBinaryFrame();
                    if (consumed)
                    {
                        continue;
                    }

                    if (_detectorRxBuffer.Count > 4096)
                    {
                        _detectorRxBuffer.Clear();
                    }

                    break;
                }
            }
        }

        private bool TryProcessDetectorAsciiFrame()
        {
            var ascii = Encoding.ASCII.GetString(_detectorRxBuffer.ToArray());
            var start = ascii.IndexOf("<STX>", StringComparison.OrdinalIgnoreCase);
            if (start < 0)
            {
                return false;
            }

            var end = ascii.IndexOf("<BCC>", start, StringComparison.OrdinalIgnoreCase);
            if (end < 0)
            {
                return false;
            }

            var frameText = ascii.Substring(start, end - start + 5);
            var consumedByteCount = Encoding.ASCII.GetByteCount(ascii.Substring(0, end + 5));
            _detectorRxBuffer.RemoveRange(0, consumedByteCount);

            Log($"DT ({_detectorSerialService.PortName}) RX FRAME: {frameText}");

            var parsed = ParseDetectorRssiAck(frameText);
            if (parsed.HasValue && _pendingDetectorRssiTcs != null)
            {
                _pendingDetectorRssiTcs.TrySetResult(parsed.Value);
                _pendingDetectorRssiTcs = null;
            }

            var readValueParsed = ParseDetectorReadValueAck(frameText);
            if (readValueParsed.HasValue && _pendingDetectorReadValueTcs != null)
            {
                _pendingDetectorReadValueTcs.TrySetResult(readValueParsed.Value);
                _pendingDetectorReadValueTcs = null;
            }

            return true;
        }

        private bool TryProcessDetectorBinaryFrame()
        {
            var stxIndex = _detectorRxBuffer.IndexOf(0x02);
            if (stxIndex < 0)
            {
                return false;
            }

            var etxIndex = -1;
            for (var i = stxIndex + 1; i < _detectorRxBuffer.Count; i++)
            {
                if (_detectorRxBuffer[i] == 0x03)
                {
                    etxIndex = i;
                    break;
                }
            }

            if (etxIndex < 0 || etxIndex + 1 >= _detectorRxBuffer.Count)
            {
                return false;
            }

            var bccIndex = etxIndex + 1;
            var frameLength = (bccIndex - stxIndex) + 1;
            var frame = _detectorRxBuffer.Skip(stxIndex).Take(frameLength).ToArray();
            _detectorRxBuffer.RemoveRange(0, stxIndex + frameLength);

            if (ComputeDetectorBcc(frame, frame.Length - 1) != frame[frame.Length - 1])
            {
                return true;
            }

            var payloadLength = etxIndex - stxIndex - 1;
            var payload = payloadLength > 0 ? Encoding.ASCII.GetString(frame, 1, payloadLength) : string.Empty;

            Log($"DT ({_detectorSerialService.PortName}) RX FRAME HEX: {ToHex(frame)}");

            var parsed = ParseDetectorRssiAck(payload);
            if (parsed.HasValue && _pendingDetectorRssiTcs != null)
            {
                _pendingDetectorRssiTcs.TrySetResult(parsed.Value);
                _pendingDetectorRssiTcs = null;
            }

            var readValueParsed = ParseDetectorReadValueAck(payload);
            if (readValueParsed.HasValue && _pendingDetectorReadValueTcs != null)
            {
                _pendingDetectorReadValueTcs.TrySetResult(readValueParsed.Value);
                _pendingDetectorReadValueTcs = null;
            }

            return true;
        }

        private static byte ComputeDetectorBcc(byte[] buffer, int length)
        {
            byte bcc = 0;
            for (var i = 1; i < length; i++)
            {
                bcc ^= buffer[i];
            }

            return bcc;
        }

        private void Log(string message)
        {
            if (!IsHandleCreated)
            {
                return;
            }

            if (IsDisposed || Disposing)
            {
                return;
            }

            lock (_logQueue)
            {
                _logQueue.Enqueue(message ?? string.Empty);

                while (_logQueue.Count > 5000)
                {
                    _logQueue.Dequeue();
                }

                if (_logFlushScheduled)
                {
                    return;
                }

                _logFlushScheduled = true;
            }

            BeginInvoke((Action)FlushLogQueue);
        }

        private void FlushLogQueue()
        {
            if (!IsHandleCreated || IsDisposed || Disposing)
            {
                return;
            }

            const int maxLinesPerFlush = 200;
            var flushed = 0;
            var batch = new StringBuilder();
            while (flushed < maxLinesPerFlush)
            {
                string message;
                lock (_logQueue)
                {
                    if (_logQueue.Count == 0)
                    {
                        _logFlushScheduled = false;
                        break;
                    }

                    message = _logQueue.Dequeue();
                }

                batch.AppendFormat("[{0:HH:mm:ss}] {1}{2}", DateTime.Now, message, Environment.NewLine);
                flushed++;
            }

            if (batch.Length > 0)
            {
                AppendLogText(batch.ToString());
            }

            lock (_logQueue)
            {
                if (_logQueue.Count > 0)
                {
                    BeginInvoke((Action)FlushLogQueue);
                }
                else
                {
                    _logFlushScheduled = false;
                }
            }
        }

        private void AppendLogText(string text)
        {
            if (txtLog.TextLength > MaxLogTextLength)
            {
                var trimLength = Math.Min(LogTrimChunkLength, txtLog.TextLength);
                txtLog.Select(0, trimLength);
                txtLog.SelectedText = string.Empty;
            }

            txtLog.AppendText(text);
            txtLog.SelectionStart = txtLog.TextLength;
            txtLog.ScrollToCaret();
        }

        private void LogFrame(string prefix, string portName, byte[] frame)
        {
            var port = string.IsNullOrWhiteSpace(portName) ? "" : $"{portName}: ";
            var frameHex = ToHex(frame);
            Log($"{prefix} {port}Frame: {frameHex}");
        }

        private void LogEvent(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            Log($"Event: {name}");
        }

        private void ApplyLedTestResultCode(byte resultCode)
        {
            _lastLedTestResultCode = resultCode;

            if (resultCode == 0x02)
            {
                SetLedTestResultDisplay("PASS", Color.SeaGreen);
            }
            else if (resultCode == 0x03)
            {
                SetLedTestResultDisplay("ERROR", Color.Firebrick);
            }
        }

        private void ApplyButtonTestResultCode(byte resultCode)
        {
            _lastButtonTestResultCode = resultCode;

            if (resultCode == 0x04)
            {
                SetButtonTestResultDisplay("PASS", Color.SeaGreen);
            }
            else if (resultCode == 0x05)
            {
                SetButtonTestResultDisplay("ERROR", Color.Firebrick);
            }
        }

        private void SetButtonTestResultDisplay(string text, Color backColor)
        {
            SetResultIndicatorDisplay(pnlButtonTestResult, text, backColor);
        }

        private void SetRssiTestResultDisplay(string text, Color backColor)
        {
            SetResultIndicatorDisplay(pnlRssiTestResult, text, backColor);
        }

        private void SetReadValueTestResultDisplay(string text, Color backColor)
        {
            SetResultIndicatorDisplay(pnlReadValueTestResult, text, backColor);
        }

        private void SetWdiTestResultDisplay(string text, Color backColor)
        {
            SetResultIndicatorDisplay(pnlWdiTestResult, text, backColor);
        }

        private void SetTestResultsErrorFrom(MainTestStep step)
        {
            var device = GetSelectedDeviceType();

            if (step <= MainTestStep.Led && IsStepRequiredForDevice(device, MainTestStep.Led))
            {
                SetLedTestResultDisplay("ERROR", Color.Firebrick);
            }

            if (step <= MainTestStep.Button && IsStepRequiredForDevice(device, MainTestStep.Button))
            {
                SetButtonTestResultDisplay("ERROR", Color.Firebrick);
            }

            if (step <= MainTestStep.Rssi && IsStepRequiredForDevice(device, MainTestStep.Rssi))
            {
                SetRssiTestResultDisplay("ERROR", Color.Firebrick);
            }

            if (step <= MainTestStep.ReadValue && IsStepRequiredForDevice(device, MainTestStep.ReadValue))
            {
                SetReadValueTestResultDisplay("ERROR", Color.Firebrick);
            }

            if (step <= MainTestStep.Wdi && IsStepRequiredForDevice(device, MainTestStep.Wdi))
            {
                SetWdiTestResultDisplay("ERROR", Color.Firebrick);
            }
        }

        private static bool IsStepRequiredForDevice(DeviceType device, MainTestStep step)
        {
            if (step == MainTestStep.Wdi)
            {
                return false;
            }

            if (device == DeviceType.PushButton)
            {
                return step == MainTestStep.Led || step == MainTestStep.Button;
            }

            if (device == DeviceType.HornStrobe)
            {
                return step == MainTestStep.Led || step == MainTestStep.Button || step == MainTestStep.Rssi || step == MainTestStep.Wdi;
            }

            return true;
        }

        private void SetResultIndicatorDisplay(Panel panel, string text, Color backColor)
        {
            if (!IsHandleCreated)
            {
                return;
            }

            if (InvokeRequired)
            {
                BeginInvoke((Action)(() => SetResultIndicatorDisplay(panel, text, backColor)));
                return;
            }

            panel.Tag = text;
            panel.BackColor = backColor;
            panel.Invalidate();
            UpdateResultSpinnerState();
            UpdateOverallResult(panel, text);
        }

        private void UpdateOverallResult(Panel panel, string text)
        {
            if (_suppressOverallResultUpdate)
            {
                return;
            }

            bool? passed = null;
            if (string.Equals(text, "PASS", StringComparison.OrdinalIgnoreCase))
            {
                passed = true;
            }
            else if (string.Equals(text, "ERROR", StringComparison.OrdinalIgnoreCase))
            {
                passed = false;
            }
            else if (string.Equals(text, "WAIT", StringComparison.OrdinalIgnoreCase))
            {
                passed = null;
            }

            var state = GetCurrentDeviceState();

            if (panel == pnlLedTestResult)
            {
                state.LedTestPassed = passed;
            }
            else if (panel == pnlButtonTestResult)
            {
                state.ButtonTestPassed = passed;
            }
            else if (panel == pnlRssiTestResult)
            {
                state.RssiTestPassed = passed;
            }
            else if (panel == pnlReadValueTestResult)
            {
                state.ReadValueTestPassed = passed;
            }
            else if (panel == pnlWdiTestResult)
            {
                state.WdiTestPassed = passed;
            }

            var device = GetSelectedDeviceType();
            var led = IsStepRequiredForDevice(device, MainTestStep.Led) ? state.LedTestPassed : true;
            var button = IsStepRequiredForDevice(device, MainTestStep.Button) ? state.ButtonTestPassed : true;
            var rssi = IsStepRequiredForDevice(device, MainTestStep.Rssi) ? state.RssiTestPassed : true;
            var readValue = IsStepRequiredForDevice(device, MainTestStep.ReadValue) ? state.ReadValueTestPassed : true;
            var wdi = IsStepRequiredForDevice(device, MainTestStep.Wdi) ? state.WdiTestPassed : true;

            if (led == null || button == null || rssi == null || readValue == null || wdi == null)
            {
                SetStartResultDisplay("RUNNING", Color.SteelBlue);
                return;
            }

            if (led == true && button == true && rssi == true && readValue == true && wdi == true)
            {
                SetStartResultDisplay(" PASS", Color.SeaGreen);
                WriteTestResult(true, string.Empty);
                return;
            }

            SetStartResultDisplay(" FAIL", Color.Firebrick);
        }

        private void MakeResultPanelCircle(Panel panel)
        {
            if (panel.Width <= 0 || panel.Height <= 0)
            {
                return;
            }

            var path = new GraphicsPath();
            path.AddEllipse(0, 0, panel.Width, panel.Height);

            var oldRegion = panel.Region;
            panel.Region = new Region(path);
            oldRegion?.Dispose();
            path.Dispose();
            panel.Invalidate();
        }

        private void ResultSpinnerTimer_Tick(object sender, EventArgs e)
        {
            _spinnerAngle = (_spinnerAngle + 20) % 360;
            InvalidateWaitingResultPanels();
        }

        private void UpdateResultSpinnerState()
        {
            if (IsWaiting(pnlLedTestResult) || IsWaiting(pnlButtonTestResult) || IsWaiting(pnlRssiTestResult) || IsWaiting(pnlReadValueTestResult) || IsWaiting(pnlWdiTestResult))
            {
                if (!_resultSpinnerTimer.Enabled)
                {
                    _resultSpinnerTimer.Start();
                }
            }
            else
            {
                _resultSpinnerTimer.Stop();
            }
        }

        private void InvalidateWaitingResultPanels()
        {
            if (IsWaiting(pnlLedTestResult)) pnlLedTestResult.Invalidate();
            if (IsWaiting(pnlButtonTestResult)) pnlButtonTestResult.Invalidate();
            if (IsWaiting(pnlRssiTestResult)) pnlRssiTestResult.Invalidate();
            if (IsWaiting(pnlReadValueTestResult)) pnlReadValueTestResult.Invalidate();
            if (IsWaiting(pnlWdiTestResult)) pnlWdiTestResult.Invalidate();
        }

        private static bool IsWaiting(Panel panel)
        {
            return string.Equals(panel.Tag as string, "WAIT", StringComparison.OrdinalIgnoreCase);
        }

        private void ResultIndicatorPanel_Paint(object sender, PaintEventArgs e)
        {
            var panel = sender as Panel;
            if (panel == null)
            {
                return;
            }

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(2, 2, panel.Width - 5, panel.Height - 5);
            if (rect.Width <= 0 || rect.Height <= 0)
            {
                return;
            }

            var innerRect = Rectangle.Inflate(rect, -4, -4);
            if (innerRect.Width < 2 || innerRect.Height < 2)
            {
                innerRect = Rectangle.Inflate(rect, -2, -2);
            }

            var status = panel.Tag as string;
            if (string.Equals(status, "WAIT", StringComparison.OrdinalIgnoreCase))
            {
                using (var basePen = new Pen(Color.FromArgb(90, 120, 120, 120), 3f))
                using (var spinnerPen = new Pen(Color.DeepSkyBlue, 3f))
                using (var coreBrush = new SolidBrush(Color.White))
                {
                    e.Graphics.DrawEllipse(basePen, rect);
                    e.Graphics.DrawArc(spinnerPen, rect, _spinnerAngle, 80);
                    e.Graphics.FillEllipse(coreBrush, innerRect);
                }
                return;
            }

            using (var ringPen = new Pen(panel.BackColor, 3f))
            using (var coreBrush = new SolidBrush(Color.White))
            using (var brush = new SolidBrush(panel.BackColor))
            {
                e.Graphics.FillEllipse(brush, rect);
                e.Graphics.DrawEllipse(ringPen, rect);
                e.Graphics.FillEllipse(coreBrush, innerRect);
            }
        }

        private async Task RunButtonTestAsync()
        {
            var isPushButton = GetSelectedDeviceType() == DeviceType.PushButton;
            if (GetSelectedDeviceType() == DeviceType.HornStrobe)
            {
                await RunHornStrobeButtonAndHornTestsAsync();
                return;
            }

            var buttonCommand = isPushButton ? (byte)0x07 : (byte)0x02;

            Log(isPushButton ? "Starting Button Emergency Test..." : "Starting Button Test...");
            SetButtonTestResultDisplay("WAIT", Color.DimGray);

            var response = await SendFrameAndWaitResponseAsync(buttonCommand, 0x00);

            if (response.HasValue)
            {
                if (response.Value == 0x06)
                {
                    if (isPushButton)
                    {
                        Log("Button Emergency ACK received. Monitor ROI3 red LED blink for 3s...");
                        StartBlinkCounting();
                        await Task.Delay(TimeSpan.FromSeconds(3));
                        var roi3BlinkDetected = StopBlinkCountingRoi3();
                        if (!roi3BlinkDetected)
                        {
                            Log("Button Emergency FAILED (ROI3 red LED blink not detected).");
                            ApplyButtonTestResultCode(0x05);
                            SetTestResultsErrorFrom(MainTestStep.Button);
                            FailAndStopAllTests("Button Emergency failed: ROI3 red LED blink not detected.");
                            return;
                        }

                        Log("Button Emergency PASSED (ACK + ROI3 red LED blink).");
                        ApplyButtonTestResultCode(0x04);
                        return;
                    }

                    Log("Button Test PASSED.");
                    ApplyButtonTestResultCode(0x04);

                    var selectedDevice = GetSelectedDeviceType();
                    if (selectedDevice == DeviceType.SmokeDetector || selectedDevice == DeviceType.HeatDetector)
                    {
                        var cmd08Source = selectedDevice == DeviceType.SmokeDetector
                            ? "after Smoke Button Test pass"
                            : "after Heat Button Test pass";
                        var cmd08Ok = await SendExtendedCommand0805Async(cmd08Source, 0x01);
                        if (!cmd08Ok)
                        {
                            SetTestResultsErrorFrom(MainTestStep.Rssi);
                            FailAndStopAllTests("ACK cmd=0x08 failed after Button Test.");
                            return;
                        }
                    }

                    var rssiReady = await SendRssiSetCommandAsync();
                    if (!rssiReady)
                    {
                        return;
                    }

                    var detectorReady = await ConnectDetectorForRssiAsync();
                    if (!detectorReady)
                    {
                        return;
                    }

                    await RunDetectorRssiTestAsync();
                }
                else
                {
                    Log($"Button Test FAILED with response code: 0x{response.Value:X2}");
                    ApplyButtonTestResultCode(0x05);
                    SetTestResultsErrorFrom(MainTestStep.Button);
                    FailAndStopAllTests("Button Test failed.");
                }
            }
            else
            {
                Log("Button Test FAILED (timeout).");
                ApplyButtonTestResultCode(0x05);
                SetTestResultsErrorFrom(MainTestStep.Button);
                FailAndStopAllTests("Button Test timeout.");
            }
        }

        private async Task RunHornStrobeTestFlowAsync()
        {
            SetStartResultDisplay("WAIT", Color.DimGray);
            SetLedTestResultDisplay("WAIT", Color.DimGray);
            SetButtonTestResultDisplay("WAIT", Color.DimGray);
            SetRssiTestResultDisplay("WAIT", Color.DimGray);
            SetReadValueTestResultDisplay("WAIT", Color.DimGray);
            SetWdiTestResultDisplay("WAIT", Color.DimGray);
            ResetCurrentDeviceState();

            Log("Starting Horn Strobe flow...");
            var powerAck = await ExecuteStartFramesAsync(0x01);
            if (!powerAck)
            {
                SetTestResultsErrorFrom(MainTestStep.Led);
                FailAndStopAllTests("Horn Strobe LED Test failed (power_on ACK failed).");
                return;
            }

            ApplyLedTestResultCode(0x02);
            SetStartResultDisplay("RUNNING", Color.SteelBlue);
            await RunHornStrobeButtonAndHornTestsAsync();
        }

        private async Task RunHornStrobeButtonAndHornTestsAsync()
        {
            Log("Starting Test nút nhấn 2...");
            SetButtonTestResultDisplay("WAIT", Color.DimGray);
            var button2Response = await SendFrameAndWaitResponseAsync(0x05, 0x00);
            if (!button2Response.HasValue || button2Response.Value != 0x06)
            {
                ApplyButtonTestResultCode(0x05);
                var reason = button2Response.HasValue ? $"Test nút nhấn 2 failed (0x{button2Response.Value:X2})." : "Test nút nhấn 2 timeout.";
                SetTestResultsErrorFrom(MainTestStep.Button);
                FailAndStopAllTests(reason);
                return;
            }

            ApplyButtonTestResultCode(0x04);

            Log("Starting Test nút nhấn...");
            SetRssiTestResultDisplay("WAIT", Color.DimGray);
            var buttonResponse = await SendFrameAndWaitResponseAsync(0x06, 0x00);
            if (!buttonResponse.HasValue || buttonResponse.Value != 0x06)
            {
                SetRssiTestResultDisplay("ERROR", Color.Firebrick);
                var reason = buttonResponse.HasValue ? $"Test nút nhấn failed (0x{buttonResponse.Value:X2})." : "Test nút nhấn timeout.";
                SetTestResultsErrorFrom(MainTestStep.Rssi);
                FailAndStopAllTests(reason);
                return;
            }

            SetRssiTestResultDisplay("PASS", Color.SeaGreen);

            var hornButtonCmd08Ok = await SendExtendedCommand0805Async("after HornStrobe Button Test pass", 0x01);
            if (!hornButtonCmd08Ok)
            {
                SetTestResultsErrorFrom(MainTestStep.Wdi);
                FailAndStopAllTests("ACK cmd=0x08 failed after HornStrobe Button Test.");
                return;
            }

            Log("Skip WDI test (disabled for all devices).");
        }

        private async Task<bool> ConnectDetectorForHornStrobeAsync()
        {
            if (_detectorSerialService.IsConnected)
            {
                Log($"DT ({_detectorSerialService.PortName}) ready for Horn Strobe test.");
                return true;
            }

            if (cmbDetectorComPort.SelectedItem == null)
            {
                Log("Please select detector COM port for Horn Strobe test.");
                SetWdiTestResultDisplay("ERROR", Color.Firebrick);
                FailAndStopAllTests("DT COM not selected for Horn Strobe test.");
                return false;
            }

            var port = cmbDetectorComPort.SelectedItem.ToString();
            var connected = await Task.Run(() => _detectorSerialService.Connect(port, DetectorHornStrobeBaudRate));
            if (connected)
            {
                Log($"DT ({port}) connected for Horn Strobe test at {DetectorHornStrobeBaudRate} baud.");
                return true;
            }

            Log($"DT ({port}) connect failed for Horn Strobe test at {DetectorHornStrobeBaudRate} baud.");
            SetWdiTestResultDisplay("ERROR", Color.Firebrick);
            FailAndStopAllTests("DT COM connect failed for Horn Strobe test.");
            return false;
        }

        private int GetDetectorBaudRateForSelectedDevice()
        {
            return GetSelectedDeviceType() == DeviceType.HornStrobe ? DetectorHornStrobeBaudRate : DetectorDefaultBaudRate;
        }

        private static string GetHornStrobeErrorMessage(byte code)
        {
            switch (code)
            {
                case 0x01: return "LoRa fail (0x01)";
                case 0x02: return "Battery fail (0x02)";
                case 0x03: return "LoRa + Battery fail (0x03)";
                case 0x04: return "5V fail (0x04)";
                case 0x05: return "LoRa + 5V fail (0x05)";
                case 0x06: return "Battery + 5V fail (0x06)";
                case 0x07: return "All fail (0x07)";
                default: return $"Unknown error code 0x{code:X2}";
            }
        }

        private async Task<bool> ExecuteStartFramesAsync(byte powerData)
        {
            var response = await SendFrameAndWaitResponseAsync(0x01, powerData);
            return response == 0x06;
        }

        private async Task<bool> SendRssiSetCommandAsync()
        {
            Log("PC Send: 1F 2F 3F FF 00 03 01 <BCC>");
            Log("Wait ACK 3s: 1F 2F 3F FF 01 03 06 <BCC>");
            var response = await SendFrameAndWaitResponseAsync(0x03, 0x01);
            if (response.HasValue && response.Value == 0x06)
            {
                Log("RSSI set ACK received.");
                return true;
            }

            var responseText = response.HasValue ? $"0x{response.Value:X2}" : "timeout";
            Log($"FAIL ({responseText}).");
            SetTestResultsErrorFrom(MainTestStep.Rssi);
            FailAndStopAllTests("RSSI set failed.");
            return false;
        }

        private void FailAndStopAllTests(string reason)
        {
            Log(reason);
            SetStartResultDisplay(" FAIL", Color.Firebrick);
            WriteTestResult(false, reason);
        }

        private async Task<bool> ConnectDetectorForRssiAsync()
        {
            if (_detectorSerialService.IsConnected)
            {
                Log($"DT ({_detectorSerialService.PortName}) ready for Lora.");
                return true;
            }

            if (cmbDetectorComPort.SelectedItem == null)
            {
                Log("Please select detector COM port for Lora.");
                FailRssiAndReadValueTests("DT COM not selected for Lora.");
                return false;
            }

            var port = cmbDetectorComPort.SelectedItem.ToString();
            var connected = await Task.Run(() => _detectorSerialService.Connect(port, DetectorDefaultBaudRate));
            if (connected)
            {
                Log($"DT ({port}) connected for Lora at {DetectorDefaultBaudRate} baud.");
                return true;
            }

            Log($"DT ({port}) connect failed for Lora at {DetectorDefaultBaudRate} baud.");
            FailRssiAndReadValueTests("DT COM connect failed for Lora.");
            return false;
        }

        private async Task RunDetectorRssiTestAsync()
        {
            SetRssiTestResultDisplay("WAIT", Color.DimGray);

            const int maxRetry = 5;
            const int timeoutMs = 2000;

            for (var attempt = 1; attempt <= maxRetry; attempt++)
            {
                var packet = BuildDetectorRssiCommand();
                var tcs = new TaskCompletionSource<bool?>(TaskCreationOptions.RunContinuationsAsynchronously);
                _pendingDetectorRssiTcs = tcs;

                Log($"PC send: <SOH>R1<STX>1.0.H()<ETX><BCC> (attempt {attempt}/{maxRetry})");
                LogFrame("DT TX", _detectorSerialService.PortName, packet);
                _detectorSerialService.SendBytes(packet);
                Log($"DT ({_detectorSerialService.PortName}) wait ACK {timeoutMs / 1000}s");

                var completed = await Task.WhenAny(tcs.Task, Task.Delay(timeoutMs));
                if (completed != tcs.Task)
                {
                    _pendingDetectorRssiTcs = null;
                    Log($"Lora timeout attempt {attempt}/{maxRetry}.");

                    if (attempt == maxRetry)
                    {
                        FailRssiAndReadValueTests("Lora Test timeout.");
                        return;
                    }

                    continue;
                }

                var result = await tcs.Task;
                if (result == true)
                {
                    SetRssiTestResultDisplay("PASS", Color.SeaGreen);
                    Log("Lora Test PASSED.");

                    await RunReadValueTestAsync();
                    return;
                }

                SetRssiTestResultDisplay("ERROR", Color.Firebrick);
                SetTestResultsErrorFrom(MainTestStep.ReadValue);
                FailAndStopAllTests("Lora Test failed.");
                return;
            }
        }

        private async Task<bool> RunReadValueTestAsync()
        {
            SetReadValueTestResultDisplay("WAIT", Color.DimGray);

            const int maxRetry = 5;
            const int timeoutMs = 1000;

            string readValuePattern;
            byte[] packet;
            if (rdoSmokeDetector.Checked)
            {
                readValuePattern = "1.0.5(";
                packet = BuildDetectorReadValueCommand((byte)'5');
            }
            else if (rdoHeatDetector.Checked)
            {
                readValuePattern = "1.0.3(";
                packet = BuildDetectorReadValueCommand((byte)'3');
            }
            else
            {
                SetReadValueTestResultDisplay("PASS", Color.SeaGreen);
                return true;
            }

            for (var attempt = 1; attempt <= maxRetry; attempt++)
            {
                var tcs = new TaskCompletionSource<bool?>(TaskCreationOptions.RunContinuationsAsynchronously);
                _pendingDetectorReadValueTcs = tcs;
                _pendingReadValuePattern = readValuePattern;

                Log($"PC send: <SOH>R1<STX>{readValuePattern.TrimEnd('(')}()<ETX><BCC> (attempt {attempt}/{maxRetry})");
                LogFrame("DT TX", _detectorSerialService.PortName, packet);
                _detectorSerialService.SendBytes(packet);
                Log($"DT ({_detectorSerialService.PortName}) wait Read Value ACK {timeoutMs / 1000}s");

                var completed = await Task.WhenAny(tcs.Task, Task.Delay(timeoutMs));
                if (completed != tcs.Task)
                {
                    _pendingDetectorReadValueTcs = null;
                    _pendingReadValuePattern = null;
                    Log($"Read Value timeout attempt {attempt}/{maxRetry}.");

                    if (attempt == maxRetry)
                    {
                        SetReadValueTestResultDisplay("ERROR", Color.Firebrick);
                        SetWdiTestResultDisplay("ERROR", Color.Firebrick);
                        FailAndStopAllTests("Read Value Test timeout.");
                        return false;
                    }

                    continue;
                }

                var result = await tcs.Task;
                _pendingDetectorReadValueTcs = null;
                _pendingReadValuePattern = null;
                if (result == true)
                {
                    SetReadValueTestResultDisplay("PASS", Color.SeaGreen);
                    Log("Read Value Test PASSED.");
                    return true;
                }

                SetReadValueTestResultDisplay("ERROR", Color.Firebrick);
                SetWdiTestResultDisplay("ERROR", Color.Firebrick);
                FailAndStopAllTests("Read Value Test failed.");
                return false;
            }

            return false;
        }

        private async Task RunWdiTestAsync()
        {
            SetWdiTestResultDisplay("WAIT", Color.DimGray);
            Log("Starting WDI Test...");

            // Additional requested frame before WDI command.
            // PC Send: 1F 2F 3F FF 00 03 00 EC
            // Receive: 1F 2F 3F FF 01 03 {0x06: success, 0x15: failed} <BCC> (e.g. ... 06 EB)
            var preWdiOk = await SendWdiPreFrame03Async();
            if (!preWdiOk)
            {
                return;
            }

            Log("PC Send: 1F 2F 3F FF 00 04 01 <BCC>");
            Log("Wait ACK 3s: 1F 2F 3F FF 01 04 06 <BCC>");

            var response = await SendFrameAndWaitResponseAsync(0x04, 0x01);
            if (!response.HasValue || response.Value != 0x06)
            {
                SetWdiTestResultDisplay("ERROR", Color.Firebrick);
                var responseText = response.HasValue ? $"0x{response.Value:X2}" : "timeout";
                FailAndStopAllTests($"WDI ACK failed ({responseText}).");
                return;
            }

            Log("WDI ACK received. Monitor LED blink for 3s...");
            StartBlinkCounting();
            await Task.Delay(TimeSpan.FromSeconds(3));
            var blinkDetected = StopBlinkCounting();
            if (!blinkDetected)
            {
                SetWdiTestResultDisplay("ERROR", Color.Firebrick);
                FailAndStopAllTests("WDI LED blink timeout (3s).");
                return;
            }

            // After detecting LED blink during WDI, send follow-up WDI frame: cmd=0x04, data=0x00
            var followUp = BuildFrame(0x04, 0x00);
            Log($"PC Send: {ToHex(followUp)}");
            LogFrame("G6T TX", _serialService.PortName, followUp);
            _serialService.SendBytes(followUp);

            SetWdiTestResultDisplay("PASS", Color.SeaGreen);
            Log("WDI Test PASSED (ACK + LED blink detected).");
        }

        private async Task<bool> SendWdiPreFrame03Async()
        {
            const byte cmd = 0x03;
            const byte data = 0x00;

            var tx = BuildFrame(cmd, data);
            Log($"PC Send (before WDI): {ToHex(tx)}");
            Log("Wait ACK 3s: 1F 2F 3F FF 01 03 {0x06: success, 0x15: failed} <BCC>");

            var response = await SendFrameAndWaitResponseAsync(cmd, data);
            if (response.HasValue)
            {
                if (response.Value == 0x06)
                {
                    Log("ACK cmd=0x03 received (before WDI): 0x06 (success)");
                    return true;
                }

                Log($"ACK cmd=0x03 received (before WDI): 0x{response.Value:X2} (failed)");
                SetWdiTestResultDisplay("ERROR", Color.Firebrick);
                FailAndStopAllTests($"ACK cmd=0x03 failed (before WDI) (0x{response.Value:X2}).");
                return false;
            }

            Log("ACK cmd=0x03 timeout (before WDI) (3s). (Expected example: 1F 2F 3F FF 01 03 06 EB)");
            SetWdiTestResultDisplay("ERROR", Color.Firebrick);
            FailAndStopAllTests("ACK cmd=0x03 timeout (before WDI) (3s). ");
            return false;
        }

        private async Task<bool> SendStartExtraFrame03Async()
        {
            const byte cmd = 0x03;
            const byte data = 0x00;

            var tx = BuildFrame(cmd, data);
            Log($"PC Send (after Start): {ToHex(tx)}");
            Log("Wait ACK 3s: 1F 2F 3F FF 01 03 {0x06: success, 0x15: failed} <BCC>");

            var response = await SendFrameAndWaitResponseAsync(cmd, data);
            if (response.HasValue)
            {
                if (response.Value == 0x06)
                {
                    Log("ACK cmd=0x03 received: 0x06 (success)");
                    return true;
                }

                Log($"ACK cmd=0x03 received: 0x{response.Value:X2} (failed)");
                SetTestResultsErrorFrom(MainTestStep.Rssi);
                FailAndStopAllTests($"ACK cmd=0x03 failed (0x{response.Value:X2}).");
                return false;
            }

            Log("ACK cmd=0x03 timeout (3s). (Expected example: 1F 2F 3F FF 01 03 06 EB)");
            SetTestResultsErrorFrom(MainTestStep.Rssi);
            FailAndStopAllTests("ACK cmd=0x03 timeout (3s). ");
            return false;
        }

        private async Task<bool> SendStartExtraFrame04Async()
        {
            const byte cmd = 0x04;
            const byte data = 0x00;

            var tx = BuildFrame(cmd, data);
            Log($"PC Send (after Start): {ToHex(tx)}");
            Log("Wait ACK 3s: 1F 2F 3F FF 01 04 {0x06: success, 0x15: failed} <BCC>");

            var response = await SendFrameAndWaitResponseAsync(cmd, data);
            if (response.HasValue)
            {
                if (response.Value == 0x06)
                {
                    Log("ACK cmd=0x04 received: 0x06 (success)");
                    return true;
                }

                Log($"ACK cmd=0x04 received: 0x{response.Value:X2} (failed)");
                SetTestResultsErrorFrom(MainTestStep.Wdi);
                FailAndStopAllTests($"ACK cmd=0x04 failed (0x{response.Value:X2}).");
                return false;
            }

            Log("ACK cmd=0x04 timeout (3s). (Expected example: 1F 2F 3F FF 01 04 06 EC)");
            SetTestResultsErrorFrom(MainTestStep.Wdi);
            FailAndStopAllTests("ACK cmd=0x04 timeout (3s). ");
            return false;
        }

        private static bool? ParseDetectorRssiAck(string detectorData)
        {
            if (string.IsNullOrEmpty(detectorData))
            {
                return null;
            }

            if (detectorData.IndexOf("On Production Mode", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            if (detectorData.IndexOf("Off Production Mode", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return false;
            }

            var hBinaryPass = "1.0.H(" + (char)0x01 + ")";
            var hBinaryFail = "1.0.H(" + (char)0x00 + ")";

            if (detectorData.IndexOf("1.0.5(1)", StringComparison.OrdinalIgnoreCase) >= 0 ||
                detectorData.IndexOf("2.0.0(1)", StringComparison.OrdinalIgnoreCase) >= 0 ||
                detectorData.IndexOf("1.0.H(1)", StringComparison.OrdinalIgnoreCase) >= 0 ||
                detectorData.IndexOf(hBinaryPass, StringComparison.Ordinal) >= 0)
            {
                return true;
            }

            if (detectorData.IndexOf("1.0.5(0)", StringComparison.OrdinalIgnoreCase) >= 0 ||
                detectorData.IndexOf("2.0.0(0)", StringComparison.OrdinalIgnoreCase) >= 0 ||
                detectorData.IndexOf("1.0.H(0)", StringComparison.OrdinalIgnoreCase) >= 0 ||
                detectorData.IndexOf(hBinaryFail, StringComparison.Ordinal) >= 0)
            {
                return false;
            }

            return null;
        }

        private bool? ParseDetectorReadValueAck(string detectorData)
        {
            if (string.IsNullOrWhiteSpace(detectorData))
            {
                return null;
            }

            var smokeStart = detectorData.IndexOf("1.0.5(", StringComparison.OrdinalIgnoreCase);
            var smokeMatched = false;
            if (smokeStart >= 0)
            {
                var smokeEnd = detectorData.IndexOf(')', smokeStart);
                if (smokeEnd > smokeStart)
                {
                    smokeMatched = true;
                }
            }

            var heatStart = detectorData.IndexOf("1.0.3(", StringComparison.OrdinalIgnoreCase);
            var heatMatched = false;
            if (heatStart >= 0)
            {
                var heatEnd = detectorData.IndexOf(')', heatStart);
                if (heatEnd > heatStart)
                {
                    heatMatched = true;
                }
            }

            if (!smokeMatched && !heatMatched)
            {
                return null;
            }

            if (string.Equals(_pendingReadValuePattern, "1.0.5(", StringComparison.OrdinalIgnoreCase))
            {
                return smokeMatched ? true : false;
            }

            if (string.Equals(_pendingReadValuePattern, "1.0.3(", StringComparison.OrdinalIgnoreCase))
            {
                return heatMatched ? true : false;
            }

            return smokeMatched || heatMatched;
        }

        private static byte[] BuildDetectorRssiCommand()
        {
            var packet = new byte[]
            {
                0x01,
                (byte)'R',
                (byte)'1',
                0x02,
                (byte)'1', (byte)'.', (byte)'0', (byte)'.', (byte)'H', (byte)'(', (byte)')',
                0x03,
                0x00
            };

            packet[packet.Length - 1] = ComputeDetectorBcc(packet, packet.Length - 1);
            return packet;
        }

        private static byte[] BuildDetectorReadValueCommand(byte typeValue)
        {
            var packet = new byte[]
            {
                0x01,
                (byte)'R',
                (byte)'1',
                0x02,
                (byte)'1', (byte)'.', (byte)'0', (byte)'.', typeValue, (byte)'(', (byte)')',
                0x03,
                0x00
            };

            packet[packet.Length - 1] = ComputeDetectorBcc(packet, packet.Length - 1);
            return packet;
        }

        private void FailRssiAndReadValueTests(string reason)
        {
            Log(reason);
            SetStartResultDisplay(" FAIL", Color.Firebrick);
            SetRssiTestResultDisplay("ERROR", Color.Firebrick);
            SetReadValueTestResultDisplay("ERROR", Color.Firebrick);
            SetWdiTestResultDisplay("ERROR", Color.Firebrick);
            WriteTestResult(false, reason);
        }

        private async Task<byte?> SendFrameAndWaitResponseAsync(byte command, byte data)
        {
            if (!_serialService.IsConnected)
            {
                return null;
            }

            var frame = BuildFrame(command, data);
            var effectiveTimeout = 3000;

            for (var attempt = 1; attempt <= 2; attempt++)
            {
                var tcs = new TaskCompletionSource<byte?>(TaskCreationOptions.RunContinuationsAsynchronously);

                lock (_frameLock)
                {
                    _pendingFrameCmd = command;
                    _pendingResponseTcs = tcs;
                }

                _serialService.SendBytes(frame);
                LogFrame("G6T TX", _serialService.PortName, frame);
                Log($"G6T ({_serialService.PortName}) wait cmd=0x{command:X2} timeout={effectiveTimeout}ms");

                var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(effectiveTimeout));
                if (completedTask == tcs.Task)
                {
                    return await tcs.Task;
                }

                Log($"G6T ({_serialService.PortName}) timeout cmd=0x{command:X2} after {effectiveTimeout}ms");

                lock (_frameLock)
                {
                    if (ReferenceEquals(_pendingResponseTcs, tcs))
                    {
                        _pendingResponseTcs = null;
                    }
                }

                if (attempt == 1)
                {
                    Log($"G6T ({_serialService.PortName}) retry cmd=0x{command:X2}");
                }
            }

            return null;
        }

        private async Task<byte?> SendDetectorFrameAndWaitResponseAsync(byte command, byte data)
        {
            if (!_detectorSerialService.IsConnected)
            {
                return null;
            }

            var frame = BuildFrame(command, data);
            const int effectiveTimeout = 3000;

            for (var attempt = 1; attempt <= 2; attempt++)
            {
                var tcs = new TaskCompletionSource<byte?>(TaskCreationOptions.RunContinuationsAsynchronously);

                lock (_frameLock)
                {
                    _pendingDetectorFrameCmd = command;
                    _pendingDetectorResponseTcs = tcs;
                }

                _detectorSerialService.SendBytes(frame);
                LogFrame("DT TX", _detectorSerialService.PortName, frame);
                Log($"DT ({_detectorSerialService.PortName}) wait cmd=0x{command:X2} timeout={effectiveTimeout}ms");

                var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(effectiveTimeout));
                if (completedTask == tcs.Task)
                {
                    return await tcs.Task;
                }

                Log($"DT ({_detectorSerialService.PortName}) timeout cmd=0x{command:X2} after {effectiveTimeout}ms");

                lock (_frameLock)
                {
                    if (ReferenceEquals(_pendingDetectorResponseTcs, tcs))
                    {
                        _pendingDetectorResponseTcs = null;
                    }
                }

                if (attempt == 1)
                {
                    Log($"DT ({_detectorSerialService.PortName}) retry cmd=0x{command:X2}");
                }
            }

            return null;
        }

        private static byte[] BuildFrame(byte command, byte data)
        {
            var frame = new byte[8];
            frame[0] = 0x1F;
            frame[1] = 0x2F;
            frame[2] = 0x3F;
            frame[3] = 0xFF;
            frame[4] = 0x00;
            frame[5] = command;
            frame[6] = data;
            frame[7] = ComputeBcc(frame, 7);
            return frame;
        }

        private static byte[] BuildExtendedFrame0805(byte data)
        {
            var frame = new byte[9];
            frame[0] = 0x1F;
            frame[1] = 0x2F;
            frame[2] = 0x3F;
            frame[3] = 0xFF;
            frame[4] = 0x00;
            frame[5] = 0x08;
            frame[6] = 0x05;
            frame[7] = data;
            frame[8] = ComputeBcc(frame, 8);
            return frame;
        }

        private static byte ComputeBcc(byte[] buffer, int length)
        {
            byte bcc = 0;
            for (var i = 1; i < length; i++)
            {
                bcc ^= buffer[i];
            }

            return bcc;
        }

        private void ParseG6tFrames(byte[] data, string portName)
        {
            var frames = new List<byte[]>();

            lock (_frameLock)
            {
                _g6tRxBuffer.AddRange(data);

                while (_g6tRxBuffer.Count >= 8)
                {
                    var start = FindFrameStart(_g6tRxBuffer);
                    if (start < 0)
                    {
                        var preserveCount = Math.Min(3, _g6tRxBuffer.Count);
                        if (preserveCount > 0)
                        {
                            var tail = _g6tRxBuffer.Skip(_g6tRxBuffer.Count - preserveCount).ToArray();
                            _g6tRxBuffer.Clear();
                            _g6tRxBuffer.AddRange(tail);
                        }
                        else
                        {
                            _g6tRxBuffer.Clear();
                        }
                        break;
                    }

                    if (start > 0)
                    {
                        _g6tRxBuffer.RemoveRange(0, start);
                    }

                    if (_g6tRxBuffer.Count < 8)
                    {
                        break;
                    }

                    var frame = _g6tRxBuffer.Take(8).ToArray();
                    var bcc = ComputeBcc(frame, 7);
                    var legacyBcc = (byte)0;
                    for (var i = 0; i < 7; i++)
                    {
                        legacyBcc ^= frame[i];
                    }

                    if (bcc != frame[7] && legacyBcc != frame[7])
                    {
                        _g6tRxBuffer.RemoveAt(0);
                        continue;
                    }

                    _g6tRxBuffer.RemoveRange(0, 8);
                    frames.Add(frame);
                }
            }

            foreach (var frame in frames)
            {
                HandleG6tFrame(frame, portName);
            }
        }

        private void ParseDetectorG6tFrames(byte[] data, string portName)
        {
            var frames = new List<byte[]>();

            lock (_frameLock)
            {
                _detectorG6tRxBuffer.AddRange(data);

                while (_detectorG6tRxBuffer.Count >= 8)
                {
                    var start = FindFrameStart(_detectorG6tRxBuffer);
                    if (start < 0)
                    {
                        var preserveCount = Math.Min(3, _detectorG6tRxBuffer.Count);
                        if (preserveCount > 0)
                        {
                            var tail = _detectorG6tRxBuffer.Skip(_detectorG6tRxBuffer.Count - preserveCount).ToArray();
                            _detectorG6tRxBuffer.Clear();
                            _detectorG6tRxBuffer.AddRange(tail);
                        }
                        else
                        {
                            _detectorG6tRxBuffer.Clear();
                        }
                        break;
                    }

                    if (start > 0)
                    {
                        _detectorG6tRxBuffer.RemoveRange(0, start);
                    }

                    if (_detectorG6tRxBuffer.Count < 8)
                    {
                        break;
                    }

                    var frame = _detectorG6tRxBuffer.Take(8).ToArray();
                    var bcc = ComputeBcc(frame, 7);
                    var legacyBcc = (byte)0;
                    for (var i = 0; i < 7; i++)
                    {
                        legacyBcc ^= frame[i];
                    }

                    if (bcc != frame[7] && legacyBcc != frame[7])
                    {
                        _detectorG6tRxBuffer.RemoveAt(0);
                        continue;
                    }

                    _detectorG6tRxBuffer.RemoveRange(0, 8);
                    frames.Add(frame);
                }
            }

            foreach (var frame in frames)
            {
                HandleDetectorG6tFrame(frame, portName);
            }
        }

        private static int FindFrameStart(List<byte> buffer)
        {
            for (var i = 0; i <= buffer.Count - 4; i++)
            {
                if (buffer[i] == 0x1F && buffer[i + 1] == 0x2F && buffer[i + 2] == 0x3F && buffer[i + 3] == 0xFF)
                {
                    return i;
                }
            }

            return -1;
        }

        private void HandleG6tFrame(byte[] frame, string portName)
        {
            LogFrame("G6T RX", portName, frame);

            if (frame.Length < 8 || frame[4] != 0x01)
            {
                return;
            }

            var cmd = frame[5];
            var responseData = frame[6];

            TaskCompletionSource<byte?> tcs = null;
            lock (_frameLock)
            {
                if (_pendingResponseTcs != null && _pendingFrameCmd == cmd)
                {
                    tcs = _pendingResponseTcs;
                    _pendingResponseTcs = null;
                }
            }

            _lastG6tFrameHex = ToHex(frame);
            if (tcs != null)
            {
                tcs.TrySetResult(responseData);
            }
        }

        private void HandleDetectorG6tFrame(byte[] frame, string portName)
        {
            LogFrame("DT RX", portName, frame);

            if (frame.Length < 8 || frame[4] != 0x01)
            {
                return;
            }

            var cmd = frame[5];
            var responseData = frame[6];

            TaskCompletionSource<byte?> tcs = null;
            lock (_frameLock)
            {
                if (_pendingDetectorResponseTcs != null && _pendingDetectorFrameCmd == cmd)
                {
                    tcs = _pendingDetectorResponseTcs;
                    _pendingDetectorResponseTcs = null;
                }
            }

            if (tcs != null)
            {
                tcs.TrySetResult(responseData);
            }
        }

        private void WriteTestResult(bool passed, string error)
        {
            if (_resultWritten)
            {
                return;
            }

            _resultWritten = true;
            TrySendFinalPowerCommand();

            var station = GetSelectedStation();
            var serial = txtSerial.Text.Trim();
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var resultDir = @"E:\Work\G6T\G6T_Quyet\FCT-G6T";
            Directory.CreateDirectory(resultDir);
            var path = Path.Combine(resultDir, "result.txt");

            using (var writer = new StreamWriter(path, append: true))
            {
                writer.WriteLine($"Serial: {serial}");
                writer.WriteLine($"Station: {station}");
                writer.WriteLine($"DateTime: {timestamp}");
                writer.WriteLine($"Result: {(passed ? "PASS" : "FAIL")}");

                if (!passed)
                {
                    writer.WriteLine($"Error: {error}");
                    var lastFrame = string.IsNullOrWhiteSpace(_lastG6tFrameHex) ? "No frame received." : _lastG6tFrameHex;
                    writer.WriteLine($"Log: {lastFrame}");
                }

                writer.WriteLine("//*******************************END TEST**********************************//");
            }
        }

        private void TrySendFinalPowerCommand()
        {
            if (_finalPowerCommandSent)
            {
                return;
            }

            _finalPowerCommandSent = true;
            _ = SendFinalPowerCommandAsync();
        }

        private async Task SendFinalPowerCommandAsync()
        {
            if (!_serialService.IsConnected)
            {
                Log("Skip final power command: G6T COM is not connected.");
                return;
            }

            Log("PC Send final: 1F 2F 3F FF 00 01 00 <BCC> (power_off)");
            var response = await SendFrameAndWaitResponseAsync(0x01, 0x00);
            if (response.HasValue && response.Value == 0x06)
            {
                Log("Final power_off ACK received: 0x06 (success).");
            }
            else if (response.HasValue)
            {
                Log($"Final power_off ACK failed: 0x{response.Value:X2}.");
            }
            else
            {
                Log("Final power_off ACK timeout.");
            }

            await SendExtendedCommand0805Async("after test completed", 0x00);
        }

        private async Task<bool> SendExtendedCommand0805Async(string source, byte data)
        {
            if (!_serialService.IsConnected)
            {
                Log($"Skip cmd=0x08 (source: {source}): G6T COM is not connected.");
                return false;
            }

            for (var attempt = 1; attempt <= 2; attempt++)
            {
                var frame = BuildExtendedFrame0805(data);
                _serialService.SendBytes(frame);
                LogFrame("G6T TX", _serialService.PortName, frame);
                Log($"G6T ({_serialService.PortName}) wait cmd=0x08 timeout=3000ms ({source}, attempt {attempt}/2)");

                var response = await WaitForResponseAsync(0x08, 3000);
                if (response == 0x06)
                {
                    Log("ACK cmd=0x08 received: 0x06 (pass)");
                    return true;
                }

                if (response == 0x15)
                {
                    Log("ACK cmd=0x08 received: 0x15 (fail)");
                    return false;
                }

                if (response.HasValue)
                {
                    Log($"ACK cmd=0x08 received: 0x{response.Value:X2}");
                    return false;
                }

                if (attempt == 1)
                {
                    Log("ACK cmd=0x08 timeout, retry...");
                }
            }

            Log("ACK cmd=0x08 timeout.");
            return false;
        }

        private async Task<byte?> WaitForResponseAsync(byte command, int timeoutMs)
        {
            var tcs = new TaskCompletionSource<byte?>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_frameLock)
            {
                _pendingFrameCmd = command;
                _pendingResponseTcs = tcs;
            }

            var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(timeoutMs));
            if (completedTask == tcs.Task)
            {
                return await tcs.Task;
            }

            lock (_frameLock)
            {
                if (ReferenceEquals(_pendingResponseTcs, tcs))
                {
                    _pendingResponseTcs = null;
                }
            }

            return null;
        }

        private string GetSelectedStation()
        {
            if (rdoHeatDetector.Checked)
            {
                return rdoHeatDetector.Text;
            }

            if (rdoPushButton.Checked)
            {
                return rdoPushButton.Text;
            }

            if (rdoHornStrobe.Checked)
            {
                return rdoHornStrobe.Text;
            }

            return rdoSmokeDetector.Text;
        }

        private DeviceType GetSelectedDeviceType()
        {
            if (rdoHeatDetector.Checked)
            {
                return DeviceType.HeatDetector;
            }

            if (rdoPushButton.Checked)
            {
                return DeviceType.PushButton;
            }

            if (rdoHornStrobe.Checked)
            {
                return DeviceType.HornStrobe;
            }

            return DeviceType.SmokeDetector;
        }

        private DeviceTestState GetCurrentDeviceState()
        {
            return _deviceTestStates[GetSelectedDeviceType()];
        }

        private void ResetCurrentDeviceState()
        {
            GetCurrentDeviceState().Reset();
        }

        private static string ToHex(byte[] data)
        {
            return BitConverter.ToString(data).Replace('-', ' ');
        }

        private void rdoPushButton_CheckedChanged(object sender, EventArgs e)
        {

        }
    }
}
