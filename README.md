# ⚡ Zalo Mini - Real-time Social Chat Ecosystem

<div align="center">
  <img src="https://img.shields.io/badge/.NET%208-512BD4?style=for-the-badge&logo=dotnet&logoColor=white" />
  <img src="https://img.shields.io/badge/Entity%20Framework-0078D7?style=for-the-badge&logo=.net&logoColor=white" />
  <img src="https://img.shields.io/badge/MS%20SQL%20Server-CC2927?style=for-the-badge&logo=microsoft-sql-server&logoColor=white" />
  <img src="https://img.shields.io/badge/SignalR-Advanced-brightgreen?style=for-the-badge" />
  <img src="https://img.shields.io/badge/React%20Vite-20232A?style=for-the-badge&logo=react&logoColor=61DAFB" />
</div>

<br/>

## 📖 Trọng tâm cốt lõi (Overview)
Đây là dự án Đồ án Tốt nghiệp xây dựng một Hệ sinh thái Mạng xã hội nhắn tin thời gian thực đa tính năng, được thiết kế theo mô hình **N-Tier Decoupled Monolithic Architecture**. 

Hệ thống được chia làm hai khối độc lập:
1. **Khối Backend (C# .NET 8):** Đảm nhiệm toàn bộ RESTful API, quản trị CSDL quan hệ với Entity Framework Core và duy trì siêu liên kết WebSockets thông qua SignalR.
2. **Khối Frontend (React.js):** Một SPA (Single-Page Application) quản trị trạng thái (State) thông minh, cung cấp giao diện nhắn tin tương tác mượt mà như các ứng dụng gốc (Native Apps).

---

## 🚀 Tính năng nổi bật (Key Features)
- **Xác thực bảo mật:** Đăng nhập 1-chạm cực nhanh bằng Google OAuth2 kết hợp Custom JWT Token, cam kết an toàn thông tin tuyệt đối.
- **Tính Thời Gian Thực (Real-time WebSockets):**
  - Nhắn tin, hiển thị dòng trạng thái *"Đang gõ..."*, ghim Đã xem và Thu hồi tin nhắn ngay lập tức.
  - Thông báo bạn bè vừa truy cập (Online status).
- **Hệ Sinh Thái Mạng Xã Hội:**
  - Mô hình Kết Bạn: Gửi lời mời, Quản lý đơn chờ duyệt, Chặn tin nhắn người lạ.
  - Hệ thống Phòng Chat Nhóm (Groups): Tạo Bang hội, Cấp mã phòng, Xin vào nhóm, Phê duyệt Hộ khẩu thành viên.
- **Tốc độ truy xuất (Hiệu năng):** Database SQL Server được đánh Chỉ mục Ghép (Composite Index) triệt để, đảm bảo tốc độ móc lịch sử Chat vượt trội dưới 1ms cho dù dữ liệu phình to.

---

## 💻 Cấu trúc Công nghệ (Stack)
### Backend (ChatService.Api)
- C#, ASP.NET Core 8 Web API
- Entity Framework Core 8
- Microsoft.AspNetCore.SignalR
- Authentication: JWT Bearer & Google OAuth2.0
- Cơ sở dữ liệu: SQL Server

### Frontend (chat-web-client)
- React.js (Vite)
- Axios (Bắn API)
- @microsoft/signalr (Hứng WebSockets)
- @react-oauth/google

---

## 🛠 Hướng dẫn chạy cục bộ (Local Setup)

### Bước 1: Khởi động Backend
1. Đảm bảo máy đã cài đặt `.NET 8 SDK` và `SQL Server`.
2. Mở thư mục `ChatService.Api` trên Visual Studio và chỉnh sửa chuỗi kết nối (Connection String) trong file `appsettings.json` để trỏ vào SQL Server của máy bạn.
3. Chặn mã vào SQL Server bằng cách mở terminal và gõ: 
   ```bash
   dotnet ef database update
   ```
4. Bấm `Run` hoặc gõ `dotnet run` để bật server (Thường chạy ở cổng `https://localhost:7240`)

### Bước 2: Khởi động Giao diện Frontend
1. Cần cài đặt `Node.js`.
2. Đi vào thư mục giao diện bằng terminal: `cd chat-web-client`.
3. Gõ `npm install` để tải môi trường.
4. Gõ `npm run dev` để khởi chạy Client.
5. Truy cập link `http://localhost:5173` và rủ một người bạn mở Tab Ẩn danh (Incognito) để test chat chéo!

---
*© 2026 Developed by Le Tung Lam*
