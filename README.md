## Hold The Bow (Multiplayer 2D) – Game Online Realtime bằng Unity + ASP.NET Core SignalR

Một dự án demo game bắn cung 2D nhiều người chơi, tập trung vào trải nghiệm điều khiển mượt mà (client prediction) nhưng vẫn đảm bảo tính công bằng (server authoritative + reconciliation). Mục tiêu là code sạch, dễ đọc, dễ mở rộng.

### Tính năng nổi bật

- **Realtime Multiplayer (SignalR)**: di chuyển nhân vật mượt, cập nhật trạng thái theo thời gian thực giữa các client.
- **Client Prediction + Server Reconciliation**: dự đoán chuyển động ở client để giảm độ trễ, server là nguồn dữ liệu tin cậy; tự động hiệu chỉnh khi lệch.
- **Không va chạm giữa hero, vẫn va chạm môi trường**: thiết kế layer hợp lý; remote player không dùng physics simulation nhưng vẫn hiển thị đúng.
- **Quản lý phiên & Đăng xuất an toàn**: hỗ trợ logout chủ động và auto-logout khi thoát app/thu nhỏ; tránh lỗi `ObjectDisposedException`.
- **Mã nguồn tách lớp rõ ràng**: SharedLibrary dùng chung Request/Response; Server và Unity Client độc lập, dễ build và triển khai.

### Kiến trúc tổng quan

- `Hobow_Server` (ASP.NET Core):
  - Hub SignalR (`HeroHub`) nhận input (direction/speed), tính vị trí, broadcast về client.
  - `GameState` quản lý danh sách hero đang online; `PlayerSessionHandler` xử lý logout.
- `Hold The Bow` (Unity Client):
  - `LocalPlayerController` nhận input, dự đoán di chuyển (prediction), gửi lệnh lên server và nhận cập nhật để reconcile.
  - `RemotePlayerManager` tạo/xóa và cập nhật vị trí các người chơi khác.
  - `LogoutManager` xử lý đăng xuất, đổi scene về màn hình đăng nhập.
- `SharedLibrary`: Chứa Contract (DTO) dùng chung cho request/response giữa server và client.

### Công nghệ sử dụng

- Server: **.NET 8**, **ASP.NET Core**, **SignalR**, **JWT Auth** (đã chuẩn bị hạ tầng xác thực).
- Client: **Unity 2022+**, **C#**, **Rigidbody2D** (local), **Animator** cho chuyển động.
- Giao tiếp: **Shared DLL** (Requests/Responses) để đồng bộ kiểu dữ liệu giữa client và server.

### Cấu trúc thư mục chính

```
  HoldTheBow_Game/
    Hobow_Server/            # ASP.NET Core server (SignalR)
    HoldTheBow_Game/Hold The Bow/  # Unity project (client)
    SharedLibrary/           # Contracts (Requests/Responses)
```

### Hướng dẫn chạy nhanh

1. Chạy Server (ASP.NET Core)

- Yêu cầu: .NET SDK 8

```
cd Hobow_Game/Hobow_Server
dotnet restore
dotnet ef database update   # nếu có dùng migrations
dotnet run
```

Server sẽ lắng nghe trên cấu hình trong `appsettings.Development.json` (ví dụ: `http://localhost:5172`).

2. Build SharedLibrary và copy DLL sang Unity

```
cd Hobow_Game/SharedLibrary
dotnet build -c Debug
cp bin/Debug/netstandard2.1/SharedLibrary.dll ../HoldTheBow_Game/Hold\ The\ Bow/Assets/DLLs/
```

3. Mở Unity Client

- Mở Unity qua folder: `Hobow_Game/HoldTheBow_Game/Hold The Bow`
- Mở scene Login (index 0), đăng nhập, sau đó connect vào server.

Gợi ý: Khi sửa `SharedLibrary`, nhớ build lại và copy DLL trước khi Play trong Unity.

### Cách hoạt động đồng bộ vị trí

- Client gửi input (direction + speed) lên server theo tick.
- Server tính toán vị trí authoritative và trả về `HeroMoveResponse` kèm timestamp.
- Client chạy prediction để cảm giác mượt ngay khi nhấn phím; khi nhận gói từ server:
  - Nếu lệch nhẹ: lerp mượt về vị trí server.
  - Nếu lệch lớn: snap an toàn để đảm bảo tính đúng (chống gian lận).

### Chống gian lận (Anti-cheat) mức cơ bản

- Client không gửi toạ độ tuyệt đối; server tự tính vị trí từ input.
- Server xác định nguồn sự thật, client chỉ hiển thị và tự hiệu chỉnh.

### UX nhỏ nhưng “có võ”

- Remote player giữ nguyên hướng idle vừa di chuyển, tránh cảm giác “bị lật hướng” khi thả phím.
- Xử lý lifecycle: tránh lỗi khi thoát app/đổi scene, đóng kết nối an toàn.

### Những điểm tôi chú trọng khi làm dự án

- Code rõ ràng, tách lớp; đặt tên biến/hàm dễ đọc, dễ review.
- Luồng bất đồng bộ (`async/await`) an toàn, có timeout, có log cụ thể.
- Hạn chế side effects giữa các component, ưu tiên DI ở server.

### Roadmap ngắn hạn

- Đồng bộ bắn tên (projectile) kèm prediction nhẹ.
- Đồng bộ animation nâng cao (combo, trạng thái trúng đòn).
- Phòng/Phân vùng (rooms) và matchmaking cơ bản.
- Viết test cho server (handler, hub) và smoke test automation.

### Tác giả & Liên hệ

- Tên: Phạm Hưng
- Email: hwngt1412@gmail.com
- LinkedIn: https://www.linkedin.com/in/h%C6%B0ng-ph%E1%BA%A1m-32741a378
- GitHub: https://github.com/hungtun
