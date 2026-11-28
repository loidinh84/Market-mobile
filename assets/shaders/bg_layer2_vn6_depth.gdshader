shader_type canvas_item;

// Hướng chạy: từ góc dưới-trái lên góc trên-phải (X dương = sang phải, Y âm = đi lên)
uniform vec2 scroll_speed = vec2(0.055, -0.055);

// Độ rõ của lớp họa tiết nền
uniform float layer_opacity : hint_range(0.0, 1.0) = 0.30;

// Tông màu phủ nhẹ giúp icon hòa chìm vào màu nền sảnh Layer 1
uniform vec4 tint_color : source_color = vec4(0.96, 0.92, 1.0, 1.0);

// Độ mờ dần về phía 4 góc màn hình tạo chiều sâu không gian
uniform float edge_vignette : hint_range(0.0, 1.0) = 0.28;

// Tỷ lệ phóng to/thu nhỏ họa tiết (lớn hơn = icon nhỏ hơn, lặp nhiều hơn)
uniform float tile_scale : hint_range(0.5, 3.0) = 1.65;

void fragment() {
    // Tách fract() ra khỏi UV để giữ derivative liên tục (tránh đường seam khi wrap),
    // kết hợp với texture_repeat = Enabled trên TextureRect để cuộn vô tận mượt mà
    vec2 scroll_offset = fract(scroll_speed * TIME);
    vec2 scrolled_uv = UV * tile_scale - scroll_offset;
    vec4 tex = texture(TEXTURE, scrolled_uv);

    // Vignette chiều sâu mềm ở rìa màn hình
    vec2 centered = (UV - vec2(0.5, 0.46)) * vec2(1.6, 1.45);
    float dist = clamp(length(centered), 0.0, 1.0);
    float depth_fade = 1.0 - edge_vignette * pow(dist, 1.4);

    vec3 final_rgb = tex.rgb * tint_color.rgb;
    float final_alpha = tex.a * layer_opacity * tint_color.a * depth_fade;

    COLOR = vec4(final_rgb, final_alpha);
}
