using Basler.Pylon;
using Microsoft.Data.SqlClient;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using System;
using System;
using System.Collections.Generic;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq;
using System.Runtime.InteropServices;
using System.ServiceModel;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;
using ZXing;
using ZXing.Common;
// IMPORTANT aliases
using BaslerCamera = Basler.Pylon.Camera;
using CvPoint = OpenCvSharp.Point;
using CvRect = OpenCvSharp.Rect;
using CvSize = OpenCvSharp.Size;
using WpfPoint = System.Windows.Point;

namespace SCADA_VERTEX
{
    /// <summary>
    /// Interaction logic for frm_setting.xaml
    /// </summary>
    public partial class frm_setting : UserControl
    {
        internal static frm_setting? Data_transfer; // truyền từ form khác đến form này

        public BaslerCamera? _camera;
        private readonly object _frameLock = new();
        private Mat? _latestFrame;

        private bool _isLiveRunning = false;

        private CvRect _barcodeRoi;

        private Mat? _barcodeCaptureFrame;
        private string? _lastBarcodeText;

        private CvRect _liquidRoi;

        private Mat? _liquidCaptureFrame;

        private readonly List<int> _liquidLevels = new();
        private readonly List<double> _liquidHeights = new();
        private readonly List<double> _liquidLevelGradients = new();

        private bool _liquidHasLabel = false;

        private double _labelScore = 0.0;
        private double _boundaryScore = 0.0;

        private double[]? _liquidProfile;
        private double[]? _liquidProfileNorm;

        private double _liquidProfileMax = 0.0;
        private double _liquidProfileThreshold = 20.0;

        private double _liquidConfidence = 0.0;
        private string _liquidFailReason = "";

        private double? _pixelToMm = null;

        private const double TUBE_INNER_DIAMETER_MM = 12.0;
        private const double TUBE_INNER_RADIUS_MM = TUBE_INNER_DIAMETER_MM / 2.0;

        private double _rbcVolumeMl = double.NaN;
        private double _plasmaVolumeMl = double.NaN;
        private double _totalVolumeMl = double.NaN;

        private readonly object _triggerImageLock = new object();
        private readonly List<Mat> _triggeredImages = new List<Mat>();

        private int _maxTriggeredImages = 8; // keep latest 10 trigger images

        // ================= YOLO-SEG LIQUID MEASUREMENT FIELDS =================
        private InferenceSession? _yoloSegSession = null;
        private readonly object _yoloSegLock = new object();

        private string _onnxModelPath = System.IO.Path.Combine(
            AppContext.BaseDirectory,
            "Models",
            "yolo11_tube.onnx"
        );

        // Same default as your Python GUI.
        private const int YOLO_INPUT_SIZE = 640;
        private const double YOLO_CONF_THRESHOLD = 0.25;
        private const double YOLO_NMS_THRESHOLD = 0.45;
        private const double YOLO_MASK_THRESHOLD = 0.50;

        // Fallback class IDs if ONNX metadata does not contain class names.
        // Change these if your ONNX class order is different.
        private const int FALLBACK_RBC_CLASS_ID = 0;
        private const int FALLBACK_LABEL_CLASS_ID = 1;

        private readonly string[] RBC_KEYWORDS = new[] { "rbc", "dark", "liquid" };
        private readonly string[] LABEL_KEYWORDS = new[] { "label", "barcode" };

        private Mat? _yoloRbcMask = null;
        private Mat? _yoloLabelMask = null;
        private double _yoloRbcConf = 0.0;
        private double _yoloLabelConf = 0.0;

        private int? _rbcTopInRoi = null;
        private int? _rbcBottomInRoi = null;
        private int? _rbcBottomGlobal = null;

        private double _rbcHeightValue = double.NaN;
        private double _plasmaHeightValue = double.NaN;
        private double _totalLiquidHeightValue = double.NaN;

        private double _labelOcclusion = 0.0;
        private double _labelOverlap = 0.0;
        private bool _tiltCorrectionApplied = false;
        private double _tiltAngleBefore = 0.0;
        private double _tiltAngleAfter = 0.0;


        private class YoloSegDetection
        {
            public int ClassId { get; set; }
            public string ClassName { get; set; } = "";
            public double Confidence { get; set; }
            public CvRect Box { get; set; }
            public float[] MaskCoeffs { get; set; } = Array.Empty<float>();
        }

        private class YoloSegResult
        {
            public Mat RbcMask { get; set; } = new Mat();
            public Mat LabelMask { get; set; } = new Mat();
            public double RbcConf { get; set; }
            public double LabelConf { get; set; }
            public List<YoloSegDetection> Detections { get; set; } = new();
        }

        private readonly struct LetterboxInfo
        {
            public readonly double Scale;
            public readonly int NewW;
            public readonly int NewH;
            public readonly int PadX;
            public readonly int PadY;

            public LetterboxInfo(double scale, int newW, int newH, int padX, int padY)
            {
                Scale = scale;
                NewW = newW;
                NewH = newH;
                PadX = padX;
                PadY = padY;
            }
        }

        public frm_setting()
        {
            InitializeComponent();
            Fn_Show_Setting_To_Textbox();
            fn_Show_SQL_Setting();
            btt_Save_PLC.Visibility = Visibility.Hidden;
            Data_transfer = this;
        }

        #region 1. Các hàm para của PLC
        private void PLC_Parameter_Edit(object sender, RoutedEventArgs e)
        {
            txt_IOServer.IsEnabled = true;
            txt_ChannelDevice.IsEnabled = true;
            txt_ScanTime.IsEnabled = true;

            btt_Save_PLC.Visibility = Visibility.Visible;
            btt_Edit_PLC.Visibility = Visibility.Hidden;
        }

        private void PLC_Parameter_Save(object sender, RoutedEventArgs e)
        {
            bool Isconfirm = Class_Common.fn_Confirm();
            if (Isconfirm == true)
            {
                Properties.Settings.Default.IOServer = txt_IOServer.Text;
                Properties.Settings.Default.Channel = txt_ChannelDevice.Text;
                Properties.Settings.Default.PLC_Tags_Scan_Time = int.Parse(txt_ScanTime.Text);
                Properties.Settings.Default.Save();

                MessageBox.Show("Success !", "Notice !", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("The operation has been canceled.");
            }
            txt_IOServer.IsEnabled = false;
            txt_ChannelDevice.IsEnabled = false;
            txt_ScanTime.IsEnabled = false;

            btt_Save_PLC.Visibility = Visibility.Hidden;
            btt_Edit_PLC.Visibility = Visibility.Visible;
        }

        public void Fn_Show_Setting_To_Textbox()
        {
            // Load PLC Settings
            txt_IOServer.Text = Properties.Settings.Default.IOServer;
            txt_ChannelDevice.Text = Properties.Settings.Default.Channel;
            txt_ScanTime.Text = Properties.Settings.Default.PLC_Tags_Scan_Time.ToString();

            // Load Camera Setup Settings
            txt_ExposureTime.Text = Properties.Settings.Default.Ex_Man.ToString();
            txt_RawGain.Text = Properties.Settings.Default.Raw_Man.ToString();

            // Load Barcode Reading Settings
            txt_BcX.Text = Properties.Settings.Default.X_B.ToString();
            txt_BcY.Text = Properties.Settings.Default.Y_B.ToString();
            txt_BcW.Text = Properties.Settings.Default.W_B.ToString();
            txt_BcH.Text = Properties.Settings.Default.H_B.ToString();

            // Load Liquid Measurement Settings
            txt_LqX.Text = Properties.Settings.Default.X_M.ToString();
            txt_LqY.Text = Properties.Settings.Default.Y_M.ToString();
            txt_LqW.Text = Properties.Settings.Default.W_M.ToString();
            txt_LqH.Text = Properties.Settings.Default.H_M.ToString();
            txt_ProfileThresh.Text = Properties.Settings.Default.Profile_Thresh.ToString();
            txt_LabelThresh.Text = Properties.Settings.Default.Label_Thresh.ToString();
            txt_LqScale.Text = Properties.Settings.Default.SCALE.ToString();

            _barcodeRoi = CreateRoiFromSettings(
                Properties.Settings.Default.X_B,
                Properties.Settings.Default.Y_B,
                Properties.Settings.Default.W_B,
                Properties.Settings.Default.H_B,
                new CvRect(550, 200, 150, 150)   // fallback barcode ROI
            );

            _liquidRoi = CreateRoiFromSettings(
                Properties.Settings.Default.X_M,
                Properties.Settings.Default.Y_M,
                Properties.Settings.Default.W_M,
                Properties.Settings.Default.H_M,
                new CvRect(600, 200, 50, 650)    // fallback liquid ROI
            );
        }

        private static CvRect CreateRoiFromSettings(
            int x,
            int y,
            int w,
            int h,
            CvRect fallback)
        {
            if (w <= 0 || h <= 0)
                return fallback;

            x = Math.Max(0, x);
            y = Math.Max(0, y);
            w = Math.Max(0, w);
            h = Math.Max(0, h);

            return new CvRect(x, y, w, h);
        }

        private void Btt_Connect_OPC(object sender, RoutedEventArgs e)
        {
            bool Isconfirm = Class_Common.fn_Confirm();
            if (Isconfirm == true)
            {
                MainWindow.Data_transfer.fn_OPCConect();
                MessageBox.Show("Success !", "Notice !", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("The operation has been canceled.");
            }

        }
        private void Btt_Disconnect_OPC(object sender, RoutedEventArgs e)
        {
            bool Isconfirm = Class_Common.fn_Confirm();
            if (Isconfirm == true)
            {
                MainWindow.Data_transfer.fn_OPCDisconect();
                MessageBox.Show("Success !", "Notice !", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("The operation has been canceled.");
            }
        }
        public void Fn_Show_txt_Watchdog(string tag_watchdog)
        {
            Tag_Watchdog_Value.Text = tag_watchdog;
        }

        public void fn_connect_status(string tagValue)
        {
            bool connect_lost = Class_Common.CheckConnectionStatusPLC(tagValue);
            if (connect_lost == true)
            {
                txt_Status_PLC.Background = new SolidColorBrush(Colors.Green);
                txt_Status_PLC.Foreground = new SolidColorBrush(Colors.White);
                txt_Status_PLC.Text = "Connected";
                MainWindow.Data_transfer.txtPLCStatus.Text = "PLC: Connected";
            }
            else
            {
                txt_Status_PLC.Background = new SolidColorBrush(Colors.Red);
                txt_Status_PLC.Foreground = new SolidColorBrush(Colors.White);
                txt_Status_PLC.Text = "Disconnected";
                MainWindow.Data_transfer.txtPLCStatus.Text = "PLC: Disconnected";
            }
        }
        #endregion

        #region 2. Hàm cài đặt SQL
        private void btn_Edit_SQL_Click(object sender, RoutedEventArgs e)
        {
            txt_SqlDB.IsEnabled = true;
            txt_SqlUser.IsEnabled = true;
            txt_SqlPass.IsEnabled = true;

            btn_Save_SQL.Visibility = Visibility.Visible;
            btn_Edit_SQL.Visibility = Visibility.Collapsed;
        }

        private void btn_Save_SQL_Click(object sender, RoutedEventArgs e)
        {
            string dbName = txt_SqlDB.Text;
            string dbUser = txt_SqlUser.Text;
            string dbPassword = txt_SqlPass.Password;

            string sqlString = $@"Data Source = localhost\sqlexpress; Initial Catalog = {dbName}; User ID = {dbUser}; Password = {dbPassword}; TrustServerCertificate=True";

            try
            {
                using (SqlConnection conn = new SqlConnection(sqlString))
                {
                    conn.Open();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Kết nối SQL thất bại! Vui lòng kiểm tra lại.\n\nChi tiết: " + ex.Message, "Lỗi kết nối", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            Properties.Settings.Default.SQL_DB_Name = dbName;
            Properties.Settings.Default.SQL_DB_User = dbUser;
            Properties.Settings.Default.SQL_DB_Password = dbPassword;
            Properties.Settings.Default.SQL_String = sqlString;
            Properties.Settings.Default.Save();

            MessageBox.Show("Kết nối thành công và đã lưu thông số!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);

            txt_SqlDB.IsEnabled = false;
            txt_SqlUser.IsEnabled = false;
            txt_SqlPass.IsEnabled = false;

            btn_Save_SQL.Visibility = Visibility.Collapsed;
            btn_Edit_SQL.Visibility = Visibility.Visible;
        }

        public void fn_Show_SQL_Setting()
        {
            txt_SqlDB.Text = Properties.Settings.Default.SQL_DB_Name;
            txt_SqlUser.Text = Properties.Settings.Default.SQL_DB_User;
            txt_SqlPass.Password = Properties.Settings.Default.SQL_DB_Password;
        }
        #endregion

        #region 3. Camera Functions (OpenCV Requirements)

        // -------------------------------------------------------------
        // Xử lý sự kiện UI: Chỉ cho phép nhập số nguyên vào các ô ROI
        // -------------------------------------------------------------
        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        // -------------------------------------------------------------
        // Xử lý UI: Auto/Manual Radio Button Check/Uncheck
        // -------------------------------------------------------------
        private void CamSetup_Mode_Changed(object sender, RoutedEventArgs e)
        {
            if (txt_ExposureTime == null || txt_RawGain == null ||
                rad_ExpAuto == null || rad_GainAuto == null)
            {
                return;
            }

            // Xử lý ô Exposure Time
            if (rad_ExpAuto.IsChecked == true)
            {
                txt_ExposureTime.IsReadOnly = true;
                txt_ExposureTime.Background = new SolidColorBrush(Color.FromRgb(211, 211, 211));
            }
            else
            {
                txt_ExposureTime.IsReadOnly = false;
                txt_ExposureTime.Background = Brushes.White;
            }

            // Xử lý ô Raw Gain
            if (rad_GainAuto.IsChecked == true)
            {
                txt_RawGain.IsReadOnly = true;
                txt_RawGain.Background = new SolidColorBrush(Color.FromRgb(211, 211, 211));
            }
            else
            {
                txt_RawGain.IsReadOnly = false;
                txt_RawGain.Background = Brushes.White;
            }
        }

        private void btn_CamOpen_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_camera != null)
                {
                    MessageBox.Show("Camera already opened. " + _camera.ToString());
                    return;
                }

                var camera = new BaslerCamera();
                camera.Open();

                _camera = camera;

                // Set pixel format first
                TrySetEnumParameter(camera, "PixelFormat", "Mono8");

                // Apply Exposure/Gain from textbox + radio buttons
                ApplyCameraExposureGainSettings();

                camera.StreamGrabber!.ImageGrabbed += OnImageGrabbed;

                MessageBox.Show("Camera opened. Click LIVE ON to start streaming.");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Open Camera Error: " + ex.Message);
            }
        }

        private frm_JogPopup? _jogPopup;

        private void btn_JogCam_Click(object sender, RoutedEventArgs e)
        {
            // Nếu popup chưa được khởi tạo, hoặc người dùng đã tắt nó đi (IsLoaded = false)
            if (_jogPopup == null || !_jogPopup.IsLoaded)
            {
                _jogPopup = new frm_JogPopup();
                _jogPopup.Show(); // Dùng Show() để mở song song, không dùng ShowDialog() vì sẽ khóa màn hình chính
            }
            else
            {
                // Nếu đã mở rồi thì lôi nó lên trên cùng (phòng khi bị chìm)
                _jogPopup.Activate();
                _jogPopup.Focus();
            }
        }

        private void btn_CamClose_Click(object sender, RoutedEventArgs e)
        {
            CloseCamera();
            ClearCameraDisplay();

        }

        private void btn_CamLiveOn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_camera == null)
                {
                    MessageBox.Show("Please open camera first.");
                    return;
                }
                SetCameraTriggerMode(false);

                if (_camera.StreamGrabber!.IsGrabbing)
                {
                    MessageBox.Show("Live is already running.");
                    return;
                }


                _isLiveRunning = true;

                _camera.StreamGrabber!.Start(
                    GrabStrategy.OneByOne,
                    GrabLoop.ProvidedByStreamGrabber
                );
            }
            catch (Exception ex)
            {
                MessageBox.Show("Live ON Error: " + ex.Message);
            }
        }

