using System;
using System.Windows.Forms;
// THƯ VIỆN FLECK
using Fleck;

namespace server
{
    static class Program
    {
        // Thay TcpClient bằng IWebSocketConnection của Fleck
        public static IWebSocketConnection clientSocket;

        // Không còn dùng Stream nữa, WebSocket tự quản lý
        // public static NetworkStream ns; 
        // public static StreamReader nr;
        // public static StreamWriter nw;

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new server());
        }
    }
}