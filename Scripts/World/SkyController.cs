using Godot;
namespace Heartbeat;

/// <summary>Lightweight procedural sky with independently moving cloud layers.</summary>
public partial class SkyController : Node3D
{
    public WorldTimeSystem Time { get; set; } = null!;
    public Node3D Follow { get; set; } = null!;
    readonly Godot.Environment _environment = new();
    readonly ProceduralSkyMaterial _skyMaterial = new();
    DirectionalLight3D _sunLight = null!;
    Sprite3D _sun = null!, _moon = null!;
    StandardMaterial3D _starsMaterial = null!;
    MultiMeshInstance3D _stars = null!, _forestHorizon = null!;
    GpuParticles3D _ambientParticles = null!;
    ParticleProcessMaterial _particleProcess = null!;
    StandardMaterial3D _particleMaterial = null!;
    readonly List<ShaderMaterial> _cloudMaterials = new();
    float _artificialTimer;

    public override void _Ready()
    {
        _environment.BackgroundMode = Godot.Environment.BGMode.Sky;
        _environment.Sky = new Sky { SkyMaterial = _skyMaterial, RadianceSize = Sky.RadianceSizeEnum.Size256 };
        _environment.AmbientLightSource = Godot.Environment.AmbientSource.Color;
        _environment.TonemapMode = Godot.Environment.ToneMapper.Filmic;
        _environment.AdjustmentEnabled = true;
        _environment.AdjustmentBrightness = .94f;
        _environment.AdjustmentContrast = 1.08f;
        _environment.AdjustmentSaturation = .95f; // slightly more saturated
        _environment.FogEnabled = true;
        _environment.FogDensity = .008f;
        _environment.FogHeight = 2.2f;
        _environment.FogHeightDensity = .12f;
        // ENABLE VOLUMETRIC FOG for dark fantasy / god rays vibe
        _environment.VolumetricFogEnabled = true;
        _environment.VolumetricFogDensity = 0.015f;
        _environment.VolumetricFogAlbedo = new Color("7b9195");
        _environment.VolumetricFogEmissionEnergy = 0.1f;
        
        _environment.SsaoEnabled = true; // Ambient Occlusion
        _environment.SsilEnabled = false;
        AddChild(new WorldEnvironment { Environment = _environment });

        _sunLight = new DirectionalLight3D { ShadowEnabled = true, DirectionalShadowMaxDistance = 50 };
        AddChild(_sunLight);
        _sun = CelestialBody("Sun", 2.7f, "res://Assets/ArtKit/Celestial/sun.png", new Color("ffd8a6"));
        _moon = CelestialBody("Moon", 2.1f, "res://Assets/ArtKit/Celestial/moon.png", new Color("c9d8ff"));
        AddChild(_sun); AddChild(_moon);
        
        BuildAmbientParticles();
        BuildClouds(); BuildStars(); BuildForestHorizon();
        ApplyTime();
    }

