# Market-mobile

Dự án game mobile 2D màn hình ngang xây dựng trên **Godot Engine 4.7 (.NET)** sử dụng ngôn ngữ **C#**.

---

## 1. Tổng quan cấu hình kỹ thuật

| Hạng mục | Cấu hình hiện tại |
| --- | --- |
| **Engine** | Godot 4.7 (phiên bản **.NET / C#**) |
| **Ngôn ngữ lập trình** | C# (.NET) |
| **Thể loại hiển thị** | 2D Mobile — Màn hình ngang (`1920×1080`) |
| **Cửa sổ test trên PC** | `1280×720` (`window_width_override` / `window_height_override`) |
| **Stretch Mode / Aspect** | `canvas_items` / `expand` |
| **Mobile Orientation** | `Sensor Landscape` (`window/handheld/orientation=4`) |
| **Renderer** | `GL Compatibility` (Desktop & Mobile) |
| **VRAM Texture Compression** | Bật `ETC2 / ASTC` cho Android & iOS |
| **Input Test trên PC** | Bật `emulate_touch_from_mouse=true` |
| **Quản lý Asset nặng** | Git LFS (ảnh, âm thanh, video, font, model 3D, `.scn`, `.res`) |

---

## 2. Yêu cầu môi trường (Bắt buộc đồng nhất cả team)

Mỗi thành viên trong team cần cài đặt đúng bộ công cụ sau để mở và build chung không lỗi:

1. **Godot Engine 4.7 (.NET edition)**:
   - **Bắt buộc tải bản có chữ `.NET` (C#)** — **KHÔNG** dùng bản Standard.
   - Cả team phải dùng **cùng một phiên bản build Godot 4.7** (ví dụ cùng bản beta/rc/stable mà chủ dự án đang dùng) để tránh thay đổi định dạng `.godot` / `Godot.NET.Sdk`.
2. **.NET SDK**:
   - Cài đặt **.NET SDK 8.0 hoặc 9.0** *(máy thiết lập dự án ban đầu đang cài .NET SDK 9.0)*.
   - Kiểm tra nhanh bằng terminal: `dotnet --version`.
3. **Git & Git LFS**:
   - Git phiên bản mới + **Git LFS** (`git lfs version`).
4. **IDE khuyến nghị**:
   - Visual Studio Code (cài các extension gợi ý trong `.vscode/extensions.json`), JetBrains Rider, hoặc Visual Studio 2022.

---

## 3. 4 bước bắt buộc cho chủ dự án trước khi commit & push lần đầu

> [!IMPORTANT]
> Kho lưu trữ hiện tại chưa tự tạo file `.csproj` / `.sln` vì các file này cần được sinh đúng phiên bản `Godot.NET.Sdk` của bản Godot 4.7 (.NET) bạn đang cài trên máy. Hãy thực hiện đủ 4 bước sau trước khi đẩy lên GitHub:

1. Mở thư mục `Market-mobile` bằng **Godot 4.7 (.NET)**.
2. Trên thanh menu của Godot, chọn:
   **Project → Tools → C# → Create C# solution**
   *(Thao tác này sẽ tự tạo `Market-mobile.csproj`, `Market-mobile.sln` và cập nhật tính năng C# vào `project.godot` đúng chuẩn).*
3. Nhấn nút **Build** (biểu tượng cây búa ở góc trên bên phải Godot Editor) để xác nhận C# biên dịch thành công không lỗi.
4. Mở terminal tại thư mục `Market-mobile` và đẩy code khởi tạo lên GitHub:
   ```bash
   git status
   git add .
   git commit -m "chore: initial godot 4.7 c# team setup"
   git push -u origin main
   ```

---

## 4. Hướng dẫn Clone & Chạy cho thành viên mới

1. Kích hoạt Git LFS trên máy (chỉ cần làm 1 lần cho mỗi máy):
   ```bash
   git lfs install
   ```
2. Clone repository về máy:
   ```bash
   git clone https://github.com/loidinh84/Market-mobile.git
   cd Market-mobile
   ```
   *(Nếu lỡ clone trước khi chạy `git lfs install`, hãy chạy `git lfs pull` trong thư mục dự án).*
3. Mở **Godot 4.7 (.NET)** → chọn **Import** → trỏ tới file `project.godot`.
4. Chờ Godot tự động tạo thư mục `.godot/` và import toàn bộ tài nguyên lần đầu.
5. Nhấn nút **Build** (biểu tượng cây búa) ở góc trên bên phải để restore NuGet packages và biên dịch C#, sau đó nhấn **Play (F5)** để chạy thử.

---

## 5. Cấu trúc thư mục dự án

```text
Market-mobile/
├── .github/                  # Mẫu Pull Request cho team
├── .vscode/                  # Extension khuyến nghị & settings dùng chung cho VS Code
├── addons/                   # Plugin / Addon của Godot
├── assets/
│   ├── audio/
│   │   ├── music/            # Nhạc nền (.ogg / .mp3)
│   │   └── sfx/              # Hiệu ứng âm thanh (.wav / .ogg)
│   ├── fonts/                # Font chữ (.ttf / .otf)
│   ├── shaders/              # Shader (.gdshader / .gdshaderinc)
│   └── sprites/
│       ├── environment/      # Background, vật thể môi trường, props
│       ├── icons/            # Icon gameplay, item
│       └── ui/               # Texture nút bấm, khung UI, HUD
├── docs/                     # Tài liệu thiết kế game (GDD, kiến trúc...)
├── prefabs/                  # Đối tượng tái sử dụng & Base Prefab (kế thừa bằng New Inherited Scene)
│   ├── characters/           # Nhân vật, khách hàng, nhân viên (base_character.tscn)
│   ├── environment/          # Vật trang trí, chướng ngại vật (base_prop.tscn)
│   ├── items/                # Sản phẩm, nguyên liệu, tiền rơi (base_item.tscn)
│   ├── stations/             # Quầy thu ngân, kệ hàng, máy chế biến (base_station.tscn)
│   ├── ui/                   # Nút bấm, thẻ item, popup mẫu (base_button/card/popup.tscn)
│   └── vfx/                  # Chữ bay, hiệu ứng hạt (base_floating_text/vfx_burst.tscn)
├── resources/
│   └── themes/               # Theme UI (.tres), Resource dữ liệu dùng chung
├── scenes/                   # Các màn chơi & màn hình hoàn chỉnh (lắp ghép từ các Prefab)
│   ├── autoload/             # Scene autoload toàn cục
│   ├── gameplay/             # Scene màn chơi, level, khu vực market
│   ├── main/                 # Scene khởi động / điều phối chính
│   └── ui/                   # Màn hình menu, shop tổng, HUD hoàn chỉnh
├── scripts/
│   ├── Autoload/             # Singleton C# (GameManager, AudioManager...)
│   ├── Core/                 # Hệ thống lõi, state machine, save/load
│   ├── Data/                 # Class dữ liệu, Godot Resource C# ([GlobalClass])
│   ├── Gameplay/             # Logic điều khiển nhân vật, tương tác màn chơi
│   ├── UI/                   # Script điều khiển giao diện
│   └── Utils/                # Hàm tiện ích, extension methods
├── CONTRIBUTING.md           # Quy ước Git, tránh conflict & coding style
├── project.godot             # Cấu hình dự án Godot
└── README.md                 # Tài liệu hướng dẫn dự án
```

---

## 6. Lưu ý Export Android / iOS an toàn

- **KHÔNG BAO GIỜ** commit file chữ ký số (`*.keystore`, `*.jks`, `*.p12`, `*.mobileprovision`), mật khẩu ký build (`export_credentials.cfg`), hoặc file build đầu ra (`*.apk`, `*.aab`, `*.ipa`, `builds/`, `exports/`). Các định dạng này đã được chặn trong `.gitignore`.
- Khi thêm Export Preset trên máy cá nhân (`export_presets.cfg`), kiểm tra kỹ không lưu mật khẩu keystore dạng plain text vào file commit chung.
- Để xuất Android (.NET), cần cài thêm **Android SDK**, **JDK** phù hợp (theo tài liệu Godot 4.7) và **Android Export Templates** tương ứng với đúng bản Godot 4.7 (.NET) đang dùng.

---

Đọc thêm quy trình làm việc nhóm hàng ngày tại [CONTRIBUTING.md](CONTRIBUTING.md).
