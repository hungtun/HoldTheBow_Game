# Hold The Bow - Multiplayer 2D Game

**Game bắn cung 2D nhiều người chơi** được phát triển bằng Unity + ASP.NET Core SignalR, tập trung vào trải nghiệm điều khiển mượt mà (client prediction) nhưng vẫn đảm bảo tính công bằng (server authoritative + reconciliation). Mục tiêu là code sạch, dễ đọc, dễ mở rộng.

## 🎮 Video Demo

[![Watch Demo Video](https://img.youtube.com/vi/_9BCwl-hJc8/0.jpg)](https://www.youtube.com/watch?v=_9BCwl-hJc8)

## ✨ Tính năng nổi bật

- **🎯 Realtime Multiplayer (SignalR)**: Di chuyển nhân vật mượt mà, cập nhật trạng thái theo thời gian thực giữa các client
- **⚡ Client Prediction + Server Reconciliation**: Dự đoán chuyển động ở client để giảm độ trễ, server là nguồn dữ liệu tin cậy; tự động hiệu chỉnh khi lệch
- **🤖 Enemy AI với Collision Detection**: Enemy thông minh đuổi theo hero, có collision detection giữa các enemy và với map
- **🏃‍♂️ Physics System**: Hệ thống va chạm hoàn chỉnh cho map, hero-hero, hero-enemy, và enemy-enemy
- **🔐 JWT Authentication**: Hệ thống xác thực bảo mật với JWT tokens
- **📦 Clean Architecture**: SharedLibrary dùng chung Request/Response; Server và Unity Client độc lập, dễ build và triển khai
- **🎮 Multiple Heroes & Enemies**: Hỗ trợ nhiều người chơi và enemy cùng lúc

## 🏗️ Kiến trúc tổng quan

### Server (ASP.NET Core)

- **SignalR Hubs**: `HeroHub`, `EnemyHub` xử lý real-time communication
- **Game State Management**: `GameState` quản lý heroes và enemies đang online
- **Physics System**: `ServerPhysicsManager` xử lý collision detection server-side
- **Enemy AI**: `EnemyAIService` chạy AI logic cho enemies
- **Authentication**: JWT-based authentication với session management

### Client (Unity)

- **Local Player**: `LocalPlayerController` với client prediction
- **Remote Players**: `RemotePlayerManager` quản lý người chơi khác
- **Enemy Management**: `EnemyClientManager` hiển thị và đồng bộ enemies
- **Map System**: `MapDataManager` xử lý collision objects từ server

### Shared Library

- **Contracts**: Request/Response DTOs dùng chung giữa server và client
- **Data Models**: `MapData`, `Hero`, `Enemy` models

## 🛠️ Công nghệ sử dụng

### Server

- **.NET 8** - Framework chính
- **ASP.NET Core** - Web framework
- **SignalR** - Real-time communication
- **Entity Framework Core** - Database ORM
- **JWT Authentication** - Security

### Client

- **Unity 2022+** - Game engine
- **C#** - Programming language
- **Rigidbody2D** - Physics simulation
- **Animator** - Animation system

### Communication

- **Shared DLL** - Common contracts (Requests/Responses)
- **SignalR** - Real-time messaging

## 📁 Cấu trúc thư mục

```
Hobow_Game/
├── Hobow_Server/                    # ASP.NET Core server
│   ├── Controllers/                 # API controllers
│   ├── Hubs/                       # SignalR hubs
│   ├── Handlers/                   # Business logic handlers
│   ├── Services/                   # Background services
│   ├── Models/                     # Data models
│   ├── Physics/                    # Server-side physics
│   └── Migrations/                 # Database migrations
├── HoldTheBow_Game/Hold The Bow/   # Unity client project
│   ├── Assets/                     # Game assets
│   │   ├── _Scripts/              # Game scripts
│   │   ├── Prefabs/               # Game prefabs
│   │   ├── Scenes/                # Game scenes
│   │   └── DLLs/                  # Shared library DLL
│   ├── ProjectSettings/            # Unity project settings
│   └── UserSettings/               # Editor settings
└── SharedLibrary/                  # Common contracts
    ├── Requests/                   # Request DTOs
    ├── Responses/                  # Response DTOs
    └── DataModels/                 # Shared data models
```

## 🚀 Hướng dẫn chạy nhanh

### 1. Clone Repository

```bash
git clone https://github.com/hungtun/HoldTheBow_Game.git
cd HoldTheBow_Game
```

### 2. Chạy Server (ASP.NET Core)

**Yêu cầu**: .NET SDK 8

```bash
cd Hobow_Game/Hobow_Server
dotnet restore
dotnet ef database update   # Cập nhật database
dotnet run
```

Server sẽ lắng nghe trên `http://localhost:5172` (cấu hình trong `appsettings.Development.json`).

### 3. Build SharedLibrary

```bash
cd Hobow_Game/SharedLibrary
dotnet build -c Debug
cp bin/Debug/netstandard2.1/SharedLibrary.dll ../HoldTheBow_Game/Hold\ The\ Bow/Assets/DLLs/
```

### 4. Mở Unity Client

1. Mở Unity Hub
2. Add project from disk: `Hobow_Game/HoldTheBow_Game/Hold The Bow`
3. Mở scene **Login** (index 0)
4. Đăng nhập và kết nối vào server

> **Lưu ý**: Khi sửa `SharedLibrary`, nhớ build lại và copy DLL trước khi Play trong Unity.

## ⚙️ Cách hoạt động

### Đồng bộ vị trí

- Client gửi input (direction + speed) lên server theo tick
- Server tính toán vị trí authoritative và trả về `HeroMoveResponse` kèm timestamp
- Client chạy prediction để cảm giác mượt ngay khi nhấn phím; khi nhận gói từ server:
  - Nếu lệch nhẹ: lerp mượt về vị trí server
  - Nếu lệch lớn: snap an toàn để đảm bảo tính đúng (chống gian lận)

### Enemy AI System

- Server-side AI chạy trong `EnemyAIService`
- Enemy đuổi theo hero trong phạm vi nhất định
- Collision detection giữa enemies và với map objects
- Real-time synchronization với client

### Anti-cheat (Cơ bản)

- Client không gửi tọa độ tuyệt đối; server tự tính vị trí từ input
- Server xác định nguồn sự thật, client chỉ hiển thị và tự hiệu chỉnh
- Physics validation server-side

## 🎯 Tính năng đã hoàn thành

- ✅ **Multiplayer Real-time** với SignalR
- ✅ **Client Prediction + Server Reconciliation**
- ✅ **Enemy AI** với collision detection
- ✅ **Physics System** hoàn chỉnh
- ✅ **JWT Authentication**
- ✅ **Session Management**
- ✅ **Map Collision Detection**
- ✅ **Enemy-Enemy Collision**

## 🚧 Roadmap

### Ngắn hạn

- [ ] Đồng bộ bắn tên (projectile) với prediction
- [ ] Animation synchronization nâng cao
- [ ] Room/Matchmaking system
- [ ] Unit tests cho server

### Dài hạn

- [ ] Multiple maps
- [ ] Power-ups và items
- [ ] Leaderboard system
- [ ] Mobile support

## 👨‍💻 Tác giả & Liên hệ

**Phạm Hưng**

- 📧 Email: hwngt1412@gmail.com
- 💼 LinkedIn: [hung-pham-32741a378](https://www.linkedin.com/in/h%C6%B0ng-ph%E1%BA%A1m-32741a378)
- 🐙 GitHub: [hungtun](https://github.com/hungtun)

---
