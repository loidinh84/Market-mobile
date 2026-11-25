shader_type canvas_item;

uniform vec4 color_left : source_color = vec4(0.325, 0.204, 0.624, 0.0);
uniform vec4 color_mid : source_color = vec4(0.62, 0.50, 0.82, 0.48);
uniform vec4 color_right : source_color = vec4(1.0, 0.96, 0.74, 0.95);
uniform vec4 edge_glow_color : source_color = vec4(1.0, 0.99, 0.90, 1.0);
uniform float sweep_pos : hint_range(-0.5, 1.5) = -0.5;
uniform float sweep_width : hint_range(0.05, 0.5) = 0.22;
uniform float sweep_strength : hint_range(0.0, 1.0) = 0.42;

void fragment() {
	float x = UV.x;

	// Smooth seamless horizontal gradient: transparent left -> lavender mid -> warm cream-gold right
	vec4 base_col;
	if (x < 0.48) {
		float t = smoothstep(0.0, 0.48, x);
		base_col = mix(color_left, color_mid, t);
	} else {
		float t = smoothstep(0.48, 0.985, x);
		base_col = mix(color_mid, color_right, t);
	}

	// Crisp glowing right edge flush against the vertical divider
	float right_edge = smoothstep(0.982, 0.996, x);
	base_col = mix(base_col, edge_glow_color, right_edge * 0.85);

	// Soft vertical feathering at very top/bottom edges (1-2px anti-alias)
	float v_feather = smoothstep(0.0, 0.03, UV.y) * smoothstep(1.0, 0.97, UV.y);

	// One-shot slanted light sweep shimmer
	float diag = x - (UV.y - 0.5) * 0.22;
	float dist = abs(diag - sweep_pos);
	float sweep = (1.0 - smoothstep(0.0, sweep_width, dist)) * sweep_strength;
	// Fade sweep out cleanly at the transparent left edge
	sweep *= smoothstep(0.05, 0.35, x);

	vec3 final_rgb = base_col.rgb + vec3(1.0, 0.98, 0.88) * sweep;
	float final_a = clamp(base_col.a + sweep * 0.35, 0.0, 1.0) * v_feather;

	COLOR = vec4(final_rgb, final_a) * COLOR;
}
