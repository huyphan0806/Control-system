**REMOTE CONTROL SYSTEM**

Hệ thống điều khiển và giám sát máy tính từ xa thông qua Web Dashboard. Dự án tích hợp sẵn công cụ Ngrok, cho phép thiết lập kết nối qua Internet mà không yêu cầu cấu hình Port Forwarding (Mở Port).

**THÀNH PHẦN DỰ ÁN**

1. **Source**: Chứa mã nguồn hệ thống.
   - Server: Vận hành trên máy trạm (máy bị điều khiển).
   - Client: Vận hành trên máy quản trị, tự động khởi chạy giao diện Web Dashboard.
2. **Remote Server Tool**: Công cụ hỗ trợ cấu hình tự động và vận hành Server qua Internet.

---

**HƯỚNG DẪN SỬ DỤNG**

**1. Phía Server (Máy bị điều khiển)**

**Cách 1: Kết nối qua Internet (Khuyến nghị)**
- Bước 1: Truy cập thư mục Remote Server Tool -> Core.
- Bước 2: Chuột phải vào file RunMe.bat chọn Edit (hoặc Open with Notepad).
- Bước 3: Tìm dòng Token và nhập Ngrok Authtoken vào vị trí chỉ định.
- Bước 4: Lưu file.
- Bước 5: Quay lại thư mục Remote Server Tool và chạy file "Start Server".
  (Cửa sổ Ngrok sẽ hiển thị kèm đường dẫn kết nối, ví dụ: https://abcd.ngrok-free.app)

**Cách 2: Kết nối mạng LAN**
- Truy cập thư mục Source/Server và chạy file thực thi.
- Sử dụng địa chỉ IP nội bộ của máy để kết nối (ví dụ: 192.168.1.10).

**2. Phía Client (Máy quản trị)**

- Truy cập thư mục Source/Client và chạy chương trình.
- Trình duyệt sẽ tự động hiển thị Web Dashboard.
- Tại màn hình kết nối:
  - Trường hợp dùng Ngrok: Nhập đường dẫn Ngrok (đổi https:// thành wss:// để tối ưu kết nối).
  - Trường hợp dùng LAN: Nhập địa chỉ IP của máy Server.

**LƯU Ý:**
Duy trì cấu trúc thư mục mặc định trong Remote Server Tool để đảm bảo hệ thống hoạt động ổn định.
