# ShareData2 Plugin for GameHelper2

ShareData2 là một plugin cho GameHelper2 framework, được thiết kế để chia sẻ dữ liệu game qua HTTP API cho các bot và ứng dụng khác.

## Tính năng

- **HTTP Server**: Host dữ liệu game trên localhost:53868
- **REST API**: Cung cấp các endpoint để lấy dữ liệu game
- **GameHelper2 Integration**: Tích hợp hoàn toàn với GameHelper2 framework
- **Settings UI**: Giao diện cài đặt trong GameHelper2

## Cài đặt

1. Build plugin:
   ```bash
   dotnet build --configuration Release
   ```

2. Copy DLL vào thư mục GameHelper2:
   - File `ShareData2.dll` sẽ được tự động copy vào `GameHelper/bin/Release/net8.0/Plugins/ShareData2/`
   - Hoặc copy thủ công từ `bin/Release/net8.0/ShareData2.dll`

3. Khởi động GameHelper2 và enable plugin ShareData2

## API Endpoints

### GET /
Trả về thông tin cơ bản về API

**Response:**
```json
{
  "name": "ShareData2",
  "version": "2.0.0",
  "endpoints": [
    "/getData?type=partial",
    "/getData?type=full", 
    "/getScreenPos?x=X&y=Y"
  ]
}
```

### GET /getData
Lấy dữ liệu game

**Parameters:**
- `type` (optional): "partial" hoặc "full" (default: "partial")

**Response:**
```json
{
  "gameState": 20,
  "windowBounds": [0, 1920, 0, 1080],
  "mousePosition": [0, 0],
  "terrainString": "...",
  "areaHash": 0,
  "areaName": "Unknown",
  "isLoading": false,
  "awakeEntities": [],
  "player": {
    "gridPosition": [0, 0],
    "lifeData": [100, 100, 0, 100, 100, 0, 0, 0, 0],
    "buffs": [],
    "isMoving": 0,
    "level": 1
  }
}
```

### GET /getScreenPos
Chuyển đổi tọa độ grid thành tọa độ màn hình

**Parameters:**
- `x`: Tọa độ X (grid)
- `y`: Tọa độ Y (grid)

**Response:**
```json
[1000, 2000]
```

## Cài đặt Plugin

Trong GameHelper2:
1. Mở Settings
2. Tìm ShareData2 plugin
3. Enable plugin
4. Cấu hình port (mặc định: 53868)
5. Save settings

## Testing

Chạy script test:
```bash
python test_api.py
```

## Cấu trúc Project

```
ShareData2/
├── ShareData2.csproj          # Project file
├── ShareData2Settings.cs      # Settings class
├── ShareData2.cs              # Main plugin class
├── Structs.cs                 # Data structures
├── HttpServer.cs              # HTTP server implementation
├── test_api.py               # Test script
└── README.md                 # Documentation
```

## Lưu ý

- Plugin chỉ hoạt động khi game đang chạy
- Port mặc định là 53868 (có thể thay đổi trong settings)
- API trả về dữ liệu JSON với CORS headers
- Plugin tự động khởi động HTTP server khi enable

## Troubleshooting

1. **Server không khởi động**: Kiểm tra port có bị sử dụng không
2. **API không trả về dữ liệu**: Đảm bảo game đang chạy và plugin được enable
3. **Build lỗi**: Kiểm tra GameHelper2 framework có đúng version không
