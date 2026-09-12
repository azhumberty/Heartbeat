shader_type spatial;
render_mode depth_draw_opaque, cull_back, depth_prepass_alpha;

uniform sampler2D tex : source_color, filter_nearest;

void fragment() {
    vec4 color = texture(tex, UV);
    
    // Calculate difference between color channels to detect grayscale
    float max_diff = max(abs(color.r - color.g), max(abs(color.g - color.b), abs(color.b - color.r)));
    
    // If the pixel is grayscale and bright (white or light gray), discard it
    if (max_diff < 0.08 && color.r > 0.6) {
        discard;
    }
    
    ALBEDO = color.rgb;
    ALPHA = color.a;
}
