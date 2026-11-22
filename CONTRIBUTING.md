# Quy ước làm việc nhóm (CONTRIBUTING)

Tài liệu này giúp cả team `Market-mobile` làm việc mượt mà trên **Godot 4.7 (.NET) + C# + Git LFS** và hạn chế tối đa xung đột (merge conflict).

---

## 1. Quy trình nhánh Git & Commit

### Cấu trúc nhánh
- `main`: Nhánh ổn định, sẵn sàng demo/build test. Chỉ merge qua Pull Request đã kiểm tra.
- `dev`: Nhánh tích hợp chính hàng ngày của team.
- Các nhánh tính năng tách từ `dev` (hoặc `main` nếu chưa tách `dev`):
  - `feature/<ten-tinh-nang>`: Phát triển tính năng mới (ví dụ: `feature/customer-queue`).
  - `fix/<ten-loi>`: Sửa lỗi (ví dụ: `fix/touch-drag-offset`).
  - `art/<ten-asset>`: Cập nhật sprite, UI, âm thanh, animation.

### Chuẩn thông điệp Commit (Conventional Commits)
Viết ngắn gọn, rõ mục đích theo mẫu: `<type>: <mô tả>`:
- `feat:` thêm tính năng mới
- `fix:` sửa lỗi
- `art:` thêm hoặc cập nhật hình ảnh, âm thanh, shader, UI theme
- `refactor:` tái cấu trúc code nhưng không đổi hành vi
- `chore:` cấu hình project, package, thư mục
- `docs:` cập nhật tài liệu

---

## 2. 5 Quy tắc vàng tránh xung đột Godot + C#

1. **Tách nhỏ Scene (`.tscn`) theo trách nhiệm:**
   - Không dồn toàn bộ game vào 1 scene duy nhất để nhiều người cùng sửa cùng lúc.
   - Mỗi màn hình UI, nhân vật, quầy hàng hoặc cụm gameplay nên là một sub-scene riêng.
2. **Thống nhất trước khi sửa `project.godot`:**
   - Việc thêm Input Map, Autoload, Physics Layer hoặc đổi Display Settings đều ghi vào `project.godot`. Hãy báo ngắn cho team trên nhóm chat khi vừa sửa và merge sớm.
3. **Luôn commit đầy đủ `.import` và `.uid`:**
   - Từ Godot 4.4+, mỗi tài nguyên và script đều gắn với **UID** (`.uid` hoặc bên trong `.import`).
   - Nếu quên commit `.import` hoặc `.uid`, máy của đồng đội sẽ báo lỗi thiếu liên kết hoặc phát sinh UID khác nhau gây conflict dây chuyền.
4. **Đổi tên hoặc di chuyển file bên trong Godot Editor (FileSystem dock):**
   - **KHÔNG** kéo thả đổi tên file trực tiếp bằng Windows Explorer khi có scene/script đang tham chiếu, vì Godot cần tự cập nhật lại đường dẫn và UID trong các `.tscn` / `.tres`.
5. **Kiểm tra Git LFS khi thêm tài nguyên media:**
   - Sau khi `git add`, kiểm tra nhanh bằng `git lfs status` để chắc chắn các file `.png`, `.wav`, `.ogg`, `.ttf`, ... nằm trong danh sách LFS trước khi commit.

---

## 3. Quy ước đặt tên

| Đối tượng | Quy ước | Ví dụ đúng |
| --- | --- | --- |
| Thư mục trong `scripts/` | `PascalCase` (khớp namespace C#) | `scripts/Gameplay/`, `scripts/UI/` |
| File script C# (`.cs`) | `PascalCase` (trùng tên class) | `PlayerController.cs`, `ShopHud.cs` |
| Thư mục `scenes/`, `prefabs/`, `assets/`, `resources/` | `snake_case` | `prefabs/stations/`, `assets/sprites/ui/` |
| File scene, prefab, resource, asset | `snake_case` (prefab gốc dùng `base_*`) | `main_menu.tscn`, `base_station.tscn`, `shelf_fruit.tscn` |
| Tên Node trong Scene Tree | `PascalCase` | `PlayerRoot`, `TouchArea`, `CoinLabel` |
| Class / Struct / Enum / Method / Property C# | `PascalCase` | `public int GoldAmount { get; private set; }` |
| Biến cục bộ / Tham số C# | `camelCase` | `float moveSpeed` |
| Field private / protected C# | `_camelCase` | `[Export] private float _moveSpeed = 300f;` |

### Quy ước Signal trong C# (Godot 4)
Mọi Signal trong C# bắt buộc dùng hậu tố `EventHandler` trên delegate để Godot tự sinh C# event tương ứng:

```csharp
using Godot;

namespace MarketMobile.Gameplay;

public partial class CheckoutCounter : Node2D
{
    [Signal]
    public delegate void ItemSoldEventHandler(int amount, int totalEarned);

    public void CompleteSale(int amount, int totalEarned)
    {
        EmitSignal(SignalName.ItemSold, amount, totalEarned);
    }
}
```

---

## 4. Quy ước tạo và quản lý Prefab (`prefabs/` vs `scenes/`)

Trong Godot, mọi `.tscn` đều có thể hoạt động như một Prefab (tương tự Unity). Team tách rõ 2 thư mục để dễ quản lý:
- **`prefabs/`**: Chứa các đối tượng tái sử dụng độc lập (nhân vật, quầy hàng, món đồ, popup, nút bấm, hiệu ứng) và các **Base Prefab** (`base_*.tscn`).
- **`scenes/`**: Chứa các màn chơi, level, màn hình tổng hợp được lắp ghép từ các Prefab trong `prefabs/`.

### Cách tạo Prefab mới từ Base Prefab (Prefab Variant)
1. Trong **FileSystem dock** của Godot Editor, chuột phải vào một mẫu `prefabs/.../base_*.tscn` (ví dụ `prefabs/stations/base_station.tscn`).
2. Chọn **New Inherited Scene** (tương đương tạo Prefab Variant trong Unity).
3. Gắn script C# tương ứng từ `scripts/`, thay đổi sprite, kích thước collision hoặc thông số riêng rồi lưu vào đúng nhóm trong `prefabs/` theo `snake_case` (ví dụ: `prefabs/stations/checkout_counter.tscn`).

### Quy tắc sử dụng & bảo trì Prefab
- **Kéo thả để gắn vào màn chơi (Instantiate Child Scene):** Không chọn **Make Local** (Editable Children chỉ dùng khi thật sự cần chỉnh nhỏ từng instance), tránh làm mất liên kết cập nhật hàng loạt và tránh phình diff trong file `.tscn`.
- **Thông báo team trước khi sửa `base_*.tscn`:** Thay đổi trên một Base Prefab sẽ tự động truyền xuống toàn bộ các Prefab con đang kế thừa từ nó.

---

## 5. Thiết lập External Editor cá nhân (Không đẩy đường dẫn riêng lên Git)

Mỗi thành viên tự chọn trình soạn thảo C# trên máy mình trong **Godot Editor → Editor → Editor Settings → Dotnet → Editor**:
- **VS Code**: chọn `Visual Studio Code` (cài các extension trong `.vscode/extensions.json`).
- **JetBrains Rider**: chọn `JetBrains Rider`.
- **Visual Studio**: chọn `Visual Studio`.

*(Không lưu đường dẫn `.exe` cá nhân vào file trong repository để tránh ảnh hưởng máy thành viên khác).*