        private void btn_CamLiveOff_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _isLiveRunning = false;

                if (_camera != null && _camera.StreamGrabber!.IsGrabbing)
                {
                    _camera.StreamGrabber.Stop();
                    ClearCameraDisplay();

                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Live OFF Error: " + ex.Message);
            }
        }

        private void CloseCamera()
        {
            try
            {
                _isLiveRunning = false;

                if (_camera != null)
                {
                    try
                    {
                        if (_camera.StreamGrabber!.IsGrabbing)
                            _camera.StreamGrabber.Stop();
                    }
                    catch { }

                    try
                    {
                        _camera.StreamGrabber!.ImageGrabbed -= OnImageGrabbed;
                    }
                    catch { }

                    try
                    {
                        if (_camera.IsOpen)
                            _camera.Close();
                    }
                    catch { }

                    _camera.Dispose();
                    _camera = null;
                }

                lock (_frameLock)
                {
                    _latestFrame?.Dispose();
                    _latestFrame = null;
                }

                _liquidCaptureFrame?.Dispose();
                _liquidCaptureFrame = null;

                imgCameraLive.Source = null;
                imgCameraResult.Source = null;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Close Camera Error: " + ex.Message);
            }
        }

        private void OnImageGrabbed(object? sender, ImageGrabbedEventArgs e)
        {
            try
            {
                using IGrabResult grabResult = e.GrabResult;

                if (!grabResult.GrabSucceeded)
                    return;

                int width = grabResult.Width;
                int height = grabResult.Height;

                byte[] buffer = grabResult.PixelData as byte[] ?? Array.Empty<byte>();

                if (buffer.Length == 0)
                    return;

                using Mat temp = Mat.FromPixelData(
                    height,
                    width,
                    MatType.CV_8UC1,
                    buffer
                );

                using Mat frame = temp.Clone();

                lock (_frameLock)
                {
                    _latestFrame?.Dispose();
                    _latestFrame = frame.Clone();
                }
                if (frm_main.Data_transfer!._isAutoMode )
                {
                    lock (_triggerImageLock)
                    {
                        _triggeredImages.Add(frame.Clone());
                        // --- THÊM 3 DÒNG NÀY ĐỂ IN RA TERMINAL ---
                        Debug.WriteLine($"[Camera Trigger] Đã nhận được ảnh thứ {_triggeredImages.Count}");
                        if (_triggeredImages.Count == 8)
                        {
                            Debug.WriteLine("=> [SUCCESS] ĐÃ NHẬN ĐỦ 8 ẢNH TRONG MẢNG CHỜ XỬ LÝ!");
                        }

                        while (_triggeredImages.Count > _maxTriggeredImages)
                        {
                            _triggeredImages[0].Dispose();
                            _triggeredImages.RemoveAt(0);
                        }
                    }
                }

                using Mat show = DrawRoiOnFrame(frame);
                BitmapSource source = MatToBitmapSourceSafe(show);

                Dispatcher.BeginInvoke(() =>
                {
                    imgCameraLive.Source = source;
                });
            }
            catch
            {
                // Ignore one bad frame.
            }
        }

        private void btn_LiquidSave_Click(object sender, RoutedEventArgs e)
        {
            int x = GetInt(txt_LqX.Text, 600);
            int y = GetInt(txt_LqY.Text, 200);
            int w = GetInt(txt_LqW.Text, 50);
            int h = GetInt(txt_LqH.Text, 650);

            _liquidRoi = new CvRect(x, y, w, h);

            MessageBox.Show("Liquid ROI saved.");
        }

        private void btn_LiquidTest_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                bool success = MeasureLiquid();

                var confidenceResult = CalculateLiquidConfidence();
                _liquidConfidence = confidenceResult.confidence;
                _liquidFailReason = confidenceResult.reason;

                using (Mat result = MeasureLiquidShow())
                    ShowMatOnImage(result, imgCameraResult);

                using (Mat graph = CreateGradientGraph())
                    ShowMatOnImage(graph, imgLiquidProfileGraph);

                txt_LabelScore.Text = _labelOcclusion.ToString("0.000");
                txt_ConfidenceScore.Text = _liquidConfidence.ToString("0.000");
                txt_FailReason.Text = _liquidFailReason;

                txt_LqPlasma.Text = "";
                txt_LqRbc.Text = "";

                if (!success)
                {
                    if (_liquidHasLabel)
                    {
                        txt_LqPlasma.Text = "LABEL";
                        txt_LqRbc.Text = "REJECT";
                    }
                    else
                    {
                        txt_LqPlasma.Text = "No level";
                        txt_LqRbc.Text = "No level";
                    }
                    return;
                }

