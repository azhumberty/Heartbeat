shader_type canvas_item;

// Solid chroma key cutout for VN sprites (TextureRect / Sprite2D).
uniform vec3 key_color : source_color = vec3(0.0, 1.0, 0.0); // #00FF00
uniform float threshold : hint_range(0.0, 1.0) = 0.50;
uniform float smoothness : hint_range(0.0, 1.0) = 0.12;

void fragment() {
	vec4 col = texture(TEXTURE, UV);
	float dist = distance(col.rgb, key_color);
	float alpha = smoothstep(threshold, threshold + smoothness, dist);
	COLOR = vec4(col.rgb, col.a * alpha);
}
