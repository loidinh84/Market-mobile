shader_type canvas_item;

uniform vec4 color_left : source_color = vec4(0.75, 0.16, 0.37, 1.0);
uniform vec4 color_right : source_color = vec4(0.84, 0.46, 0.81, 1.0);
uniform float skew_ratio : hint_range(0.0, 0.25) = 0.055;

void fragment() {
    float left_edge = skew_ratio * (1.0 - UV.y);
    float right_edge = 1.0 - skew_ratio * UV.y;

    float aa = max(fwidth(UV.x), 0.0015);
    float mask_left = smoothstep(left_edge - aa, left_edge + aa, UV.x);
    float mask_right = 1.0 - smoothstep(right_edge - aa, right_edge + aa, UV.x);
    float alpha = mask_left * mask_right;

    float t = clamp((UV.x - left_edge) / max(right_edge - left_edge, 0.001), 0.0, 1.0);
    vec4 grad = mix(color_left, color_right, t);
    COLOR = vec4(grad.rgb, grad.a * alpha);
}