    static StandardMaterial3D GlowMaterial(Color color) => new()
    {
        AlbedoColor = color, EmissionEnabled = true, Emission = color,
        EmissionEnergyMultiplier = 2.2f, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
    };
    static Sprite3D CelestialBody(string name, float scale, string texPath, Color color)
    {
        Texture2D? tex = null;
        string global = ProjectSettings.GlobalizePath(texPath);
        if (System.IO.File.Exists(global))
        {
            var bytes = System.IO.File.ReadAllBytes(global);
            var img = new Image();
            if (bytes.Length > 2 && bytes[0] == 0xFF && bytes[1] == 0xD8)
            {
                if (img.LoadJpgFromBuffer(bytes) == Error.Ok) tex = ImageTexture.CreateFromImage(img);
            }
            else
            {
                if (img.LoadPngFromBuffer(bytes) == Error.Ok) tex = ImageTexture.CreateFromImage(img);
            }
        }
        if (tex == null && ResourceLoader.Exists(texPath)) tex = GD.Load<Texture2D>(texPath);
        
        var sprite = new Sprite3D
        {
            Name = name, Texture = tex, PixelSize = 0.035f * scale,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, Modulate = color, Transparent = true,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        
        if (tex != null && ResourceLoader.Exists("res://Assets/ArtKit/Billboards/checker_mask.gdshader"))
        {
            var shader = GD.Load<Shader>("res://Assets/ArtKit/Billboards/checker_mask.gdshader");
            var mat = new ShaderMaterial { Shader = shader };
            mat.SetShaderParameter("tex", tex);
            sprite.MaterialOverride = mat;
        }

        return sprite;
    }

    void BuildAmbientParticles()
    {
        _particleMaterial = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles, Transparency = BaseMaterial3D.TransparencyEnum.Alpha, VertexColorUseAsAlbedo = true };
        _particleProcess = new ParticleProcessMaterial
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
            EmissionBoxExtents = new Vector3(30, 10, 30),
            Gravity = new Vector3(0, -0.05f, 0),
            Color = new Color("e2f0a1"), // Day pollen color
            Direction = new Vector3(1, 0, 1),
            Spread = 30,
            InitialVelocityMin = 0.1f, InitialVelocityMax = 0.5f,
            ScaleMin = 0.02f, ScaleMax = 0.05f,
            TurbulenceEnabled = true, TurbulenceNoiseStrength = 0.2f
        };
        _ambientParticles = new GpuParticles3D
        {
            Amount = 150, Lifetime = 8f, ProcessMaterial = _particleProcess,
            DrawPass1 = new QuadMesh { Size = new Vector2(0.1f, 0.1f) },
            MaterialOverride = _particleMaterial, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Position = new Vector3(0, 5, 0)
        };
        AddChild(_ambientParticles);
    }

    void BuildClouds()
    {
        const string shaderCode = """
            shader_type spatial;
            render_mode unshaded, cull_disabled, depth_draw_never, blend_mix;
            uniform float speed = 0.004;
            uniform float density = 0.5;
            uniform vec4 day_color : source_color = vec4(1.0);
            uniform float visibility = 1.0;
            float h(vec2 p){ return fract(sin(dot(p,vec2(127.1,311.7)))*43758.5453); }
            float n(vec2 p){ vec2 i=floor(p),f=fract(p); f=f*f*(3.0-2.0*f); return mix(mix(h(i),h(i+vec2(1,0)),f.x),mix(h(i+vec2(0,1)),h(i+vec2(1,1)),f.x),f.y); }
            void fragment(){
                vec2 p=UV*6.0+vec2(TIME*speed,0.0);
                float cloud=n(p);
                float a=smoothstep(density, density+0.22, cloud)*0.34*visibility;
                ALBEDO=day_color.rgb; ALPHA=a;
            }
            """;
        for (int i = 0; i < 2; i++)
        {
            var material = new ShaderMaterial { Shader = new Shader { Code = shaderCode } };
            material.SetShaderParameter("speed", .009f - i * .0025f);
            material.SetShaderParameter("density", .66f + i * .035f);
            _cloudMaterials.Add(material);
            var plane = new MeshInstance3D
            {
                Name = $"CloudLayer{i + 1}", Position = new(0, 30 + i * 12, 0),
                Mesh = new PlaneMesh { Size = new Vector2(190 - i * 18, 190 - i * 18), SubdivideWidth = 1, SubdivideDepth = 1 },
                MaterialOverride = material, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            };
            AddChild(plane);
        }
    }

    void BuildStars()
    {
        _starsMaterial = GlowMaterial(Colors.White); _starsMaterial.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        var multi = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = new SphereMesh { Radius = .09f, Height = .18f }, InstanceCount = 70 };
        var random = new Random(9321);
        for (int i = 0; i < multi.InstanceCount; i++)
        {
            var angle = (float)random.NextDouble() * Mathf.Tau;
            var height = 30f + (float)random.NextDouble() * 38f;
            var radius = 62f + (float)random.NextDouble() * 16f;
            multi.SetInstanceTransform(i, new Transform3D(Basis.Identity, new Vector3(Mathf.Cos(angle) * radius, height, Mathf.Sin(angle) * radius)));
        }
        _stars = new MultiMeshInstance3D { Multimesh = multi, MaterialOverride = _starsMaterial, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(_stars);
    }

    void BuildForestHorizon()
    {
        var material = new StandardMaterial3D { AlbedoColor = new Color("15261d"), Roughness = 1 };
        var multi = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = new CylinderMesh { TopRadius = 0, BottomRadius = .5f, Height = 1, RadialSegments = 7 }, InstanceCount = 52 };
        var random = new Random(4207);
        for (int i = 0; i < multi.InstanceCount; i++)
        {
            var angle = i / (float)multi.InstanceCount * Mathf.Tau;
            var width = 5f + (float)random.NextDouble() * 4f;
            var height = 12f + (float)random.NextDouble() * 12f;
            var radius = 68f + (float)random.NextDouble() * 9f;
            var basis = Basis.Identity.Scaled(new Vector3(width, height, width));
            multi.SetInstanceTransform(i, new Transform3D(basis, new Vector3(Mathf.Cos(angle) * radius, height / 2 - .1f, Mathf.Sin(angle) * radius)));
        }
        _forestHorizon = new MultiMeshInstance3D { Name = "ForestHorizon", Multimesh = multi, MaterialOverride = material, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(_forestHorizon);
    }

    public override void _Process(double delta)
    {
        if (Follow != null)
        {
            var snap = ChunkGenerator.ChunkSize;
            Position = new(Mathf.Round(Follow.GlobalPosition.X / snap) * snap, 0, Mathf.Round(Follow.GlobalPosition.Z / snap) * snap);
        }
        ApplyTime();
        _artificialTimer += (float)delta;
        if (_artificialTimer >= .5f) { _artificialTimer = 0; UpdateArtificialLights(); }
    }

    void ApplyTime()
    {
        float hour = Time?.Hour ?? 12;
        float sunAmount = Mathf.SmoothStep(0, 1, Mathf.Clamp(Mathf.Sin((hour - 6) / 24f * Mathf.Tau) * 1.6f, 0, 1));
        float sunset = Math.Max(Mathf.Exp(-Mathf.Pow((hour - 18.5f) / 1.5f, 2)), Mathf.Exp(-Mathf.Pow((hour - 5.5f) / 1.4f, 2)));
        var nightTop = new Color("071225"); var dayTop = new Color("3184c7"); var sunsetTop = new Color("5b527c");
        var nightHorizon = new Color("121a35"); var dayHorizon = new Color("b8daf0"); var sunsetHorizon = new Color("f08b68");
        _skyMaterial.SkyTopColor = nightTop.Lerp(dayTop, sunAmount).Lerp(sunsetTop, sunset * .55f);
        _skyMaterial.SkyHorizonColor = nightHorizon.Lerp(dayHorizon, sunAmount).Lerp(sunsetHorizon, sunset * .85f);
        _skyMaterial.GroundBottomColor = new Color("07101c").Lerp(new Color("43556b"), sunAmount);
        _skyMaterial.GroundHorizonColor = _skyMaterial.SkyHorizonColor.Darkened(.25f);
        _environment.AmbientLightColor = new Color("66789f").Lerp(new Color("c5d5e3"), sunAmount).Lerp(new Color("ffb186"), sunset * .3f);
        _environment.AmbientLightEnergy = Mathf.Lerp(.22f, .78f, sunAmount);
        _environment.FogLightColor = new Color("415b51").Lerp(new Color("b7c9bd"), sunAmount).Lerp(new Color("d79a78"), sunset * .25f);
        _environment.FogLightEnergy = Mathf.Lerp(.45f, .9f, sunAmount);
        var angle = (hour - 6) / 24f * Mathf.Tau;
        var direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), .35f).Normalized();
        _sun.Position = direction * 68; _moon.Position = -direction * 68;
        _sun.Visible = direction.Y > -.05f; _moon.Visible = direction.Y < .2f;
        _sunLight.Rotation = new Vector3(angle - Mathf.Pi / 2, -.4f, 0);
        _sunLight.LightColor = new Color("ffe4c4").Lerp(new Color("ff9a69"), sunset * .75f);
        _sunLight.LightEnergy = Mathf.Lerp(.03f, 1.05f, sunAmount);
        var cloudColor = new Color("66718a").Lerp(Colors.White, sunAmount).Lerp(new Color("ffb093"), sunset * .55f);
        foreach (var material in _cloudMaterials) { material.SetShaderParameter("day_color", cloudColor); material.SetShaderParameter("visibility", Mathf.Lerp(.45f, 1, sunAmount)); }
        var starAlpha = 1 - Mathf.SmoothStep(.05f, .35f, sunAmount);
        _starsMaterial.AlbedoColor = new Color(1, 1, 1, starAlpha); _stars.Visible = starAlpha > .02f;
        
        // Update ambient particles (pollen day, fireflies night)
        if (_particleProcess != null)
        {
            var isNight = hour >= 19 || hour < 5;
            _particleProcess.Color = isNight ? new Color("a7f3d0") : new Color("e2f0a1").Lerp(new Color("f87171"), sunset); // Green fireflies or yellow/orange pollen
            _particleMaterial.EmissionEnabled = isNight;
            if (isNight) { _particleMaterial.Emission = new Color("a7f3d0"); _particleMaterial.EmissionEnergyMultiplier = 4f; }
            _particleProcess.Gravity = isNight ? new Vector3(0, 0.02f, 0) : new Vector3(0, -0.05f, 0); // Fireflies float up, pollen falls
        }
    }

    void UpdateArtificialLights()
    {
        bool enabled = Time.Hour >= 18.25f || Time.Hour < 6.25f;
        foreach (var node in GetTree().GetNodesInGroup("artificial_lights")) if (node is Light3D light) light.Visible = enabled;
    }
}
