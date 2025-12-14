using System;
using System.Diagnostics; // Thư viện để dùng Process.Start
using System.IO;          // Thư viện để xử lý đường dẫn file
using System.Windows.Forms;

namespace client
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            // Tên file giao diện web
            string htmlFile = "dashboard.html";

            // Lấy đường dẫn đầy đủ của file nằm cùng thư mục với file .exe
            string fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, htmlFile);

            // Kiểm tra xem file có tồn tại không
            if (File.Exists(fullPath))
            {
                try
                {
                    // Mở file bằng trình duyệt mặc định của Windows
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = fullPath,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Không thể mở trình duyệt: " + ex.Message);
                }
            }
            else
            {
                // Báo lỗi nếu quên copy file html vào thư mục debug/release
                MessageBox.Show($"Không tìm thấy file '{htmlFile}'!\nHãy copy file này vào cùng thư mục với file client.exe.", "Lỗi thiếu file");
            }

            // Chương trình kết thúc ngay lập tức sau khi mở web.
            // Không còn chạy ngầm, không tốn RAM.
        }
    }
}