## Mô tả thay đổi
<!-- Mô tả ngắn gọn bạn đã thêm, sửa hoặc xóa gì và lý do -->

## Loại thay đổi
- [ ] Tính năng mới (`feat`)
- [ ] Sửa lỗi (`fix`)
- [ ] Giao diện / UI / Asset (`art` / `ui`)
- [ ] Tái cấu trúc code (`refactor`)
- [ ] Cấu hình / Tài liệu (`chore` / `docs`)

## Checklist kiểm tra (Godot 4.7 .NET + C# + Git LFS)
- [ ] Đã nhấn **Build** trong Godot 4.7 (.NET) / `dotnet build`, không còn lỗi hoặc cảnh báo nghiêm trọng.
- [ ] Đã chạy thử game (Play) và kiểm tra đúng màn hình ngang 16:9 / 1920×1080 (hoặc các tỷ lệ mobile phổ biến).
- [ ] Không chỉnh sửa ngoài ý muốn vào `project.godot` hoặc các scene `.tscn` dùng chung của thành viên khác.
- [ ] Đã commit đầy đủ file `.import` và `.uid` đi kèm mọi script/asset mới.
- [ ] Kiểm tra `git lfs status` đảm bảo file ảnh/âm thanh/font nặng đều đi qua Git LFS.
- [ ] Không có file bí mật hoặc build cục bộ bị lọt vào commit (`*.keystore`, `*.jks`, `export_credentials.cfg`, `bin/`, `obj/`, `.godot/`).