                string unit = _pixelToMm.HasValue ? " mm" : " px";
                txt_LqPlasma.Text = double.IsNaN(_plasmaHeightValue) ? "No level" : _plasmaHeightValue.ToString("0.00") + unit;
                txt_LqRbc.Text = double.IsNaN(_rbcHeightValue) ? "No level" : _rbcHeightValue.ToString("0.00") + unit;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Liquid test error: " + ex.Message);
            }
        }

        private bool MeasureLiquid()
        {
            Mat? captured = null;

            try
            {
                lock (_frameLock)
                {
                    if (_latestFrame == null || _latestFrame.Empty())
                    {
                        _liquidFailReason = "No camera frame";
                        return false;
                    }

                    captured = _latestFrame.Clone();
                }

                return MeasureLiquidCore(captured);
            }
            catch (Exception ex)
            {
                _liquidFailReason = "Measure error: " + ex.Message;
                return false;
            }
            finally
            {
                captured?.Dispose();
            }
        }

        private (bool hasLabel, double labelScore, double boundaryScore, double barcodeScore) DetectLabel(Mat roiImg)
        {
            if (roiImg == null || roiImg.Empty())
                return (false, 0.0, 0.0, 0.0);

            using Mat gray = new Mat();

            if (roiImg.Channels() == 1)
                roiImg.CopyTo(gray);
            else
                Cv2.CvtColor(roiImg, gray, ColorConversionCodes.BGR2GRAY);

            double labelThreshold = GetDoubleSafe(txt_LabelThresh, 0.10);

            using Mat blur = new Mat();
            Cv2.GaussianBlur(gray, blur, new CvSize(3, 3), 0);

            using Mat sobelX64 = new Mat();
            Cv2.Sobel(blur, sobelX64, MatType.CV_64F, 1, 0, ksize: 3);

            using Mat sobelXAbs = new Mat();
            Cv2.ConvertScaleAbs(sobelX64, sobelXAbs);

            using Mat textBinary = new Mat();
            Cv2.Threshold(sobelXAbs, textBinary, 40, 255, ThresholdTypes.Binary);

            double textEdgeDensity =
                Cv2.CountNonZero(textBinary) /
                Math.Max(1.0, textBinary.Rows * textBinary.Cols);

            using Mat sobelY64 = new Mat();
            Cv2.Sobel(blur, sobelY64, MatType.CV_64F, 0, 1, ksize: 3);

            using Mat sobelYAbs = new Mat();
            Cv2.ConvertScaleAbs(sobelY64, sobelYAbs);

            using Mat stripeBinary = new Mat();
            Cv2.Threshold(sobelYAbs, stripeBinary, 35, 255, ThresholdTypes.Binary);

            using Mat horizontalKernel = Cv2.GetStructuringElement(MorphShapes.Rect, new CvSize(25, 1));
            using Mat horizontalLines = new Mat();
            Cv2.MorphologyEx(stripeBinary, horizontalLines, MorphTypes.Open, horizontalKernel);

            double barcodeScore =
                Cv2.CountNonZero(horizontalLines) /
                Math.Max(1.0, horizontalLines.Rows * horizontalLines.Cols);

            int height = sobelYAbs.Rows;
            int width = sobelYAbs.Cols;
            int yStart = (int)(height * 0.10);
            int yEnd = (int)(height * 0.90);

            double maxRowMean = 0.0;
            for (int y = yStart; y < yEnd; y++)
            {
                double rowSum = 0.0;
                for (int x = 0; x < width; x++)
                    rowSum += sobelYAbs.At<byte>(y, x);

                double rowMean = rowSum / Math.Max(width, 1);
                if (rowMean > maxRowMean)
                    maxRowMean = rowMean;
            }

            double boundaryScore = maxRowMean / 255.0;
            double barcodeThreshold = 0.015;
            double finalScore = Math.Max(textEdgeDensity, barcodeScore);

            bool hasLabel = finalScore > labelThreshold || barcodeScore > barcodeThreshold;
            return (hasLabel, finalScore, boundaryScore, barcodeScore);
        }

        private (double confidence, string reason) CalculateLiquidConfidence()
        {
            //if (_liquidProfileNorm == null || _liquidProfileNorm.Length == 0)
            //    return (0.0, "No profile");

            double labelThreshold = GetDoubleSafe(txt_LabelThresh, 0.10);

            if (_labelOcclusion >= labelThreshold)
                return (0.0, "Label occlusion");

            if (_liquidLevelGradients.Count == 0)
                return (0.0, _liquidFailReason);

            double strongestPeakRaw = _liquidLevelGradients.Max();
            double rawThreshold = GetDoubleSafe(txt_ProfileThresh, 20.0);

            double peakScore = Math.Min(1.0, strongestPeakRaw / Math.Max(rawThreshold * 2.0, 1e-6));
            double labelClearScore = Math.Max(0.0, 1.0 - _labelOcclusion / Math.Max(labelThreshold, 1e-6));
            double rbcScore = Math.Min(1.0, _yoloRbcConf / 0.50);
            double boundaryQuality = _boundaryScore;

            int minDistance = 70;
            double duplicatePenalty = 0.0;
            if (_liquidLevels.Count >= 2)
            {
                int distance = Math.Abs(_liquidLevels[1] - _liquidLevels[0]);
                if (distance < minDistance)
                    duplicatePenalty = 1.0 - distance / (double)minDistance;
            }

            double tiltScore = 1.0;
            double residualTilt = _tiltCorrectionApplied ? Math.Abs(_tiltAngleAfter) : Math.Abs(_tiltAngleBefore);
            tiltScore = Math.Max(0.0, 1.0 - residualTilt / 5.0);

            double confidence =
                0.35 * peakScore +
                0.22 * boundaryQuality +
                0.18 * labelClearScore +
                0.15 * rbcScore +
                0.10 * tiltScore;

            confidence -= 0.25 * duplicatePenalty;

            if (_liquidFailReason != "OK")
                confidence *= 0.5;

            confidence = Math.Max(0.0, Math.Min(1.0, confidence));
            string reason = _liquidFailReason == "OK" ? "OK" : _liquidFailReason;
            return (confidence, reason);
        }

        private Mat MeasureLiquidShow()
        {
            if (_liquidCaptureFrame == null || _liquidCaptureFrame.Empty())
                return CreateBlankResultImage("No liquid captured image");

            Mat result = ToBgr(_liquidCaptureFrame);

            if (_yoloLabelMask != null && !_yoloLabelMask.Empty())
                OverlayMask(result, _yoloLabelMask, new Scalar(0, 0, 255), 0.25);

            if (_yoloRbcMask != null && !_yoloRbcMask.Empty())
                OverlayMask(result, _yoloRbcMask, new Scalar(255, 0, 0), 0.25);

            CvRect? safe = MakeSafeRoi(_liquidRoi, result.Width, result.Height);
            if (safe == null)
                return result;

            CvRect roi = safe.Value;
            bool accepted = _liquidFailReason == "OK";
            Scalar roiColor = accepted ? new Scalar(0, 255, 0) : new Scalar(0, 0, 255);

            Cv2.Rectangle(result, roi, roiColor, 3);

            if (_rbcBottomInRoi.HasValue)
            {
                int yy = roi.Y + _rbcBottomInRoi.Value;
                Cv2.Line(result, new CvPoint(roi.X, yy), new CvPoint(roi.X + roi.Width, yy), new Scalar(255, 0, 255), 2);
                Cv2.PutText(result, "RBC bottom = 0", new CvPoint(roi.X + 5, Math.Max(25, yy - 8)),
                    HersheyFonts.HersheySimplex, 0.55, new Scalar(255, 0, 255), 2);
            }

            for (int i = 0; i < _liquidLevels.Count; i++)
            {
                int lv = _liquidLevels[i];
                int yy = roi.Y + lv;

                Cv2.Line(result, new CvPoint(roi.X, yy), new CvPoint(roi.X + roi.Width, yy), new Scalar(0, 255, 255), 2);

                string unit = _pixelToMm.HasValue ? "mm" : "px";
                string label;
                if (i == 0)
                {
                    label = double.IsNaN(_rbcHeightValue)
                        ? "Level 1 RBC/plasma"
                        : $"Level 1 RBC: {_rbcHeightValue:F2} {unit}";
                }
                else
                {
                    label = double.IsNaN(_plasmaHeightValue)
                        ? "Level 2 plasma top"
                        : $"Level 2 plasma: {_plasmaHeightValue:F2} {unit}";
                }

                Cv2.PutText(result, label, new CvPoint(roi.X + 10, Math.Max(25, yy - 10)),
                    HersheyFonts.HersheySimplex, 0.60, new Scalar(0, 255, 255), 2);
            }

            Cv2.Rectangle(result, new CvRect(5, 5, Math.Min(920, result.Width - 10), 105), new Scalar(0, 0, 0), -1);
            Cv2.PutText(result, accepted ? "ACCEPTED" : "REJECTED", new CvPoint(15, 32),
                HersheyFonts.HersheySimplex, 0.75, accepted ? new Scalar(0, 255, 0) : new Scalar(0, 0, 255), 2);

            string msg = accepted
                ? $"RBC={_rbcHeightValue:F2}, Plasma={_plasmaHeightValue:F2}, Conf={_liquidConfidence:F3}"
                : $"Reason: {_liquidFailReason}";

            Cv2.PutText(result, msg, new CvPoint(15, 62), HersheyFonts.HersheySimplex, 0.55, new Scalar(255, 255, 255), 1);
            Cv2.PutText(result, $"YOLO RBC={_yoloRbcConf:F3}, LABEL={_yoloLabelConf:F3}, LabelOcc={_labelOcclusion:F3}",
                new CvPoint(15, 88), HersheyFonts.HersheySimplex, 0.50, new Scalar(255, 255, 255), 1);

            return result;
        }

        private Mat CreateGradientGraph()
        {
            int graphW = 760;
            int graphH = 620;
            int marginLeft = 95;
            int marginRight = 45;
            int marginTop = 45;
            int marginBottom = 65;

            Scalar bgColor = new Scalar(255, 245, 230);       // BGR light blue
            Scalar plotBgColor = new Scalar(255, 250, 240);
            Scalar borderColor = new Scalar(80, 80, 80);
            Scalar gridColor = new Scalar(205, 210, 215);
            Scalar textColor = new Scalar(20, 20, 20);
            Scalar profileColor = new Scalar(130, 60, 0);
            Scalar thresholdColor = new Scalar(0, 0, 220);
            Scalar levelLineColor = new Scalar(60, 60, 60);
            Scalar levelPointColor = new Scalar(0, 0, 220);
            Scalar rbcBottomColor = new Scalar(180, 0, 180);
            Scalar statusColor = _liquidFailReason == "OK" ? new Scalar(0, 120, 0) : new Scalar(0, 0, 180);

            Mat img = new Mat(graphH, graphW, MatType.CV_8UC3, bgColor);

            if (_liquidProfile == null || _liquidProfile.Length == 0)
            {
                Cv2.PutText(img, "No Sobel-Y profile available", new CvPoint(70, 300),
                    HersheyFonts.HersheySimplex, 0.8, textColor, 2);
                return img;
            }

            double[] profile = _liquidProfile;
            int n = profile.Length;
            double rawThreshold = _liquidProfileThreshold;
            double maxProfile = profile.Max();
            double maxXValue = Math.Max(Math.Max(maxProfile, rawThreshold), 1.0) * 1.15;

            int plotX1 = marginLeft;
            int plotY1 = marginTop;
            int plotX2 = graphW - marginRight;
            int plotY2 = graphH - marginBottom;
            int plotW = plotX2 - plotX1;
            int plotH = plotY2 - plotY1;

            Cv2.Rectangle(img, new CvPoint(plotX1, plotY1), new CvPoint(plotX2, plotY2), plotBgColor, -1);
            Cv2.Rectangle(img, new CvPoint(plotX1, plotY1), new CvPoint(plotX2, plotY2), borderColor, 1);

            for (int i = 1; i < 6; i++)
            {
                int xx = plotX1 + (int)(plotW * i / 6.0);
                Cv2.Line(img, new CvPoint(xx, plotY1), new CvPoint(xx, plotY2), gridColor, 1);
            }

            for (int i = 1; i < 6; i++)
            {
                int yy = plotY1 + (int)(plotH * i / 6.0);
                Cv2.Line(img, new CvPoint(plotX1, yy), new CvPoint(plotX2, yy), gridColor, 1);
            }

            Cv2.PutText(img, "Sobel-Y gradient profile", new CvPoint(marginLeft, 25),
                HersheyFonts.HersheySimplex, 0.65, textColor, 2);
            Cv2.PutText(img, "Gradient magnitude", new CvPoint(graphW / 2 - 90, graphH - 18),
                HersheyFonts.HersheySimplex, 0.55, textColor, 1);
            Cv2.PutText(img, "Y", new CvPoint(35, plotY1 + 15),
                HersheyFonts.HersheySimplex, 0.65, textColor, 2);
            Cv2.PutText(img, "0", new CvPoint(55, plotY1 + 5),
                HersheyFonts.HersheySimplex, 0.45, textColor, 1);
            Cv2.PutText(img, (n - 1).ToString(), new CvPoint(45, plotY2 + 5),
                HersheyFonts.HersheySimplex, 0.45, textColor, 1);
            Cv2.PutText(img, "0", new CvPoint(plotX1 - 5, graphH - 42),
                HersheyFonts.HersheySimplex, 0.45, textColor, 1);
            Cv2.PutText(img, maxXValue.ToString("F1"), new CvPoint(plotX2 - 55, graphH - 42),
                HersheyFonts.HersheySimplex, 0.45, textColor, 1);

            List<CvPoint> points = new();
            for (int i = 0; i < n; i++)
            {
                double value = Math.Max(0.0, profile[i]);
                int px = plotX1 + (int)(value / maxXValue * plotW);
                int py = plotY1 + (int)(i / Math.Max(1.0, n - 1) * plotH);
                points.Add(new CvPoint(px, py));
            }

            for (int i = 1; i < points.Count; i++)
                Cv2.Line(img, points[i - 1], points[i], profileColor, 2);

            int thresholdX = plotX1 + (int)(rawThreshold / maxXValue * plotW);
            Cv2.Line(img, new CvPoint(thresholdX, plotY1), new CvPoint(thresholdX, plotY2), thresholdColor, 2);
            Cv2.PutText(img, $"Threshold = {rawThreshold:F1}", new CvPoint(Math.Min(thresholdX + 8, plotX2 - 180), plotY1 + 25),
                HersheyFonts.HersheySimplex, 0.50, thresholdColor, 2);

            for (int idx = 0; idx < _liquidLevels.Count; idx++)
            {
                int lv = _liquidLevels[idx];
                if (lv < 0 || lv >= n) continue;

                CvPoint p = points[lv];
                double rawValue = profile[lv];

                Cv2.Line(img, new CvPoint(plotX1, p.Y), new CvPoint(plotX2, p.Y), levelLineColor, 1);
                Cv2.Circle(img, p, 7, levelPointColor, -1);
                Cv2.Circle(img, p, 11, textColor, 2);

                string levelName = idx == 0 ? "L1 RBC/plasma" : "L2 plasma top";
                int textX = Math.Min(p.X + 12, plotX2 - 230);
                int textY = Math.Max(plotY1 + 20, p.Y - 8);

                Cv2.PutText(img, $"{levelName}: y={lv}, G={rawValue:F1}", new CvPoint(textX, textY),
                    HersheyFonts.HersheySimplex, 0.45, textColor, 1);
            }

            if (_rbcBottomInRoi.HasValue && n > 1)
            {
                int by = plotY1 + (int)(_rbcBottomInRoi.Value / Math.Max(1.0, n - 1) * plotH);
                Cv2.Line(img, new CvPoint(plotX1, by), new CvPoint(plotX2, by), rbcBottomColor, 1);
                Cv2.PutText(img, "RBC bottom baseline", new CvPoint(plotX1 + 10, Math.Max(plotY1 + 20, by - 8)),
                    HersheyFonts.HersheySimplex, 0.42, rbcBottomColor, 1);
            }

            string status = $"Max gradient: {maxProfile:F1} | Levels: {_liquidLevels.Count} | Status: {_liquidFailReason}";
            Cv2.PutText(img, status, new CvPoint(plotX1, graphH - 42), HersheyFonts.HersheySimplex, 0.50, statusColor, 1);

            return img;
        }

        private static void TrySetEnumParameter(BaslerCamera camera, string parameterName, string value)
        {
            try
            {
                IParameter parameter = camera.Parameters[parameterName];

                if (parameter is IEnumParameter enumParameter)
                {
                    enumParameter.TrySetValue(value);
                }
            }
            catch
            {
                // Camera may not support this parameter.
            }
        }

        private Mat DrawRoiOnFrame(Mat frame)
        {
            Mat show = ToBgr(frame);

            CvRect? barcodeSafe = MakeSafeRoi(_barcodeRoi, show.Width, show.Height);

            if (barcodeSafe != null)
            {
                CvRect roi = barcodeSafe.Value;

                Cv2.Rectangle(
                    show,
                    roi,
                    new Scalar(255, 0, 0),
                    2
                );

                Cv2.PutText(
                    show,
                    "BARCODE",
                    new CvPoint(roi.X, Math.Max(25, roi.Y - 8)),
                    HersheyFonts.HersheySimplex,
                    0.7,
                    new Scalar(255, 0, 0),
                    2
                );
            }

            CvRect? liquidSafe = MakeSafeRoi(
                _liquidRoi,
                show.Width,
                show.Height
            );

            if (liquidSafe != null)
            {
                CvRect roi = liquidSafe.Value;

                Cv2.Rectangle(
                    show,
                    roi,
                    new Scalar(0, 255, 0),
                    2
                );

                Cv2.PutText(
                    show,
                    "LIQUID",
                    new CvPoint(roi.X, Math.Max(25, roi.Y - 8)),
                    HersheyFonts.HersheySimplex,
                    0.7,
                    new Scalar(0, 255, 0),
                    2
                );
            }

            return show;
        }

        private static void OverlayMask(Mat image, Mat mask, Scalar color, double alpha)
        {
            if (image == null || image.Empty() || mask == null || mask.Empty())
                return;

            using Mat safeMask = new Mat();
            if (mask.Size() != image.Size())
                Cv2.Resize(mask, safeMask, new CvSize(image.Width, image.Height), 0, 0, InterpolationFlags.Nearest);
            else
                mask.CopyTo(safeMask);

            using Mat colorLayer = new Mat(image.Size(), MatType.CV_8UC3, color);
            using Mat blended = new Mat();
            Cv2.AddWeighted(image, 1.0 - alpha, colorLayer, alpha, 0.0, blended);
            blended.CopyTo(image, safeMask);
        }

        private static CvPoint[] GetNonZeroPoints(Mat mask)
        {
            if (mask == null || mask.Empty())
                return Array.Empty<CvPoint>();

            using Mat ptsMat = new Mat();
            Cv2.FindNonZero(mask, ptsMat);

            if (ptsMat.Empty())
                return Array.Empty<CvPoint>();

            int count = ptsMat.Rows * ptsMat.Cols;
            CvPoint[] pts = new CvPoint[count];

            for (int i = 0; i < count; i++)
            {
                // OpenCvSharp stores FindNonZero output as CV_32SC2 points.
                pts[i] = ptsMat.At<CvPoint>(i);
            }

            return pts;
        }

        private static Mat ToBgr(Mat src)
        {
            if (src.Channels() == 3)
                return src.Clone();

            Mat bgr = new Mat();
            Cv2.CvtColor(src, bgr, ColorConversionCodes.GRAY2BGR);
            return bgr;
        }

        private void ShowMatOnImage(Mat mat, System.Windows.Controls.Image imageControl)
        {
            try
            {
                if (mat == null || mat.Empty())
                    return;

                using Mat safeMat = mat.Clone();

                BitmapSource source = BitmapSourceConverter.ToBitmapSource(safeMat);
                source.Freeze();

                Dispatcher.BeginInvoke(() =>
                {
                    imageControl.Source = source;
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Show image error: " + ex.Message);
            }
        }

        private static BitmapSource MatToBitmapSourceSafe(Mat mat)
        {
            if (mat == null || mat.Empty())
                throw new ArgumentException("Mat is null or empty.");

            using Mat clone = mat.Clone();

            BitmapSource source = BitmapSourceConverter.ToBitmapSource(clone);
            source.Freeze();

            return source;
        }

        private static CvRect MakeSafeRect(CvRect roi, int imageWidth, int imageHeight)
        {
            int x = Math.Max(0, roi.X);
            int y = Math.Max(0, roi.Y);

            int w = Math.Min(roi.Width, imageWidth - x);
            int h = Math.Min(roi.Height, imageHeight - y);

            if (w <= 0 || h <= 0)
                return new CvRect(0, 0, 0, 0);

            return new CvRect(x, y, w, h);
        }

        private static CvRect? MakeSafeRoi(CvRect roi, int imageWidth, int imageHeight)
        {
            int x = Math.Max(0, roi.X);
            int y = Math.Max(0, roi.Y);

            int w = Math.Min(roi.Width, imageWidth - x);
            int h = Math.Min(roi.Height, imageHeight - y);

            if (w <= 0 || h <= 0)
                return null;

            return new CvRect(x, y, w, h);
        }

        private static int GetInt(string text, int defaultValue)
        {
            return int.TryParse(text, out int value)
                ? value
                : defaultValue;
        }

        private static double GetDouble(string text, double defaultValue)
        {
            return double.TryParse(text, out double value)
                ? value
                : defaultValue;
        }

        // Hàm đọc TextBox an toàn cho luồng ngầm (Background Thread)
        private double GetDoubleSafe(TextBox txt, double defaultValue)
        {
            double result = defaultValue;
            Application.Current?.Dispatcher?.Invoke(() =>
            {
                if (double.TryParse(txt.Text, out double val))
                    result = val;
            });
            return result;
        }

        private static int FindMaxIndex(double[] values)
        {
            if (values.Length == 0)
                return -1;

            int maxIndex = 0;
            double maxValue = values[0];

            for (int i = 1; i < values.Length; i++)
            {
                if (values[i] > maxValue)
                {
                    maxValue = values[i];
                    maxIndex = i;
                }
            }

            return maxValue <= 0 ? -1 : maxIndex;
        }

        private static double[] RowMean(Mat mat64)
        {
            int rows = mat64.Rows;
            int cols = mat64.Cols;

            double[] profile = new double[rows];

            for (int y = 0; y < rows; y++)
            {
                double sum = 0.0;

                for (int x = 0; x < cols; x++)
                    sum += mat64.At<double>(y, x);

                profile[y] = sum / Math.Max(cols, 1);
            }

            return profile;
        }

        private static double[] SmoothProfile(double[] profile, int kernelSize)
        {
            if (profile.Length == 0)
                return Array.Empty<double>();

            if (kernelSize % 2 == 0)
                kernelSize++;

            int radius = kernelSize / 2;
            double sigma = kernelSize / 6.0;

            double[] kernel = new double[kernelSize];
            double sum = 0.0;

            for (int i = 0; i < kernelSize; i++)
            {
                int x = i - radius;
                double value = Math.Exp(-(x * x) / (2 * sigma * sigma));

                kernel[i] = value;
                sum += value;
            }

            for (int i = 0; i < kernelSize; i++)
                kernel[i] /= sum;

            double[] smoothed = new double[profile.Length];

            for (int i = 0; i < profile.Length; i++)
            {
                double acc = 0.0;

                for (int k = 0; k < kernelSize; k++)
                {
                    int srcIndex = i + k - radius;
                    srcIndex = Math.Max(0, Math.Min(profile.Length - 1, srcIndex));

                    acc += profile[srcIndex] * kernel[k];
                }

                smoothed[i] = acc;
            }

            return smoothed;
        }

        private static double Median(double[] values)
        {
            if (values.Length == 0)
                return 0.0;

            double[] sorted = values.OrderBy(v => v).ToArray();

            int mid = sorted.Length / 2;

            if (sorted.Length % 2 == 0)
                return (sorted[mid - 1] + sorted[mid]) / 2.0;

            return sorted[mid];
        }

        private static Mat CreateBlankResultImage(string text)
        {
            Mat blank = new Mat(
                300,
                500,
                MatType.CV_8UC3,
                new Scalar(0, 0, 0)
            );

            Cv2.PutText(
                blank,
                text,
                new CvPoint(30, 150),
                HersheyFonts.HersheySimplex,
                0.8,
                new Scalar(255, 255, 255),
                2
            );

            return blank;
        }

        private void ClearCameraDisplay()
        {

            txt_BcResult.Text = "";
            _lastBarcodeText = null;

            _barcodeCaptureFrame?.Dispose();
            _barcodeCaptureFrame = null;

            _barcodeCaptureFrame?.Dispose();
            _barcodeCaptureFrame = null;

            imgCameraLive.Source = null;
            imgCameraResult.Source = null;
            imgLiquidProfileGraph.Source = null;

            txt_LqPlasma.Text = "";
            txt_LqRbc.Text = "";
            txt_LabelScore.Text = "0.000";
            txt_ConfidenceScore.Text = "0.000";
            txt_FailReason.Text = "";

            _liquidLevels.Clear();
            _liquidHeights.Clear();
            _liquidLevelGradients.Clear();

            _liquidProfile = Array.Empty<double>();
            _liquidProfileNorm = Array.Empty<double>();

            _labelScore = 0.0;
            _boundaryScore = 0.0;
            _liquidConfidence = 0.0;
            _liquidFailReason = "";


            _labelOcclusion = 0.0;
            _labelOverlap = 0.0;
            _yoloRbcConf = 0.0;
            _yoloLabelConf = 0.0;
            _rbcTopInRoi = null;
            _rbcBottomInRoi = null;
            _rbcBottomGlobal = null;
            _rbcHeightValue = double.NaN;
            _plasmaHeightValue = double.NaN;
            _totalLiquidHeightValue = double.NaN;
            _tiltCorrectionApplied = false;
            _tiltAngleBefore = 0.0;
            _tiltAngleAfter = 0.0;

            _yoloRbcMask?.Dispose();
            _yoloRbcMask = null;
            _yoloLabelMask?.Dispose();
            _yoloLabelMask = null;
        }

        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            //CloseCamera();
            //_liquidCaptureFrame?.Dispose();

            //_yoloSegSession?.Dispose();
            //_yoloSegSession = null;
            //_yoloRbcMask?.Dispose();
            //_yoloLabelMask?.Dispose();
        }

        // === NÚT LƯU THÔNG SỐ SETUP CAMERA ===
        private void btn_CamSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Properties.Settings.Default.Ex_Man = float.Parse(txt_ExposureTime.Text);
                Properties.Settings.Default.Raw_Man = float.Parse(txt_RawGain.Text);
                Properties.Settings.Default.Save();

                if (_camera == null || !_camera.IsOpen)
                {
                    MessageBox.Show("Camera chưa mở. Hãy OPEN camera trước.");
                    return;
                }

                ApplyCameraExposureGainSettings();

                MessageBox.Show(
                    "Đã áp dụng Exposure và Gain cho camera!",
                    "Apply",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
            }
            catch (Exception ex)
            {
                MessageBox.Show("Apply camera settings error: " + ex.Message);
            }
        }

        // === NÚT LƯU THÔNG SỐ BARCODE ===
        private void btn_BarcodeSave_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.X_B = int.Parse(txt_BcX.Text);
            Properties.Settings.Default.Y_B = int.Parse(txt_BcY.Text);
            Properties.Settings.Default.W_B = int.Parse(txt_BcW.Text);
            Properties.Settings.Default.H_B = int.Parse(txt_BcH.Text);
            Properties.Settings.Default.Save();
            int x = GetInt(txt_BcX.Text, 550);
            int y = GetInt(txt_BcY.Text, 200);
            int w = GetInt(txt_BcW.Text, 150);
            int h = GetInt(txt_BcH.Text, 650);
            _barcodeRoi = new CvRect(x, y, w, h);

            MessageBox.Show("Barcode ROI saved.");
        }

        private void btn_BarcodeTest_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string? barcodeText = BarcodeReading();

                using Mat resultImage = BarcodeReadingShow();
                ShowMatOnImage(resultImage, imgCameraResult);

                if (barcodeText == null)
                {
                    txt_BcResult.Text = "No barcode detected";
                }
                else
                {
                    txt_BcResult.Text = barcodeText;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Barcode test error: " + ex.Message);
            }
        }

        private string? BarcodeReading()
        {
            Mat? captured = null;

            try
            {
                lock (_frameLock)
                {
                    if (_latestFrame == null || _latestFrame.Empty())
                    {
                        MessageBox.Show("No camera frame available.");
                        return null;
                    }

                    captured = _latestFrame.Clone();
                }

                CvRect safeRoi = MakeSafeRect(_barcodeRoi, captured.Width, captured.Height);

                if (safeRoi.Width <= 0 || safeRoi.Height <= 0)
                {
                    MessageBox.Show("Invalid barcode ROI.");
                    return null;
                }

                _barcodeCaptureFrame?.Dispose();
                _barcodeCaptureFrame = captured.Clone();

                CvRect paddedRoi = ExpandRect(safeRoi, 20, captured.Width, captured.Height);

                using Mat barcodeCrop = new Mat(captured, paddedRoi).Clone();

                string? decodedText = DecodeBarcodeFromMat(barcodeCrop);

                _lastBarcodeText = decodedText;

                txt_BcResult.Text = decodedText ?? "No barcode detected";

                return decodedText;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Barcode reading error: " + ex.Message);
                return null;
            }
            finally
            {
                captured?.Dispose();
            }
        }

        private static CvRect ExpandRect(CvRect rect, int padding, int imageWidth, int imageHeight)
        {
            int x = Math.Max(0, rect.X - padding);
            int y = Math.Max(0, rect.Y - padding);

            int right = Math.Min(imageWidth, rect.X + rect.Width + padding);
            int bottom = Math.Min(imageHeight, rect.Y + rect.Height + padding);

            int w = right - x;
            int h = bottom - y;

            if (w <= 0 || h <= 0)
                return rect;

            return new CvRect(x, y, w, h);
        }
        private string? DecodeBarcodeFromMat(Mat barcodeMat)
        {
            try
            {
                if (barcodeMat == null || barcodeMat.Empty())
                    return null;

                using Mat gray = new Mat();

                if (barcodeMat.Channels() == 1)
                    barcodeMat.CopyTo(gray);
                else
                    Cv2.CvtColor(barcodeMat, gray, ColorConversionCodes.BGR2GRAY);

                // Make ROI bigger. ZXing usually works better with larger barcode area.
                using Mat resized = new Mat();
                double scale = 2.5;

                Cv2.Resize(
                    gray,
                    resized,
                    new CvSize(),
                    scale,
                    scale,
                    InterpolationFlags.Cubic
                );

                List<Mat> candidates = BuildBarcodeCandidates(resized);

                var reader = new BarcodeReaderGeneric
                {
                    AutoRotate = true,
                    Options = new DecodingOptions
                    {
                        TryHarder = true,
                        TryInverted = true,
                        PureBarcode = false,
                        PossibleFormats = new List<BarcodeFormat>
                {
                    BarcodeFormat.CODE_128,
                    //BarcodeFormat.CODE_39,
                    //BarcodeFormat.EAN_13,
                    //BarcodeFormat.EAN_8,
                    //BarcodeFormat.QR_CODE,
                    //BarcodeFormat.DATA_MATRIX
                }
                    }
                };

                foreach (Mat candidate in candidates)
                {
                    string? decoded = TryDecodeCandidate(reader, candidate);

                    if (!string.IsNullOrWhiteSpace(decoded))
                    {
                        foreach (Mat m in candidates)
                            m.Dispose();

                        return decoded;
                    }
                }

                foreach (Mat m in candidates)
                    m.Dispose();

                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Barcode decode error: " + ex.Message);
                return null;
            }
        }

        private List<Mat> BuildBarcodeCandidates(Mat gray)
        {
            List<Mat> candidates = new List<Mat>();

            // 1. Original
            candidates.Add(gray.Clone());

            //// 2. Histogram equalization
            //Mat equalized = new Mat();
            //Cv2.EqualizeHist(gray, equalized);
            //candidates.Add(equalized);

            // 3. CLAHE
            Mat claheImg = new Mat();
            using (CLAHE clahe = Cv2.CreateCLAHE(
                clipLimit: 2.0,
                tileGridSize: new CvSize(8, 8)))
            {
                clahe.Apply(gray, claheImg);
            }
            candidates.Add(claheImg);

            //// 4. Gaussian blur + Otsu
            //Mat blur = new Mat();

            //Cv2.GaussianBlur(gray, blur, new CvSize(3, 3), 0);

            //Mat otsu = new Mat();
            //Cv2.Threshold(
            //    blur,
            //    otsu,
            //    0,
            //    255,
            //    ThresholdTypes.Binary | ThresholdTypes.Otsu
            //);
            //candidates.Add(otsu);

            //// 5. Otsu inverted
            //Mat otsuInv = new Mat();
            //Cv2.BitwiseNot(otsu, otsuInv);
            //candidates.Add(otsuInv);

            //// 6. Adaptive threshold
            //Mat adaptive = new Mat();
            //Cv2.AdaptiveThreshold(
            //    gray,
            //    adaptive,
            //    255,
            //    AdaptiveThresholdTypes.GaussianC,
            //    ThresholdTypes.Binary,
            //    31,
            //    5
            //);
            //candidates.Add(adaptive);

            //// 7. Adaptive inverted
            //Mat adaptiveInv = new Mat();
            //Cv2.BitwiseNot(adaptive, adaptiveInv);
            //candidates.Add(adaptiveInv);

            //// 8. Sharpened
            //Mat sharpened = new Mat();
            //using (Mat blurSharp = new Mat())
            //{
            //    Cv2.GaussianBlur(gray, blurSharp, new CvSize(0, 0), 1.2);
            //    Cv2.AddWeighted(gray, 1.8, blurSharp, -0.8, 0, sharpened);
            //}
            //candidates.Add(sharpened);

            //// 9. Sharpened + Otsu
            //Mat sharpOtsu = new Mat();
            //Cv2.Threshold(
            //    sharpened,
            //    sharpOtsu,
            //    0,
            //    255,
            //    ThresholdTypes.Binary | ThresholdTypes.Otsu
            //);
            //candidates.Add(sharpOtsu);

            //// 10. Morphological close for broken barcode stripes
            //Mat morphClose = new Mat();
            //using (Mat kernel = Cv2.GetStructuringElement(
            //    MorphShapes.Rect,
            //    new CvSize(3, 3)))
            //{
            //    Cv2.MorphologyEx(
            //        sharpOtsu,
            //        morphClose,
            //        MorphTypes.Close,
            //        kernel
            //    );
            //}
            //candidates.Add(morphClose);

            return candidates;
        }

        private string? TryDecodeCandidate(BarcodeReaderGeneric reader, Mat candidate)
        {
            try
            {
                if (candidate == null || candidate.Empty())
                    return null;

                using Mat gray = new Mat();

                if (candidate.Channels() == 1)
                    candidate.CopyTo(gray);
                else
                    Cv2.CvtColor(candidate, gray, ColorConversionCodes.BGR2GRAY);

                byte[] pixels = MatToGrayByteArray(gray);

                var luminanceSource = new RGBLuminanceSource(
                    pixels,
                    gray.Width,
                    gray.Height,
                    RGBLuminanceSource.BitmapFormat.Gray8
                );

                var result = reader.Decode(luminanceSource);

                if (result != null && !string.IsNullOrWhiteSpace(result.Text))
                    return result.Text;

                return null;
            }
            catch
            {
                return null;
            }
        }

        private byte[] MatToGrayByteArray(Mat mat)
        {
            if (mat.Type() != MatType.CV_8UC1)
                throw new ArgumentException("Mat must be CV_8UC1 grayscale image.");

            int width = mat.Width;
            int height = mat.Height;

            byte[] data = new byte[width * height];

            if (mat.IsContinuous())
            {
                Marshal.Copy(mat.Data, data, 0, data.Length);
            }
            else
            {
                for (int y = 0; y < height; y++)
                {
                    IntPtr rowPtr = mat.Ptr(y);
                    Marshal.Copy(rowPtr, data, y * width, width);
                }
            }

            return data;
        }

        private Mat BarcodeReadingShow()
        {
            Mat result;

            if (_barcodeCaptureFrame == null || _barcodeCaptureFrame.Empty())
                return CreateBlankResultImage("No barcode captured image");

            result = ToBgr(_barcodeCaptureFrame);

            CvRect? safe = MakeSafeRoi(_barcodeRoi, result.Width, result.Height);

            if (safe == null)
                return result;

            CvRect roi = safe.Value;

            Cv2.Rectangle(
                result,
                roi,
                new Scalar(255, 0, 0),
                3
            );

            string text = string.IsNullOrWhiteSpace(_lastBarcodeText)
                ? "No barcode detected"
                : _lastBarcodeText;

            Cv2.Rectangle(
                result,
                new CvRect(5, 5, Math.Min(650, result.Width - 10), 60),
                new Scalar(0, 0, 0),
                -1
            );

            Cv2.PutText(
                result,
                text,
                new CvPoint(15, 42),
                HersheyFonts.HersheySimplex,
                0.8,
                string.IsNullOrWhiteSpace(_lastBarcodeText)
                    ? new Scalar(0, 0, 255)
                    : new Scalar(0, 255, 0),
                2
            );

            return result;
        }

        //AUTO

        public void ClearTriggeredImages()
        {
            lock (_triggerImageLock)
            {
                foreach (Mat img in _triggeredImages)
                    img.Dispose();

                _triggeredImages.Clear();
            }
        }

        public string? barcode_reading_auto()
        {
            List<Mat> imagesToRead = new List<Mat>();
            Debug.WriteLine($"Chuẩn bị xử lý {_triggeredImages.Count}");
            //TrySetEnumParameter(_camera!, "ExposureAuto", "Off");


            try
            {
                // 1. Copy triggered images safely
                lock (_triggerImageLock)
                {
                    foreach (Mat img in _triggeredImages)
                    {
                        if (img != null && !img.Empty())
                        {
                            imagesToRead.Add(img.Clone());
                        }
                    }
                    _triggeredImages.Clear();
                }

                

                if (imagesToRead.Count == 0)
                {
                    Dispatcher.BeginInvoke(() =>
                    {
                        txt_BcResult.Text = "No triggered image";
                    });

                    return null;
                }

                // 2. Read barcode ROI from textbox
                // ... [Code Bước 1 phía trên giữ nguyên] ...

                // 2. Read barcode ROI from textbox (ĐÃ FIX CROSS-THREAD)
                int x = 100, y = 100, w = 300, h = 150;

                // Bắt buộc dùng Dispatcher.Invoke để nhờ UI Thread đọc dữ liệu Textbox an toàn
                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    x = GetInt(txt_BcX.Text, 100);
                    y = GetInt(txt_BcY.Text, 100);
                    w = GetInt(txt_BcW.Text, 300);
                    h = GetInt(txt_BcH.Text, 150);
                });

                CvRect barcodeRoi = new CvRect(x, y, w, h);

                // 3. Try reading barcode image by image
                // ... [Code Bước 3 phía dưới giữ nguyên] ...


                // 3. Try reading barcode image by image
                for (int i = 0; i < imagesToRead.Count; i++)
                {
                    Mat frame = imagesToRead[i];

                    if (frame == null || frame.Empty())
                        continue;

                    CvRect safeRoi = MakeSafeRect(
                        barcodeRoi,
                        frame.Width,
                        frame.Height
                    );

                    if (safeRoi.Width <= 0 || safeRoi.Height <= 0)
                        continue;

                    using Mat barcodeCrop = new Mat(frame, safeRoi).Clone();

                    string? barcodeText = DecodeBarcodeFromMat(barcodeCrop);

                    if (!string.IsNullOrWhiteSpace(barcodeText))
                    {
                        _lastBarcodeText = barcodeText;

                        _barcodeCaptureFrame?.Dispose();
                        _barcodeCaptureFrame = frame.Clone();

                        Dispatcher.BeginInvoke(() =>
                        {
                            txt_BcResult.Text = barcodeText;

                            using Mat resultImage = BarcodeReadingShow();
                            ShowMatOnImage(resultImage, imgCameraResult);
                        });

                        return barcodeText;
                    }
                }

                // 4. If all images failed
                _lastBarcodeText = null;

                if (imagesToRead.Count > 0)
                {
                    _barcodeCaptureFrame?.Dispose();
                    _barcodeCaptureFrame = imagesToRead[^1].Clone();
                }

                Dispatcher.BeginInvoke(() =>
                {
                    txt_BcResult.Text = "No barcode detected";

                    using Mat resultImage = BarcodeReadingShow();
                    ShowMatOnImage(resultImage, imgCameraResult);
                });

                return null;
            }
            catch (Exception ex)
            {
                Dispatcher.BeginInvoke(() =>
                {
                    MessageBox.Show("Auto barcode reading error: " + ex.Message);
                });

                return null;
            }
            finally
            {
                foreach (Mat img in imagesToRead)
                {
                    img.Dispose();
                }
            }
        }

        private static double TubeVolumeFromBottomMl(double heightMm)
        {
            if (double.IsNaN(heightMm) || heightMm <= 0)
                return 0.0;

            double r = TUBE_INNER_RADIUS_MM;
            double h = heightMm;

            double volumeMm3;

            // Rounded dome bottom, approximated as a hemisphere.
            // From bottom to h <= r: spherical cap volume.
            if (h <= r)
            {
                volumeMm3 = Math.PI * h * h * (r - h / 3.0);
            }
            else
            {
                double hemiVolumeMm3 = (2.0 / 3.0) * Math.PI * Math.Pow(r, 3);
                double cylVolumeMm3 = Math.PI * r * r * (h - r);

                volumeMm3 = hemiVolumeMm3 + cylVolumeMm3;
            }

            return volumeMm3 / 1000.0; // mm3 to mL
        }

        public (double? rbcHeight, double? plasmaHeight,
                    double? rbcVolumeMl, double? plasmaVolumeMl, double? totalVolumeMl,
                    string failReason) liquid_reading_auto()
        {
            Application.Current?.Dispatcher?.Invoke(() =>
            {
                ClearCameraDisplay();
            });

            List<Mat> imagesToRead = new List<Mat>();

            double bestConfidence = -1.0;
            bool bestSuccess = false;

            double? bestRbc = null;
            double? bestPlasma = null;
            string bestFailReason = "No triggered image";

            Mat? bestFrame = null;
            List<int> bestLevels = new List<int>();
            List<double> bestHeights = new List<double>();
            List<double> bestGradients = new List<double>();
            double[]? bestProfile = null;
            double[]? bestProfileNorm = null;
            Mat? bestRbcMask = null;
            Mat? bestLabelMask = null;

            bool bestHasLabel = false;
            double bestLabelScore = 0.0;
            double bestLabelOcclusion = 0.0;
            double bestBoundaryScore = 0.0;
            double bestProfileMax = 0.0;
            double bestProfileThreshold = 0.0;

            // --- BẢN VÁ: THÊM BIẾN NHỚ ĐIỂM YOLO CỦA TẤM ẢNH TỐT NHẤT ---
            double bestYoloRbcConf = 0.0;
            double bestYoloLabelConf = 0.0;

            int? bestRbcTop = null;
            int? bestRbcBottom = null;
            double bestRbcHeight = double.NaN;
            double bestPlasmaHeight = double.NaN;
            double bestTotalHeight = double.NaN;
            double bestRbcVolumeMl = double.NaN;
            double bestPlasmaVolumeMl = double.NaN;
            double bestTotalVolumeMl = double.NaN;

            CvRect bestRoi = _liquidRoi;

            const double ACCEPT_CONFIDENCE = 0.80;

            try
            {
                lock (_triggerImageLock)
                {
                    foreach (Mat img in _triggeredImages)
                    {
                        if (img != null && !img.Empty())
                            imagesToRead.Add(img.Clone());
                    }
                    _triggeredImages.Clear();
                }

                if (imagesToRead.Count == 0)
                {
                    Application.Current?.Dispatcher?.BeginInvoke(() =>
                    {
                        txt_FailReason.Text = "No triggered image";
                    });
                    return (null, null, null, null, null, "No triggered image");
                }

                for (int i = 0; i < imagesToRead.Count; i++)
                {
                    Mat frame = imagesToRead[i];
                    if (frame == null || frame.Empty())
                        continue;

                    bool success = MeasureLiquidFromFrame(frame);
                    var confidenceResult = CalculateLiquidConfidence();
                    double confidence = confidenceResult.confidence;

                    // --- BẢN VÁ: BẮT ĐÚNG LỖI THỰC SỰ THAY VÌ "No Profile" ẢO ---
                    string reason = success ? confidenceResult.reason : _liquidFailReason;

                    bool better = false;
                    if (success && !bestSuccess)
                        better = true;
                    else if (success == bestSuccess && confidence > bestConfidence)
                        better = true;

                    if (better)
                    {
                        bestConfidence = confidence;
                        bestSuccess = success;

                        bestRbc = success ? _rbcHeightValue : null;
                        bestPlasma = success ? _plasmaHeightValue : null;
                        bestFailReason = success ? "OK" : reason;

                        bestFrame?.Dispose();
                        bestFrame = frame.Clone();

                        bestLevels = new List<int>(_liquidLevels);
                        bestHeights = new List<double>(_liquidHeights);
                        bestGradients = new List<double>(_liquidLevelGradients);
                        bestProfile = _liquidProfile?.ToArray();
                        bestProfileNorm = _liquidProfileNorm?.ToArray();

                        bestRbcMask?.Dispose();
                        bestLabelMask?.Dispose();
                        bestRbcMask = _yoloRbcMask?.Clone();
                        bestLabelMask = _yoloLabelMask?.Clone();

                        bestHasLabel = _liquidHasLabel;
                        bestLabelScore = _labelScore;
                        bestLabelOcclusion = _labelOcclusion;
                        bestBoundaryScore = _boundaryScore;
                        bestProfileMax = _liquidProfileMax;
                        bestProfileThreshold = _liquidProfileThreshold;
                        bestRbcTop = _rbcTopInRoi;
                        bestRbcBottom = _rbcBottomInRoi;
                        bestRbcHeight = _rbcHeightValue;
                        bestPlasmaHeight = _plasmaHeightValue;
                        bestTotalHeight = _totalLiquidHeightValue;

                        // LƯU LẠI ĐIỂM YOLO CỦA ẢNH TỐT NHẤT NÀY
                        bestYoloRbcConf = _yoloRbcConf;
                        bestYoloLabelConf = _yoloLabelConf;

                        bestRbcVolumeMl = double.NaN;
                        bestPlasmaVolumeMl = double.NaN;
                        bestTotalVolumeMl = double.NaN;

                        if (success && _pixelToMm.HasValue &&
                            !double.IsNaN(bestRbcHeight) &&
                            !double.IsNaN(bestPlasmaHeight))
                        {
                            double rbcHeightMm = bestRbcHeight;
                            double totalHeightMm;

                            if (!double.IsNaN(bestTotalHeight) && bestTotalHeight > 0)
                                totalHeightMm = bestTotalHeight;
                            else
                                totalHeightMm = bestRbcHeight + bestPlasmaHeight;

                            bestRbcVolumeMl = TubeVolumeFromBottomMl(rbcHeightMm);
                            bestTotalVolumeMl = TubeVolumeFromBottomMl(totalHeightMm);
                            bestPlasmaVolumeMl = Math.Max(0.0, bestTotalVolumeMl - bestRbcVolumeMl);
                        }

                        bestRoi = _liquidRoi;
                    }
                }

                if (bestConfidence < 0)
                {
                    Application.Current?.Dispatcher?.BeginInvoke(() =>
                    {
                        txt_FailReason.Text = "No valid image checked";
                    });
                    return (null, null, null, null, null, "No valid image checked");
                }

                // Restore best result for UI display
                if (bestFrame != null)
                {
                    _liquidCaptureFrame?.Dispose();
                    _liquidCaptureFrame = bestFrame.Clone();
                }

                _liquidRoi = bestRoi;
                _liquidLevels.Clear(); _liquidLevels.AddRange(bestLevels);
                _liquidHeights.Clear(); _liquidHeights.AddRange(bestHeights);
                _liquidLevelGradients.Clear(); _liquidLevelGradients.AddRange(bestGradients);
                _liquidProfile = bestProfile ?? Array.Empty<double>();
                _liquidProfileNorm = bestProfileNorm ?? Array.Empty<double>();

                _yoloRbcMask?.Dispose();
                _yoloLabelMask?.Dispose();
                _yoloRbcMask = bestRbcMask?.Clone();
                _yoloLabelMask = bestLabelMask?.Clone();

                _liquidHasLabel = bestHasLabel;
                _labelScore = bestLabelScore;
                _labelOcclusion = bestLabelOcclusion;
                _boundaryScore = bestBoundaryScore;
                _liquidProfileMax = bestProfileMax;
                _liquidProfileThreshold = bestProfileThreshold;
                _rbcTopInRoi = bestRbcTop;
                _rbcBottomInRoi = bestRbcBottom;
                _rbcHeightValue = bestRbcHeight;
                _plasmaHeightValue = bestPlasmaHeight;
                _totalLiquidHeightValue = bestTotalHeight;
                _rbcVolumeMl = bestRbcVolumeMl;
                _plasmaVolumeMl = bestPlasmaVolumeMl;
                _totalVolumeMl = bestTotalVolumeMl;
                _liquidConfidence = Math.Max(0.0, bestConfidence);

                // PHỤC HỒI ĐIỂM YOLO ĐỂ IN RA UI
                _yoloRbcConf = bestYoloRbcConf;
                _yoloLabelConf = bestYoloLabelConf;
                _liquidFailReason = bestFailReason; // Trả lại lý do lỗi gốc

                double? rbcVol = double.IsNaN(_rbcVolumeMl) ? null : _rbcVolumeMl;
                double? plasmaVol = double.IsNaN(_plasmaVolumeMl) ? null : _plasmaVolumeMl;
                double? totalVol = double.IsNaN(_totalVolumeMl) ? null : _totalVolumeMl;

                bool accepted = bestSuccess &&
                                bestRbc.HasValue &&
                                bestPlasma.HasValue &&
                                bestConfidence >= ACCEPT_CONFIDENCE;

                if (accepted)
                {
                    _liquidFailReason = "OK";
                    string unit = _pixelToMm.HasValue ? " mm" : " px";

                    Application.Current?.Dispatcher?.BeginInvoke(() =>
                    {
                        txt_LabelScore.Text = _labelOcclusion.ToString("0.000");
                        txt_ConfidenceScore.Text = _liquidConfidence.ToString("0.000");
                        txt_FailReason.Text = "OK";

                        if (_pixelToMm.HasValue &&
                            !double.IsNaN(_rbcVolumeMl) &&
                            !double.IsNaN(_plasmaVolumeMl))
                        {
                            txt_LqPlasma.Text = $"{bestPlasma!.Value:0.00}{unit} | {_plasmaVolumeMl:0.000} mL";
                            txt_LqRbc.Text = $"{bestRbc!.Value:0.00}{unit} | {_rbcVolumeMl:0.000} mL";
                            txt_FailReason.Text = $"OK | Total volume = {_totalVolumeMl:0.000} mL";
                        }
                        else
                        {
                            txt_LqPlasma.Text = bestPlasma!.Value.ToString("0.00") + unit;
                            txt_LqRbc.Text = bestRbc!.Value.ToString("0.00") + unit;
                            txt_FailReason.Text = "OK | Volume not calculated because scale is missing.";
                        }

                        using Mat resultImage = MeasureLiquidShow();
                        ShowMatOnImage(resultImage, imgCameraResult);
                        using Mat graph = CreateGradientGraph();
                        ShowMatOnImage(graph, imgLiquidProfileGraph);
                    });

                    return (bestRbc, bestPlasma, rbcVol, plasmaVol, totalVol, "OK");
                }
                else
                {
                    string failReason = !bestSuccess
                        ? $"Best image failed. Confidence={bestConfidence:0.000}. Reason: {bestFailReason}"
                        : $"Best image confidence too low. Confidence={bestConfidence:0.000}, required >= {ACCEPT_CONFIDENCE:0.00}";

                    _liquidFailReason = failReason;

                    Application.Current?.Dispatcher?.BeginInvoke(() =>
                    {
                        txt_LabelScore.Text = _labelOcclusion.ToString("0.000");
                        txt_ConfidenceScore.Text = _liquidConfidence.ToString("0.000");
                        txt_FailReason.Text = failReason;
                        txt_LqPlasma.Text = "No level";
                        txt_LqRbc.Text = "No level";

                        using Mat resultImage = MeasureLiquidShow();
                        ShowMatOnImage(resultImage, imgCameraResult);
                        using Mat graph = CreateGradientGraph();
                        ShowMatOnImage(graph, imgLiquidProfileGraph);
                    });

                    return (null, null, null, null, null, failReason);
                }
            }
            catch (Exception ex)
            {
                string err = "Auto liquid reading error: " + ex.Message;
                Application.Current?.Dispatcher?.BeginInvoke(() =>
                {
                    txt_FailReason.Text = err;
                });
                return (null, null, null, null, null, err);
            }
            finally
            {
                foreach (Mat img in imagesToRead)
                    img.Dispose();

                bestFrame?.Dispose();
                bestRbcMask?.Dispose();
                bestLabelMask?.Dispose();
            }
        }

        private bool MeasureLiquidFromFrame(Mat inputFrame)
        {
            Mat? captured = null;

            try
            {
                if (inputFrame == null || inputFrame.Empty())
                {
                    _liquidFailReason = "Empty input image";
                    return false;
                }

                captured = inputFrame.Clone();
                return MeasureLiquidCore(captured);
            }
            catch (Exception ex)
            {
                _liquidFailReason = "Measure error: " + ex.Message;
                return false;
            }
            finally
            {
                captured?.Dispose();
            }
        }

        private bool EnsureYoloSegLoaded()
        {
            if (_yoloSegSession != null)
                return true;

            if (!File.Exists(_onnxModelPath))
            {
                MessageBox.Show(
                    "YOLO segmentation ONNX model not found:\n" + _onnxModelPath +
                    "\n\nPut yolo11_tube.onnx in SCADA_VERTEX/Models and set Copy if newer.",
                    "ONNX Model Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
                return false;
            }

            try
            {
                var options = new SessionOptions();
                // CPU is safest. If you use GPU, install Microsoft.ML.OnnxRuntime.Gpu and append CUDA provider.
                _yoloSegSession = new InferenceSession(_onnxModelPath, options);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Cannot load ONNX model:\n" + ex.Message);
                _yoloSegSession = null;
                return false;
            }
        }

        private bool MeasureLiquidCore(Mat inputFrame)
        {
            Mat? working = null;

            try
            {
                ResetLiquidState();

                if (!EnsureYoloSegLoaded())
                {
                    _liquidFailReason = "YOLO segmentation model not loaded";
                    return false;
                }

                working = ToBgr(inputFrame);
                int imgW = working.Width;
                int imgH = working.Height;

                // =====================================================
                // 1. YOLO-Seg inference: get RBC mask and label mask
                // =====================================================
                YoloSegResult seg;
                lock (_yoloSegLock)
                {
                    seg = RunYoloSegmentation(working);
                }

                _yoloRbcMask?.Dispose();
                _yoloLabelMask?.Dispose();
                _yoloRbcMask = seg.RbcMask.Clone();
                _yoloLabelMask = seg.LabelMask.Clone();
                _yoloRbcConf = seg.RbcConf;
                _yoloLabelConf = seg.LabelConf;

                if (_yoloRbcMask == null || _yoloRbcMask.Empty() || Cv2.CountNonZero(_yoloRbcMask) <= 0)
                {
                    _liquidFailReason = "No RBC/dark liquid detected";
                    return false;
                }

                // =====================================================
                // 2. Tilt correction from elongated mask, same idea as Python
                // =====================================================
                ApplyTiltCorrectionIfUseful(ref working, ref _yoloRbcMask, ref _yoloLabelMask);

                // =====================================================
                // 3. Auto-generate measurement ROI from RBC mask
                // =====================================================
                CvRect autoRoi = GenerateRoiFromRbcMask(_yoloRbcMask!, working.Width, working.Height);
                CvRect liquidSafeRoi = MakeSafeRect(autoRoi, working.Width, working.Height);

                if (liquidSafeRoi.Width <= 0 || liquidSafeRoi.Height <= 0)
                {
                    _liquidFailReason = "Invalid auto ROI";
                    return false;
                }

                _liquidRoi = liquidSafeRoi;

                _liquidCaptureFrame?.Dispose();
                _liquidCaptureFrame = working.Clone();

                // =====================================================
                // 4. Label occlusion
                //    txt_LabelThresh = label occlusion threshold
                //    txt_LabelScore = measured label occlusion
                // =====================================================
                using Mat roiBool = new Mat(working.Height, working.Width, MatType.CV_8UC1, Scalar.All(0));
                Cv2.Rectangle(roiBool, liquidSafeRoi, Scalar.All(255), -1);

                double roiArea = Math.Max(1.0, liquidSafeRoi.Width * liquidSafeRoi.Height);
                double yoloLabelOverlap = 0.0;

                if (_yoloLabelMask != null && !_yoloLabelMask.Empty())
                {
                    using Mat labelInRoi = new Mat();
                    Cv2.BitwiseAnd(_yoloLabelMask, roiBool, labelInRoi);
                    yoloLabelOverlap = Cv2.CountNonZero(labelInRoi) / roiArea;
                }

                using Mat grayForLabel = new Mat();
                if (working.Channels() == 1)
                    working.CopyTo(grayForLabel);
                else
                    Cv2.CvtColor(working, grayForLabel, ColorConversionCodes.BGR2GRAY);

                using Mat labelRoiImg = new Mat(grayForLabel, liquidSafeRoi);
                var classicalLabel = DetectLabel(labelRoiImg);

                _labelOverlap = yoloLabelOverlap;
                _labelOcclusion = Math.Max(yoloLabelOverlap, classicalLabel.labelScore);
                _labelScore = _labelOcclusion;
                _boundaryScore = classicalLabel.boundaryScore;

                 double labelOccThreshold = GetDoubleSafe(txt_LabelThresh, 0.10);
                _liquidHasLabel = _labelOcclusion > labelOccThreshold;

                if (_liquidHasLabel)
                {
                    _liquidFailReason = "Label occlusion";
                    return false;
                }

                // =====================================================
                // 5. Build Sobel-Y profile in center strip of ROI
                // =====================================================
                using Mat gray = new Mat();
                if (working.Channels() == 1)
                    working.CopyTo(gray);
                else
                    Cv2.CvtColor(working, gray, ColorConversionCodes.BGR2GRAY);

                using Mat roiImg = new Mat(gray, liquidSafeRoi);
                int roiH = roiImg.Rows;
                int roiW = roiImg.Cols;

                if (roiW < 5 || roiH < 10)
                {
                    _liquidFailReason = "ROI too small";
                    return false;
                }

                int cx1 = (int)(roiW * 0.30);
                int cx2 = (int)(roiW * 0.70);
                cx1 = Math.Clamp(cx1, 0, roiW - 1);
                cx2 = Math.Clamp(cx2, cx1 + 1, roiW);

                using Mat centerRoi = new Mat(roiImg, new CvRect(cx1, 0, cx2 - cx1, roiH));
                using Mat enhanced = new Mat();
                using (CLAHE clahe = Cv2.CreateCLAHE(clipLimit: 2.0, tileGridSize: new CvSize(8, 8)))
                {
                    clahe.Apply(centerRoi, enhanced);
                }

                using Mat blur = new Mat();
                Cv2.GaussianBlur(enhanced, blur, new CvSize(5, 5), 0);

                using Mat sobel64 = new Mat();
                Cv2.Sobel(blur, sobel64, MatType.CV_64F, 0, 1, ksize: 3);

                using Mat sobelAbs = new Mat();
                Cv2.Absdiff(sobel64, Scalar.All(0), sobelAbs);

                double[] profile = RowMean(sobelAbs);
                profile = SmoothProfile(profile, kernelSize: 21);

                _liquidProfile = profile;
                _liquidProfileMax = profile.Length > 0 ? profile.Max() : 0.0;
                _liquidProfileNorm = _liquidProfileMax > 0
                    ? profile.Select(v => v / _liquidProfileMax).ToArray()
                    : profile.ToArray();

                //if (profile.Length == 0)
                //{
                //    _liquidFailReason = "No profile";
                //    return false;
                //}

                double rawThreshold = Math.Max(0.0, GetDoubleSafe(txt_ProfileThresh, 20.0));
                _liquidProfileThreshold = rawThreshold;

                // =====================================================
                // 6. RBC top/bottom from RBC mask inside ROI
                //    Level 1 = RBC/plasma interface = RBC top
                // =====================================================
                using Mat rbcRoi = new Mat(_yoloRbcMask!, liquidSafeRoi);
                CvPoint[] rbcPts = GetNonZeroPoints(rbcRoi);

                if (rbcPts == null || rbcPts.Length < 20)
                {
                    _liquidFailReason = "RBC mask too small inside ROI";
                    return false;
                }

                int[] rbcYs = rbcPts.Select(p => p.Y).OrderBy(v => v).ToArray();
                int rbcTop = PercentileInt(rbcYs, 2.0);
                int rbcBottom = PercentileInt(rbcYs, 98.0);
                rbcTop = Math.Clamp(rbcTop, 0, roiH - 1);
                rbcBottom = Math.Clamp(rbcBottom, 0, roiH - 1);

                if (rbcBottom <= rbcTop + 10)
                {
                    _liquidFailReason = "Invalid RBC top/bottom from mask";
                    return false;
                }

                _rbcTopInRoi = rbcTop;
                _rbcBottomInRoi = rbcBottom;
                _rbcBottomGlobal = liquidSafeRoi.Y + rbcBottom;

                int level1 = rbcTop;
                double level1Grad = (level1 >= 0 && level1 < profile.Length) ? profile[level1] : 0.0;

                // =====================================================
                // 7. Level 2 = upper plasma boundary from Sobel-Y profile
                //    Search only ABOVE RBC top. If no valid peak: reject.
                // =====================================================
                int searchMargin = 20;
                int searchEnd = Math.Max(5, rbcTop - searchMargin);

                int level2 = FindBestUpperBoundaryPeak(
                    profile,
                    rawThreshold,
                    searchStart: 0,
                    searchEndExclusive: searchEnd,
                    minProminenceRatio: 0.35
                );

                double level2Grad = (level2 >= 0 && level2 < profile.Length) ? profile[level2] : 0.0;

                if (level2 < 0)
                {
                    _liquidLevels.Clear();
                    _liquidLevelGradients.Clear();
                    _liquidLevels.Add(level1);
                    _liquidLevelGradients.Add(level1Grad);
                    _liquidFailReason = "Only one liquid level detected";
                    return false;
                }

                if (level2Grad < rawThreshold)
                {
                    _liquidLevels.Clear();
                    _liquidLevelGradients.Clear();
                    _liquidLevels.Add(level1);
                    _liquidLevelGradients.Add(level1Grad);
                    _liquidFailReason = "Weak plasma boundary";
                    return false;
                }

                if (level2 >= level1)
                {
                    _liquidLevels.Clear();
                    _liquidLevelGradients.Clear();
                    _liquidLevels.Add(level1);
                    _liquidLevels.Add(level2);
                    _liquidLevelGradients.Add(level1Grad);
                    _liquidLevelGradients.Add(level2Grad);
                    _liquidFailReason = "Invalid plasma boundary: below RBC interface";
                    return false;
                }

                // False upper edge / clamp reflection rejection.
                double minPlasmaYRatio = 0.25;
                int minValidPlasmaY = (int)(roiH * minPlasmaYRatio);

                //if (level2 < minValidPlasmaY)
                //{
                //    _liquidLevels.Clear();
                //    _liquidLevelGradients.Clear();
                //    _liquidLevels.Add(level1);
                //    _liquidLevels.Add(level2);
                //    _liquidLevelGradients.Add(level1Grad);
                //    _liquidLevelGradients.Add(level2Grad);
                //    _liquidFailReason = "Invalid plasma boundary: false upper edge";
                //    return false;
                //}

                double rbcHeightPx = Math.Max(0, rbcBottom - level1);
                double plasmaHeightPx = Math.Max(0, level1 - level2);
                double totalHeightPx = Math.Max(1, rbcBottom - level2);
                double rbcFraction = rbcHeightPx / Math.Max(1.0, totalHeightPx);

                //double minRbcFraction = 0.40;
                //if (rbcFraction < minRbcFraction)
                //{
                //    _liquidLevels.Clear();
                //    _liquidLevelGradients.Clear();
                //    _liquidLevels.Add(level1);
                //    _liquidLevels.Add(level2);
                //    _liquidLevelGradients.Add(level1Grad);
                //    _liquidLevelGradients.Add(level2Grad);
                //    _liquidFailReason = "Invalid plasma boundary: unreasonable RBC/liquid ratio";
                //    return false;
                //}

                // =====================================================
                // 8. Store levels and heights
                // Semantic order:
                //   _liquidLevels[0] = Level 1 RBC/plasma interface
                //   _liquidLevels[1] = Level 2 plasma upper boundary
                //   txt_LqRbc     = RBC height
                //   txt_LqPlasma  = plasma height
                // =====================================================
                _liquidLevels.Clear();
                _liquidLevelGradients.Clear();
                _liquidHeights.Clear();

                _liquidLevels.Add(level1);
                _liquidLevelGradients.Add(level1Grad);

                _liquidLevels.Add(level2);
                _liquidLevelGradients.Add(level2Grad);

                double scale = GetDoubleSafe(txt_LqScale, 0.0);
                _pixelToMm = scale > 0 ? scale : null;

                _rbcHeightValue = _pixelToMm.HasValue ? rbcHeightPx * _pixelToMm.Value : rbcHeightPx;
                _plasmaHeightValue = _pixelToMm.HasValue ? plasmaHeightPx * _pixelToMm.Value : plasmaHeightPx;
                _totalLiquidHeightValue = _pixelToMm.HasValue ? totalHeightPx * _pixelToMm.Value : totalHeightPx;

                _liquidHeights.Add(_plasmaHeightValue);
                _liquidHeights.Add(_rbcHeightValue);

                _boundaryScore = Math.Min(1.0, Math.Max(Math.Max(level1Grad, level2Grad), _liquidProfileMax) / Math.Max(rawThreshold * 2.0, 1e-6));

                _liquidFailReason = "OK";
                return true;
            }
            catch (Exception ex)
            {
                _liquidFailReason = "Measure error: " + ex.Message;
                return false;
            }
            finally
            {
                working?.Dispose();
            }
        }

        private void ResetLiquidState()
        {
            _liquidLevels.Clear();
            _liquidHeights.Clear();
            _liquidLevelGradients.Clear();

            _liquidHasLabel = false;
            _labelScore = 0.0;
            _labelOcclusion = 0.0;
            _labelOverlap = 0.0;
            _boundaryScore = 0.0;

            _liquidProfile = Array.Empty<double>();
            _liquidProfileNorm = Array.Empty<double>();
            _liquidProfileMax = 0.0;
            _liquidProfileThreshold = GetDoubleSafe(txt_ProfileThresh, 20.0);

            _liquidConfidence = 0.0;
            _liquidFailReason = "";

            _rbcTopInRoi = null;
            _rbcBottomInRoi = null;
            _rbcBottomGlobal = null;

            _rbcHeightValue = double.NaN;
            _plasmaHeightValue = double.NaN;
            _totalLiquidHeightValue = double.NaN;

            _yoloRbcConf = 0.0;
            _yoloLabelConf = 0.0;

            _tiltCorrectionApplied = false;
            _tiltAngleBefore = 0.0;
            _tiltAngleAfter = 0.0;
        }



        private YoloSegResult RunYoloSegmentation(Mat bgrInput)
        {
            if (_yoloSegSession == null)
                throw new InvalidOperationException("YOLO segmentation session is null.");

            int origW = bgrInput.Width;
            int origH = bgrInput.Height;

            using Mat input640 = Letterbox(bgrInput, YOLO_INPUT_SIZE, out LetterboxInfo lb);
            using Mat rgb = new Mat();
            Cv2.CvtColor(input640, rgb, ColorConversionCodes.BGR2RGB);

            var tensor = new DenseTensor<float>(new[] { 1, 3, YOLO_INPUT_SIZE, YOLO_INPUT_SIZE });

            for (int y = 0; y < YOLO_INPUT_SIZE; y++)
            {
                for (int x = 0; x < YOLO_INPUT_SIZE; x++)
                {
                    Vec3b c = rgb.At<Vec3b>(y, x);
                    tensor[0, 0, y, x] = c.Item0 / 255.0f;
                    tensor[0, 1, y, x] = c.Item1 / 255.0f;
                    tensor[0, 2, y, x] = c.Item2 / 255.0f;
                }
            }

            string inputName = _yoloSegSession.InputMetadata.Keys.First();
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(inputName, tensor)
            };

            using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> outputs = _yoloSegSession.Run(inputs);

            var outputList = outputs.ToList();
            if (outputList.Count < 2)
                throw new Exception("YOLO segmentation ONNX must have 2 outputs: predictions and prototypes.");

            Tensor<float> predTensor = outputList[0].AsTensor<float>();
            Tensor<float> protoTensor = outputList[1].AsTensor<float>();

            Dictionary<int, string> classNames = ReadClassNamesFromMetadata(_yoloSegSession);
            List<YoloSegDetection> detections = DecodeYoloSegPredictions(
                predTensor,
                classNames,
                origW,
                origH,
                lb
            );

            List<YoloSegDetection> kept = NonMaxSuppression(detections, YOLO_NMS_THRESHOLD);

            Mat rbcMask = new Mat(origH, origW, MatType.CV_8UC1, Scalar.All(0));
            Mat labelMask = new Mat(origH, origW, MatType.CV_8UC1, Scalar.All(0));

            double rbcConf = 0.0;
            double labelConf = 0.0;

            bool foundRbc = false;
            bool foundLabel = false;

            foreach (var det in kept)
            {
                // If it's a label and we haven't found our best label yet
                if (!foundLabel && IsLabelClass(det.ClassId, det.ClassName, classNames))
                {
                    using Mat instanceMask = BuildInstanceMask(protoTensor, det.MaskCoeffs, det.Box, origW, origH, lb);
                    instanceMask.CopyTo(labelMask); // Copy directly, NO BitwiseOr!
                    labelConf = det.Confidence;
                    foundLabel = true;
                }
                // If it's an RBC liquid and we haven't found our best RBC yet
                else if (!foundRbc && IsRbcClass(det.ClassId, det.ClassName, classNames))
                {
                    using Mat instanceMask = BuildInstanceMask(protoTensor, det.MaskCoeffs, det.Box, origW, origH, lb);
                    instanceMask.CopyTo(rbcMask);   // Copy directly, NO BitwiseOr!
                    rbcConf = det.Confidence;
                    foundRbc = true;
                }

                // If we already have the best of both, stop wasting time processing the rest
                if (foundRbc && foundLabel)
                {
                    break;
                }
            }

            return new YoloSegResult
            {
                RbcMask = rbcMask,
                LabelMask = labelMask,
                RbcConf = rbcConf,
                LabelConf = labelConf,
                Detections = kept
            };
        }

        private Mat Letterbox(Mat bgr, int size, out LetterboxInfo info)
        {
            int w = bgr.Width;
            int h = bgr.Height;
            double scale = Math.Min(size / (double)w, size / (double)h);

            int newW = (int)Math.Round(w * scale);
            int newH = (int)Math.Round(h * scale);
            int padX = (size - newW) / 2;
            int padY = (size - newH) / 2;

            Mat resized = new Mat();
            Cv2.Resize(bgr, resized, new CvSize(newW, newH));

            Mat output = new Mat(size, size, MatType.CV_8UC3, new Scalar(114, 114, 114));
            using Mat roi = new Mat(output, new CvRect(padX, padY, newW, newH));
            resized.CopyTo(roi);
            resized.Dispose();

            info = new LetterboxInfo(scale, newW, newH, padX, padY);
            return output;
        }

        private Dictionary<int, string> ReadClassNamesFromMetadata(InferenceSession session)
        {
            Dictionary<int, string> names = new();

            try
            {
                var meta = session.ModelMetadata.CustomMetadataMap;
                if (meta != null && meta.TryGetValue("names", out string? namesText) && !string.IsNullOrWhiteSpace(namesText))
                {
                    // Supports Ultralytics metadata like: {0: 'rbc', 1: 'label'}
                    MatchCollection matches = Regex.Matches(namesText, "(\\d+)\\s*:\\s*[\'\\\"]([^\'\\\"]+)[\'\\\"]");
                    foreach (Match m in matches)
                    {
                        int id = int.Parse(m.Groups[1].Value);
                        string name = m.Groups[2].Value;
                        names[id] = name;
                    }
                }
            }
            catch
            {
                // Metadata is optional.
            }

            return names;
        }

        private List<YoloSegDetection> DecodeYoloSegPredictions(
            Tensor<float> pred,
            Dictionary<int, string> classNames,
            int origW,
            int origH,
            LetterboxInfo lb)
        {
            int rank = pred.Dimensions.Length;
            if (rank != 3)
                throw new Exception("Unsupported YOLO prediction tensor shape. Expected rank 3.");

            int d1 = pred.Dimensions[1];
            int d2 = pred.Dimensions[2];

            // YOLO export commonly gives [1, channels, numPred]. Some exports give [1, numPred, channels].
            bool channelsFirst = d1 < d2;
            int channels = channelsFirst ? d1 : d2;
            int numPred = channelsFirst ? d2 : d1;

            int numClasses;
            if (classNames.Count > 0)
                numClasses = classNames.Count;
            else
                numClasses = Math.Max(1, channels - 4 - 32);

            int maskDim = channels - 4 - numClasses;
            if (maskDim <= 0)
                maskDim = 32;

            List<YoloSegDetection> detections = new();

            for (int i = 0; i < numPred; i++)
            {
                float Get(int ch)
                {
                    return channelsFirst ? pred[0, ch, i] : pred[0, i, ch];
                }

                float cx = Get(0);
                float cy = Get(1);
                float bw = Get(2);
                float bh = Get(3);

                int bestClass = 0;
                double bestScore = 0.0;

                for (int c = 0; c < numClasses; c++)
                {
                    double score = Get(4 + c);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestClass = c;
                    }
                }

                if (bestScore < YOLO_CONF_THRESHOLD)
                    continue;

                double x1 = (cx - bw / 2.0 - lb.PadX) / lb.Scale;
                double y1 = (cy - bh / 2.0 - lb.PadY) / lb.Scale;
                double x2 = (cx + bw / 2.0 - lb.PadX) / lb.Scale;
                double y2 = (cy + bh / 2.0 - lb.PadY) / lb.Scale;

                int bx1 = Math.Clamp((int)Math.Round(x1), 0, origW - 1);
                int by1 = Math.Clamp((int)Math.Round(y1), 0, origH - 1);
                int bx2 = Math.Clamp((int)Math.Round(x2), 0, origW - 1);
                int by2 = Math.Clamp((int)Math.Round(y2), 0, origH - 1);

                int boxW = Math.Max(1, bx2 - bx1 + 1);
                int boxH = Math.Max(1, by2 - by1 + 1);

                float[] coeffs = new float[maskDim];
                for (int k = 0; k < maskDim; k++)
                {
                    int ch = 4 + numClasses + k;
                    if (ch < channels)
                        coeffs[k] = Get(ch);
                }

                string className = classNames.TryGetValue(bestClass, out string? n) ? n : $"class_{bestClass}";

                detections.Add(new YoloSegDetection
                {
                    ClassId = bestClass,
                    ClassName = className,
                    Confidence = bestScore,
                    Box = new CvRect(bx1, by1, boxW, boxH),
                    MaskCoeffs = coeffs
                });
            }

            return detections;
        }

        private Mat BuildInstanceMask(
            Tensor<float> proto,
            float[] coeffs,
            CvRect box,
            int origW,
            int origH,
            LetterboxInfo lb)
        {
            int rank = proto.Dimensions.Length;
            if (rank != 4)
                throw new Exception("Unsupported YOLO mask prototype tensor shape. Expected rank 4.");

            int maskDim = proto.Dimensions[1];
            int maskH = proto.Dimensions[2];
            int maskW = proto.Dimensions[3];

            int usedDim = Math.Min(maskDim, coeffs.Length);

            Mat maskSmall = new Mat(maskH, maskW, MatType.CV_32FC1, Scalar.All(0));

            for (int y = 0; y < maskH; y++)
            {
                for (int x = 0; x < maskW; x++)
                {
                    double v = 0.0;
                    for (int k = 0; k < usedDim; k++)
                        v += coeffs[k] * proto[0, k, y, x];

                    float sig = (float)(1.0 / (1.0 + Math.Exp(-v)));
                    maskSmall.Set(y, x, sig);
                }
            }

            Mat mask640 = new Mat();
            Cv2.Resize(maskSmall, mask640, new CvSize(YOLO_INPUT_SIZE, YOLO_INPUT_SIZE), 0, 0, InterpolationFlags.Linear);
            maskSmall.Dispose();

            CvRect unpad = new CvRect(lb.PadX, lb.PadY, lb.NewW, lb.NewH);
            unpad = MakeSafeRect(unpad, YOLO_INPUT_SIZE, YOLO_INPUT_SIZE);

            using Mat unpadded = new Mat(mask640, unpad).Clone();
            mask640.Dispose();

            Mat maskOrigFloat = new Mat();
            Cv2.Resize(unpadded, maskOrigFloat, new CvSize(origW, origH), 0, 0, InterpolationFlags.Linear);

            Mat maskBinary = new Mat();
            Cv2.Threshold(maskOrigFloat, maskBinary, YOLO_MASK_THRESHOLD, 255, ThresholdTypes.Binary);
            maskOrigFloat.Dispose();
            maskBinary.ConvertTo(maskBinary, MatType.CV_8UC1);

            // Crop mask by detected box, same idea as YOLO segmentation postprocess.
            Mat finalMask = new Mat(origH, origW, MatType.CV_8UC1, Scalar.All(0));
            CvRect safeBox = MakeSafeRect(box, origW, origH);
            if (safeBox.Width > 0 && safeBox.Height > 0)
            {
                using Mat srcBox = new Mat(maskBinary, safeBox);
                using Mat dstBox = new Mat(finalMask, safeBox);
                srcBox.CopyTo(dstBox);
            }

            maskBinary.Dispose();
            return finalMask;
        }

        private List<YoloSegDetection> NonMaxSuppression(List<YoloSegDetection> dets, double iouThreshold)
        {
            List<YoloSegDetection> result = new();

            foreach (var det in dets.OrderByDescending(d => d.Confidence))
            {
                bool keep = true;
                foreach (var kept in result)
                {
                    if (det.ClassId == kept.ClassId && IoU(det.Box, kept.Box) > iouThreshold)
                    {
                        keep = false;
                        break;
                    }
                }

                if (keep)
                    result.Add(det);
            }

            return result;
        }

        private static double IoU(CvRect a, CvRect b)
        {
            int x1 = Math.Max(a.X, b.X);
            int y1 = Math.Max(a.Y, b.Y);
            int x2 = Math.Min(a.X + a.Width, b.X + b.Width);
            int y2 = Math.Min(a.Y + a.Height, b.Y + b.Height);

            int interW = Math.Max(0, x2 - x1);
            int interH = Math.Max(0, y2 - y1);
            double inter = interW * interH;
            double union = a.Width * a.Height + b.Width * b.Height - inter;
            return union <= 0 ? 0.0 : inter / union;
        }

        private bool IsRbcClass(int classId, string className, Dictionary<int, string> names)
        {
            string n = (className ?? "").ToLowerInvariant();
            if (RBC_KEYWORDS.Any(k => n.Contains(k)))
                return true;

            if (names.Count == 0)
                return classId == FALLBACK_RBC_CLASS_ID;

            return false;
        }

        private bool IsLabelClass(int classId, string className, Dictionary<int, string> names)
        {
            string n = (className ?? "").ToLowerInvariant();
            if (LABEL_KEYWORDS.Any(k => n.Contains(k)))
                return true;

            if (names.Count == 0)
                return classId == FALLBACK_LABEL_CLASS_ID;

            return false;
        }



        private CvRect GenerateRoiFromRbcMask(Mat rbcMask, int imgW, int imgH)
        {
            CvPoint[] pts = GetNonZeroPoints(rbcMask);
            if (pts.Length == 0)
                return MakeSafeRect(_liquidRoi, imgW, imgH);

            int xMin = pts.Min(p => p.X);
            int xMax = pts.Max(p => p.X);
            int rbcW = Math.Max(1, xMax - xMin + 1);

            double padRatio = 0.35;
            int minWidth = 50;
            int pad = (int)(rbcW * padRatio);
            int roiW = Math.Max(minWidth, rbcW + 2 * pad);
            roiW = Math.Min(roiW, (int)(imgW * 0.60));

            int centerX = (xMin + xMax) / 2;
            int x = centerX - roiW / 2;

            // Full image height, same as Python dataset GUI, so upper plasma boundary is searchable.
            return MakeSafeRect(new CvRect(x, 0, roiW, imgH), imgW, imgH);
        }

        private void ApplyTiltCorrectionIfUseful(ref Mat image, ref Mat? rbcMask, ref Mat? labelMask)
        {
            try
            {
                Mat? refMask = null;
                string source = "";

                double qLabel = MaskQualityForTilt(labelMask);
                double qRbc = MaskQualityForTilt(rbcMask);

                if (qLabel > qRbc && labelMask != null && !labelMask.Empty())
                {
                    refMask = labelMask;
                    source = "label";
                }
                else if (rbcMask != null && !rbcMask.Empty())
                {
                    refMask = rbcMask;
                    source = "rbc";
                }

                if (refMask == null)
                    return;

                double? angle = EstimateMaskTiltDeg(refMask);
                if (!angle.HasValue)
                    return;

                _tiltAngleBefore = angle.Value;

                if (Math.Abs(angle.Value) < 0.5)
                {
                    _tiltAngleAfter = angle.Value;
                    return;
                }

                if (Math.Abs(angle.Value) > 5.0)
                {
                    _tiltAngleAfter = angle.Value;
                    return;
                }

                Mat img0 = image.Clone();
                Mat? rbc0 = rbcMask?.Clone();
                Mat? label0 = labelMask?.Clone();

                double bestAfterAbs = double.MaxValue;
                double bestAfter = angle.Value;
                Mat? bestImg = null;
                Mat? bestRbc = null;
                Mat? bestLabel = null;

                try
                {
                    foreach (double rot in new[] { angle.Value, -angle.Value })
                    {
                        Mat testImg = RotateImageKeepSize(img0, rot);
                        Mat? testRbc = rbc0 == null ? null : RotateMaskKeepSize(rbc0, rot);
                        Mat? testLabel = label0 == null ? null : RotateMaskKeepSize(label0, rot);

                        try
                        {
                            Mat? testRef = source == "label" ? testLabel : testRbc;
                            double? after = testRef == null ? null : EstimateMaskTiltDeg(testRef);
                            double afterAbs = after.HasValue ? Math.Abs(after.Value) : 999.0;

                            if (afterAbs < bestAfterAbs)
                            {
                                bestAfterAbs = afterAbs;
                                bestAfter = after ?? 0.0;

                                bestImg?.Dispose();
                                bestRbc?.Dispose();
                                bestLabel?.Dispose();

                                bestImg = testImg.Clone();
                                bestRbc = testRbc?.Clone();
                                bestLabel = testLabel?.Clone();
                            }
                        }
                        finally
                        {
                            testImg.Dispose();
                            testRbc?.Dispose();
                            testLabel?.Dispose();
                        }
                    }

                    if (bestImg != null && bestAfterAbs < Math.Abs(angle.Value))
                    {
                        image.Dispose();
                        image = bestImg.Clone();

                        rbcMask?.Dispose();
                        labelMask?.Dispose();
                        rbcMask = bestRbc?.Clone();
                        labelMask = bestLabel?.Clone();

                        _tiltCorrectionApplied = true;
                        _tiltAngleAfter = bestAfter;
                    }
                    else
                    {
                        _tiltAngleAfter = angle.Value;
                    }
                }
                finally
                {
                    img0.Dispose();
                    rbc0?.Dispose();
                    label0?.Dispose();
                    bestImg?.Dispose();
                    bestRbc?.Dispose();
                    bestLabel?.Dispose();
                }
            }
            catch
            {
                // Tilt correction is optional; never crash measurement because of it.
            }
        }

        private double MaskQualityForTilt(Mat? mask)
        {
            if (mask == null || mask.Empty())
                return 0.0;

            CvPoint[] pts = GetNonZeroPoints(mask);
            if (pts.Length < 80)
                return 0.0;

            int xMin = pts.Min(p => p.X);
            int xMax = pts.Max(p => p.X);
            int yMin = pts.Min(p => p.Y);
            int yMax = pts.Max(p => p.Y);

            int bw = Math.Max(1, xMax - xMin + 1);
            int bh = Math.Max(1, yMax - yMin + 1);
            double elongation = bh / (double)bw;

            return pts.Length * Math.Max(0.0, Math.Min(elongation, 8.0) - 0.8);
        }

        private double? EstimateMaskTiltDeg(Mat mask)
        {
            CvPoint[] pts = GetNonZeroPoints(mask);
            if (pts.Length < 80)
                return null;

            double meanX = pts.Average(p => (double)p.X);
            double meanY = pts.Average(p => (double)p.Y);

            double covXX = 0.0;
            double covXY = 0.0;
            double covYY = 0.0;

            foreach (CvPoint p in pts)
            {
                double dx = p.X - meanX;
                double dy = p.Y - meanY;
                covXX += dx * dx;
                covXY += dx * dy;
                covYY += dy * dy;
            }

            covXX /= pts.Length;
            covXY /= pts.Length;
            covYY /= pts.Length;

            // Principal-axis angle from the x-axis, equivalent to 2D PCA.
            double angleFromX = 0.5 * Math.Atan2(2.0 * covXY, covXX - covYY) * 180.0 / Math.PI;
            double angleFromVertical = angleFromX - 90.0;

            while (angleFromVertical > 90.0) angleFromVertical -= 180.0;
            while (angleFromVertical < -90.0) angleFromVertical += 180.0;

            if (angleFromVertical > 45.0) angleFromVertical -= 90.0;
            if (angleFromVertical < -45.0) angleFromVertical += 90.0;

            return angleFromVertical;
        }

        private Mat RotateImageKeepSize(Mat img, double angleDeg)
        {
            Point2f center = new Point2f(img.Width / 2.0f, img.Height / 2.0f);
            using Mat M = Cv2.GetRotationMatrix2D(center, angleDeg, 1.0);
            Mat rotated = new Mat();
            Cv2.WarpAffine(img, rotated, M, new CvSize(img.Width, img.Height), InterpolationFlags.Linear, BorderTypes.Constant, new Scalar(0, 0, 0));
            return rotated;
        }

        private Mat RotateMaskKeepSize(Mat mask, double angleDeg)
        {
            Point2f center = new Point2f(mask.Width / 2.0f, mask.Height / 2.0f);
            using Mat M = Cv2.GetRotationMatrix2D(center, angleDeg, 1.0);
            Mat rotated = new Mat();
            Cv2.WarpAffine(mask, rotated, M, new CvSize(mask.Width, mask.Height), InterpolationFlags.Nearest, BorderTypes.Constant, Scalar.All(0));
            Cv2.Threshold(rotated, rotated, 127, 255, ThresholdTypes.Binary);
            return rotated;
        }

        private int FindBestUpperBoundaryPeak(double[] profile, double rawThreshold, int searchStart, int searchEndExclusive, double minProminenceRatio)
        {
            if (profile == null || profile.Length < 3)
                return -1;

            searchStart = Math.Max(1, searchStart);
            searchEndExclusive = Math.Min(profile.Length - 1, searchEndExclusive);

            if (searchEndExclusive <= searchStart)
                return -1;

            int bestIdx = -1;
            double bestScore = -1.0;
            int radius = 25;
            double minProminence = rawThreshold * minProminenceRatio;

            for (int i = searchStart; i < searchEndExclusive; i++)
            {
                double v = profile[i];
                if (v < rawThreshold)
                    continue;

                bool isLocalPeak = v >= profile[i - 1] && v > profile[i + 1];
                if (!isLocalPeak)
                    continue;

                double prom = LocalProminence(profile, i, radius);
                if (prom < minProminence)
                    continue;

                if (v > bestScore)
                {
                    bestScore = v;
                    bestIdx = i;
                }
            }

            return bestIdx;
        }

        private static double LocalProminence(double[] profile, int index, int radius)
        {
            int leftStart = Math.Max(0, index - radius);
            int rightEnd = Math.Min(profile.Length - 1, index + radius);

            double leftMin = profile[index];
            for (int i = leftStart; i <= index; i++)
                leftMin = Math.Min(leftMin, profile[i]);

            double rightMin = profile[index];
            for (int i = index; i <= rightEnd; i++)
                rightMin = Math.Min(rightMin, profile[i]);

            double baseLevel = Math.Max(leftMin, rightMin);
            return profile[index] - baseLevel;
        }

        private static int PercentileInt(int[] sortedValues, double percentile)
        {
            if (sortedValues == null || sortedValues.Length == 0)
                return 0;

            double pos = (percentile / 100.0) * (sortedValues.Length - 1);
            int lo = (int)Math.Floor(pos);
            int hi = (int)Math.Ceiling(pos);

            if (lo == hi)
                return sortedValues[lo];

            double t = pos - lo;
            return (int)Math.Round(sortedValues[lo] * (1.0 - t) + sortedValues[hi] * t);
        }


        public void SetCameraTriggerMode(bool enableTrigger)
        {
            Debug.WriteLine(_camera);
            try
            {
                if (_camera == null)
                {
                    // (Phần khởi tạo camera của bạn giữ nguyên)
                    var camera = new BaslerCamera();
                    camera.Open();
                    _camera = camera;
                    TrySetEnumParameter(camera, "PixelFormat", "Mono8");
                    camera.StreamGrabber!.ImageGrabbed += OnImageGrabbed;
                }


                bool wasGrabbing = _camera.StreamGrabber!.IsGrabbing;

                // Dừng để cấu hình lại
                if (wasGrabbing)
                    _camera.StreamGrabber.Stop();

                if (enableTrigger)
                {
                    Debug.WriteLine("Camera Trigger ON");
                    // --- KHI BẬT TRIGGER ---
                    _camera.Parameters[PLCamera.TriggerSelector].SetValue(PLCamera.TriggerSelector.FrameStart);
                    _camera.Parameters[PLCamera.TriggerMode].SetValue(PLCamera.TriggerMode.On);
                    _camera.Parameters[PLCamera.TriggerSource].SetValue(PLCamera.TriggerSource.Line1);
                    _camera.Parameters[PLCamera.TriggerActivation].SetValue(PLCamera.TriggerActivation.RisingEdge);
                    _camera.Parameters[PLCamera.AcquisitionMode].TrySetValue(PLCamera.AcquisitionMode.Continuous);

                    ClearTriggeredImages();
                    _camera.StreamGrabber.Start(GrabStrategy.OneByOne, GrabLoop.ProvidedByStreamGrabber);
                }
                else
                {
                    // --- KHI TẮT TRIGGER (BỔ SUNG) ---
                    _camera.Parameters[PLCamera.TriggerMode].SetValue(PLCamera.TriggerMode.Off);

                    // Quay lại chế độ chụp liên tục tự do
                    _camera.Parameters[PLCamera.AcquisitionMode].TrySetValue(PLCamera.AcquisitionMode.Continuous);

                    // Bắt đầu lại luồng grabber để bạn có thể thấy hình trên UI (Live View)
                    _camera.StreamGrabber.Start(GrabStrategy.OneByOne, GrabLoop.ProvidedByStreamGrabber);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Set trigger mode error: " + ex.Message);
            }
        }

        private void ApplyCameraExposureGainSettings()
        {
            if (_camera == null || !_camera.IsOpen)
            {
                MessageBox.Show("Camera is not open.");
                return;
            }

            try
            {
                bool wasGrabbing = _camera.StreamGrabber != null &&
                                   _camera.StreamGrabber.IsGrabbing;

                if (wasGrabbing)
                    _camera.StreamGrabber!.Stop();

                // ======================
                // EXPOSURE
                // ======================
                if (rad_ExpAuto.IsChecked == true)
                {
                    TrySetEnumParameter(_camera, "ExposureAuto", "Continuous");
                    Debug.WriteLine("[Camera] ExposureAuto = Continuous");
                }
                else
                {
                    TrySetEnumParameter(_camera, "ExposureAuto", "Off");
                    Debug.WriteLine("[Camera] ExposureAuto = Off");

                    double exposureDefault = Properties.Settings.Default.Ex_Man > 0
                        ? Properties.Settings.Default.Ex_Man
                        : 5000.0;

                    double exposure = GetDouble(txt_ExposureTime.Text, exposureDefault);

                    bool exposureOk = TrySetAnyFloatParameter(
                        _camera,
                        new string[]
                        {
                            "ExposureTime",
                            "ExposureTimeAbs"
                        },
                        exposure
                    );

                    if (!exposureOk)
                        MessageBox.Show("Cannot set ExposureTime. Check camera parameter name.");
                }

                // ======================
                // GAIN
                // ======================
                if (rad_GainAuto.IsChecked == true)
                {
                    TrySetEnumParameter(_camera, "GainAuto", "Continuous");
                }
                else
                {
                    TrySetEnumParameter(_camera, "GainAuto", "Off");

                    double gain = GetDouble(
                        txt_RawGain.Text,
                        Properties.Settings.Default.Raw_Man
                    );

                    bool gainOk = TrySetAnyFloatParameter(
                        _camera,
                        new string[]
                        {
                            "Gain",
                            "GainRaw"
                        },
                        gain
                    );

                    if (!gainOk)
                        MessageBox.Show("Cannot set Gain. Check camera parameter name.");
                }

                if (wasGrabbing)
                {
                    _camera.StreamGrabber!.Start(
                        GrabStrategy.OneByOne,
                        GrabLoop.ProvidedByStreamGrabber
                    );
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Apply camera settings error: " + ex.Message);
            }
        }

        private static bool TrySetFloatParameter(BaslerCamera camera, string parameterName, double value)
        {
            try
            {
                IParameter parameter = camera.Parameters[parameterName];

                if (parameter is IFloatParameter floatParameter)
                {
                    double min = floatParameter.GetMinimum();
                    double max = floatParameter.GetMaximum();

                    double safeValue = Math.Max(min, Math.Min(max, value));

                    floatParameter.SetValue(safeValue);

                    double actualValue = floatParameter.GetValue();

                    Debug.WriteLine(
                        $"[Camera] Set {parameterName}: request={value}, safe={safeValue}, actual={actualValue}, min={min}, max={max}"
                    );

                    return true;
                }

                Debug.WriteLine($"[Camera] {parameterName} is not IFloatParameter.");
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Camera] Cannot set {parameterName}: {ex.Message}");
                return false;
            }
        }

        private static bool TrySetAnyFloatParameter(BaslerCamera camera, string[] parameterNames, double value)
        {
            foreach (string name in parameterNames)
            {
                if (TrySetFloatParameter(camera, name, value))
                    return true;
            }

            Debug.WriteLine("[Camera] Cannot set any parameter: " + string.Join(", ", parameterNames));
            return false;
        }

        // Hàm chuyên dùng để luồng Auto mượn đổi độ sáng TRƯỚC khi PLC kích chụp
        public void ChangeCameraExposureAuto(double exposureValue)
        {
            if (_camera != null && _camera.IsOpen)
            {
                TrySetAnyFloatParameter(
                    _camera,
                    new string[] { "ExposureTime", "ExposureTimeAbs" },
                    exposureValue
                );
            }
        }


        // Nút Trigger
        private void btnTrigger_pressed(object sender, MouseButtonEventArgs e)
        {
            MainWindow.Data_transfer.TagWrite("Trigger", 1);
        }
        private void btnTrigger_notpressed(object sender, MouseEventArgs e)
        {
            MainWindow.Data_transfer.TagWrite("Trigger", 0);
        }

        #endregion


    }
}