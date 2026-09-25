shader_type spatial;
render_mode wireframe, cull_disabled, unshaded;

uniform vec4 line_color : source_color = vec4(0.0, 1.0, 0.0, 1.0);

void fragment() {
    ALBEDO = line_color.rgb;
}