using Godot;
namespace Heartbeat;

// Shared world-space procedural textures: no downloads and no stretching on tall walls.
public static class SurfaceMaterials
{
    static readonly Dictionary<string, Material> Cache = new();
    static readonly Dictionary<string, Material> ForestCache = new();
    static readonly Dictionary<string, Material> PbrCache = new();
    static readonly Shader Surface = new() { Code = """
        shader_type spatial;
        uniform vec4 tint : source_color;
        uniform int surface = 0;
        varying vec3 world;
        varying vec3 world_normal;
        float hash(vec3 p) { return fract(sin(dot(p, vec3(12.9898,78.233,37.719))) * 43758.5453); }
        float noise(vec3 p) {
            vec3 i = floor(p), f = fract(p); f = f*f*(3.0-2.0*f);
            return mix(mix(mix(hash(i),hash(i+vec3(1,0,0)),f.x), mix(hash(i+vec3(0,1,0)),hash(i+vec3(1,1,0)),f.x),f.y),
                       mix(mix(hash(i+vec3(0,0,1)),hash(i+vec3(1,0,1)),f.x), mix(hash(i+vec3(0,1,1)),hash(i+vec3(1,1,1)),f.x),f.y),f.z);
        }
        void vertex() {
            world = (MODEL_MATRIX * vec4(VERTEX,1.0)).xyz;
            world_normal = normalize(MODEL_NORMAL_MATRIX * NORMAL);
            if(surface == 3 && world.y > 1.0) {
                VERTEX.x += sin(TIME*0.65 + world.y*1.7 + world.z*.15) * .045;
                VERTEX.z += cos(TIME*0.52 + world.x*.12) * .025;
            }
        }
        void fragment() {
            vec3 n = abs(world_normal);
            vec2 uv = n.y > max(n.x,n.z) ? world.xz : (n.x > n.z ? world.zy : world.xy);
            float fine = noise(world*48.0);
            float variation = 0.91 + fine*0.13 + noise(world*2.5)*0.08;
            if(surface == 1) {
                variation = 0.78 + noise(vec3(uv.x*1.5, uv.y*75.0,0))*0.3 + fine*0.1;
            } else if(surface == 2) {
                vec2 cell = uv / vec2(0.48,0.22);
                cell.x += mod(floor(cell.y),2.0)*0.5;
                vec2 edge = min(fract(cell),1.0-fract(cell));
                vec2 aa = max(fwidth(cell),vec2(0.001));
                float brick = smoothstep(0.015,0.015+aa.x,edge.x)*smoothstep(0.025,0.025+aa.y,edge.y);
                variation = mix(0.58, variation * (0.92+hash(vec3(floor(cell),0))*0.14),brick);
            } else if(surface == 3) {
                variation = 0.72+noise(world*4.0)*0.2+fine*0.26;
            } else if(surface == 4) {
                variation = 0.8+fine*0.27;
            } else if(surface == 5) {
                vec2 cell = uv/0.8;
                vec2 edge = min(fract(cell),1.0-fract(cell));
                vec2 aa = max(fwidth(cell),vec2(0.001));
                variation *= 0.75+0.25*smoothstep(0.008,0.008+aa.x,edge.x)*smoothstep(0.008,0.008+aa.y,edge.y);
            }
            ALBEDO = tint.rgb * variation;
            ALPHA = 1.0;
            ROUGHNESS = surface == 1 ? 0.72 : 0.94;
        }
        """ };

    public static Material For(string color)
    {
        if (Cache.TryGetValue(color, out var material)) return material;
        var type = color switch
        {
            "846a58" or "9d755f" or "624b40" or "5b554d" => 1,
            "726961" or "243242" => 2,
            "354e4b" or "2b645b" => 3,
            "1c2330" or "303849" => 4,
            "3a3a48" or "776857" or "8a8170" or "4a4a58" => 5,
            _ => 0
        };
        if (type == 0) return Cache[color] = new StandardMaterial3D { AlbedoColor = new Color(color), Roughness = .8f, Transparency = BaseMaterial3D.TransparencyEnum.Disabled };
        var shader = new ShaderMaterial { Shader = Surface };
        shader.SetShaderParameter("tint", new Color(color)); shader.SetShaderParameter("surface", type);
        return Cache[color] = shader;
    }

    public static Material Forest(string color, ForestKind kind)
    {
        string key = color + kind;
        if (ForestCache.TryGetValue(key, out var cached)) return cached;
        var shader = new ShaderMaterial { Shader = new Shader { Code = """
            shader_type spatial;
            render_mode cull_disabled;
            uniform vec4 tint : source_color;
            uniform float wind = 0.0;
            uniform float grass = 0.0;
            float hash(vec2 p){ return fract(sin(dot(p,vec2(127.1,311.7)))*43758.5453); }
            void vertex(){
                vec3 w=(MODEL_MATRIX*vec4(VERTEX,1.0)).xyz;
                if(wind>0.5) VERTEX.xz += vec2(sin(TIME*.7+w.z*.17),cos(TIME*.53+w.x*.13)) * .035 * max(UV.y,.25);
            }
            void fragment(){
                if(grass>0.5){
                    float blade=smoothstep(.48,.18,abs(UV.x-.5)) * smoothstep(1.0,.18,UV.y);
                    ALPHA_SCISSOR_THRESHOLD=.35; ALPHA=blade;
                } else {
                    ALPHA = 1.0;
                }
                float fleck=.86+hash(floor(UV*vec2(18.0,27.0)))*.2;
                ALBEDO=tint.rgb*fleck; ROUGHNESS=.96;
            }
            """ } };
        shader.SetShaderParameter("tint", new Color(color));
        shader.SetShaderParameter("wind", kind is ForestKind.Crown or ForestKind.Conifer or ForestKind.Bush or ForestKind.Grass ? 1f : 0f);
        shader.SetShaderParameter("grass", kind == ForestKind.Grass ? 1f : 0f);
        return ForestCache[key] = shader;
    }

    public static Material ForestGround() => Pbr("forest_floor",new Color("87927d"),new Vector3(5.5f,5.5f,5.5f), true);
    public static Material MossyRock() => Pbr("mossy_rock",new Color("92978d"),new Vector3(1.8f,1.8f,1.8f), true);
    public static Material Bark() => Pbr("bark_brown_01",new Color("88786a"),new Vector3(2.2f,2.2f,2.2f), false);

    static Material Pbr(string id,Color tint,Vector3 scale, bool triplanar = false)
    {
        if(PbrCache.TryGetValue(id,out var cached))return cached;
        string root=$"res://Assets/Materials/{id}/{id}";
        var material=new StandardMaterial3D
        {
            AlbedoColor=new Color(tint.R, tint.G, tint.B, 1f),
            AlbedoTexture=GD.Load<Texture2D>(root+"_diff_1k.jpg"),
            NormalEnabled=true, NormalTexture=GD.Load<Texture2D>(root+"_nor_gl_1k.jpg"),
            Roughness=1, RoughnessTexture=GD.Load<Texture2D>(root+"_rough_1k.jpg"),
            Uv1Scale=scale, TextureFilter=BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic,
            TextureRepeat=true,
            Uv1Triplanar = triplanar,
            Uv1TriplanarSharpness = 1.0f,
            Transparency = BaseMaterial3D.TransparencyEnum.Disabled,
            CullMode = BaseMaterial3D.CullModeEnum.Back,
            DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.OpaqueOnly
        };
        return PbrCache[id]=material;
    }
}
