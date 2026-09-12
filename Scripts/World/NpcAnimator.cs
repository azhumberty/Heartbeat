using Godot;

namespace Heartbeat;

/// <summary>
/// Dedicated animation component for NPC sprites.
/// Handles sprite sheet frame cycling, directional resolution relative to camera,
/// fallback chains, and synchronization of animation speed with movement.
/// </summary>
public partial class NpcAnimator : Node3D
{
    // ── Configuration ──
    public CharacterData Data { get; set; } = new();
    public string OutfitId { get; set; } = "";
    MeshInstance3D? _outfitMesh;
    ShaderMaterial? _outfitMaterial;
    CharacterOutfit? _outfit;
    readonly List<Texture2D> _outfitTextures = new();
    string _simpleState = "idle";
    float _moveSpeed;
    double _simpleTime;
    public bool UsesOutfit => _outfitMesh?.Visible == true;
    public string ResolvedOutfitId => _outfit?.Id ?? "";
    public float HeightMeters => Math.Max(0.5f, Data.HeightMeters * Data.VisualScale);

    // ── Visual nodes ──
    Sprite3D _sprite = null!;
    MeshInstance3D _shadow = null!;
    float _footY;

    // ── Animation state ──
    readonly Dictionary<string, AnimationEntry> _animations = new();
    readonly Dictionary<string, Texture2D> _sheetCache = new();
    string _currentAnim = "";
    int _currentFrame;
    float _frameTimer;
    string _lastDirection = "front";
    float _directionHysteresis; // angle in radians to prevent rapid flipping

    // ── Breathing ──
    double _breathTime;
    bool _breathingEnabled = true;

    // ── Fallback portrait mode ──
    bool _legacyMode;
    Texture2D? _legacyTexture;

    sealed class AnimationEntry
    {
        public Texture2D Sheet = null!;
        public int Columns, Rows, StartFrame, FrameCount, Margin, Spacing;
        public float Fps, PivotX, PivotY, OffsetX, OffsetY;
        public bool Loop;
        public int SheetWidthPx, SheetHeightPx;
        public int FrameWidth => (SheetWidthPx - Margin * 2 - Spacing * (Math.Max(1, Columns) - 1)) / Math.Max(1, Columns);
        public int FrameHeight => (SheetHeightPx - Margin * 2 - Spacing * (Math.Max(1, Rows) - 1)) / Math.Max(1, Rows);
    }

    public override void _Ready()
    {
        _sprite = new Sprite3D
        {
            Billboard = BaseMaterial3D.BillboardModeEnum.FixedY,
            Shaded = false,
            AlphaCut = SpriteBase3D.AlphaCutMode.Discard,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmaps,
            RenderPriority = 1
        };
        AddChild(_sprite);

        // Contact shadow — flat dark ellipse on the ground
        var shadowMesh = new QuadMesh { Size = new Vector2(0.6f, 0.3f) };
        _shadow = new MeshInstance3D
        {
            Mesh = shadowMesh,
            Position = new Vector3(0, 0.02f, 0),
            RotationDegrees = new Vector3(-90, 0, 0),
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0, 0, 0, 0.25f),
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled
            }
        };
        AddChild(_shadow);

        LoadAnimations();
        LoadOutfit();
    }

    void LoadOutfit()
    {
        _outfitTextures.Clear();
        _outfit = Wardrobe.Resolve(Data, OutfitId);
        if (_outfitMesh != null) _outfitMesh.Visible = false;
        _sprite.Visible = true;
        if (_outfit == null || _outfit.UseLegacy || string.IsNullOrEmpty(_outfit.Idle.Cutout)) return;
        try
        {
            using var idle = Wardrobe.Bake(Data, _outfit, _outfit.Idle);
            _outfitTextures.Add(ImageTexture.CreateFromImage(idle));
            if (!string.IsNullOrEmpty(_outfit.Left.Cutout) && !string.IsNullOrEmpty(_outfit.Right.Cutout))
                foreach (var pose in new[] { _outfit.Left, _outfit.Right })
                {
                    using var frame = Wardrobe.Bake(Data, _outfit, pose);
                    _outfitTextures.Add(ImageTexture.CreateFromImage(frame));
                }
        }
        catch (Exception) { GD.PushWarning("[Guarda-roupa] Imagem ausente/inválida; usando visual disponível."); }
        if (_outfitTextures.Count == 0) return;
        if (_outfitMesh == null)
        {
            _outfitMaterial = new ShaderMaterial { Shader = new Shader { Code = """
                shader_type spatial;
                render_mode unshaded, cull_disabled;
                uniform sampler2D portrait : source_color, filter_linear;
                uniform float phase = 0.0;
                uniform float breath = 0.003;
                uniform float talk = 0.0;
                void vertex() {
                    float torso = smoothstep(0.15, 0.35, UV.y) * (1.0 - smoothstep(0.52, 0.67, UV.y));
                    VERTEX.x *= 1.0 + sin(phase * 1.5) * breath * torso;
                    VERTEX.x += sin(phase * 0.9) * talk * torso;
                }
                void fragment() {
                    vec4 c = texture(portrait, UV);
                    ALBEDO = c.rgb; ALPHA = c.a; ALPHA_SCISSOR_THRESHOLD = 0.1;
                }
                """ } };
            _outfitMesh = new MeshInstance3D { MaterialOverride = _outfitMaterial };
            AddChild(_outfitMesh);
        }
        var canvasHeight = HeightMeters / Wardrobe.BodyFraction(Data, _outfit);
        _outfitMesh.Mesh = new QuadMesh { Size = new(canvasHeight * .5f, canvasHeight), SubdivideWidth = 8, SubdivideDepth = 16 };
        _outfitMesh.Position = new(0, canvasHeight * (962f / 1024 - .5f) + Data.FootOffset + _outfit.FootOffset, 0);
        _outfitMaterial!.SetShaderParameter("portrait", _outfitTextures[0]);
        _outfitMaterial.SetShaderParameter("breath", Data.EnableBreathing ? Math.Clamp(_outfit.Breathing, 0, .02f) : 0);
        _outfitMesh.Visible = true; _sprite.Visible = false;
        _currentFrame = 0; _simpleTime = 0;
    }

    /// <summary>Loads all sprite animation definitions from CharacterData.</summary>
    void LoadAnimations()
    {
        _animations.Clear();

        if (Data.SpriteAnimations.Count == 0)
        {
            // Legacy mode: use portrait cache for a static texture
            _legacyMode = true;
            _breathingEnabled = Data.EnableBreathing;
            return;
        }

        _legacyMode = false;
        foreach (var def in Data.SpriteAnimations)
        {
            var texture = LoadSheet(def.ImagePath);
            if (texture == null) continue;

            _animations[def.Name] = new AnimationEntry
            {
                Sheet = texture,
                Columns = Math.Max(1, def.Columns),
                Rows = Math.Max(1, def.Rows),
                StartFrame = Math.Max(0, def.StartFrame),
                FrameCount = Math.Max(1, def.FrameCount),
                Margin = Math.Max(0, def.Margin),
                Spacing = Math.Max(0, def.Spacing),
                Fps = Math.Max(0.1f, def.Fps),
                PivotX = def.PivotX,
                PivotY = def.PivotY,
                OffsetX = def.OffsetX,
                OffsetY = def.OffsetY,
                Loop = def.Loop,
                SheetWidthPx = texture.GetWidth(),
                SheetHeightPx = texture.GetHeight()
            };
        }

        // Disable breathing if we have an idle animation
        _breathingEnabled = !_animations.Keys.Any(k => k.StartsWith("idle_")) && Data.EnableBreathing;

        if (_animations.Count > 0)
            PlayAnimation("idle_front");
    }

    Texture2D? LoadSheet(string imagePath)
    {
        if (string.IsNullOrEmpty(imagePath)) return null;
        if (_sheetCache.TryGetValue(imagePath, out var cached)) return cached;

        // Try character package folder
        string fullPath;
        if (imagePath.StartsWith("user://") || imagePath.StartsWith("res://"))
            fullPath = ProjectSettings.GlobalizePath(imagePath);
        else
            fullPath = ProjectSettings.GlobalizePath($"user://Characters/{Data.Id}/{imagePath}");

        if (!File.Exists(fullPath))
        {
            // Try absolute path as-is
            fullPath = imagePath;
            if (!File.Exists(fullPath))
            {
                GD.PushWarning($"[NpcAnimator] Sprite sheet not found: {imagePath}");
                return null;
            }
        }

        var image = Image.LoadFromFile(fullPath);
        if (image == null || image.IsEmpty())
        {
            GD.PushWarning($"[NpcAnimator] Failed to load image: {fullPath}");
            return null;
        }

        // Preserve original pixels — NO auto-crop on sheets
        var tex = ImageTexture.CreateFromImage(image);
        _sheetCache[imagePath] = tex;
        return tex;
    }

    /// <summary>Sets the legacy portrait texture for portrait-only characters.</summary>
    public void SetLegacyTexture(Texture2D? texture)
    {
        _legacyTexture = texture;
        if (_legacyMode && texture != null)
        {
            _sprite.Texture = texture;
            FitSprite();
        }
    }

    /// <summary>Refreshes legacy texture (called when expression changes).</summary>
    public void RefreshLegacy(Texture2D? texture)
    {
        if (!_legacyMode) return;
        if (texture == null || _sprite.Texture == texture) return;
        var tween = CreateTween();
        tween.TweenProperty(_sprite, "modulate:a", 0f, .12);
        tween.TweenCallback(Callable.From(() => { _sprite.Texture = texture; FitSprite(); }));
        tween.TweenProperty(_sprite, "modulate:a", 1f, .18);
    }

    /// <summary>Fits sprite to character height with feet on ground.</summary>
    void FitSprite()
    {
        if (_sprite.Texture == null) return;
        var texH = _sprite.Texture.GetHeight();
        if (texH <= 0) return;

        _sprite.PixelSize = HeightMeters / texH;
        _footY = Data.FootOffset;
        _sprite.Position = new Vector3(0, HeightMeters / 2f + _footY, 0);
        _shadow.Position = new Vector3(0, 0.02f + _footY, 0);
        _shadow.Scale = new Vector3(Data.CollisionRadius * 2f, Data.CollisionRadius * 2f, 1f);
    }

    /// <summary>Updates direction based on NPC world angle relative to camera.</summary>
    public void UpdateDirection(Vector3 npcForward, Camera3D? camera)
    {
        if (UsesOutfit || _legacyMode || camera == null) return;

        // Compute angle from camera to NPC facing direction
        var camForward = -camera.GlobalTransform.Basis.Z;
        camForward.Y = 0;
        camForward = camForward.Normalized();

        npcForward.Y = 0;
        if (npcForward.LengthSquared() < 0.001f)
            npcForward = -camForward; // facing camera = front

        npcForward = npcForward.Normalized();

        // Angle between camera forward and NPC forward (projected on XZ plane)
        var dot = camForward.Dot(npcForward);
        var cross = camForward.Cross(npcForward).Y;

        // Determine direction with hysteresis (15 degrees = ~0.26 rad)
        const float hysteresis = 0.22f;
        string dir;

        if (dot > 0.5f + hysteresis) dir = "front"; // NPC faces toward camera
        else if (dot < -0.5f - hysteresis) dir = "back"; // NPC faces away from camera
        else if (cross > hysteresis) dir = "right";
        else if (cross < -hysteresis) dir = "left";
        else dir = _lastDirection; // within hysteresis zone, keep current

        if (dir != _lastDirection)
        {
            _lastDirection = dir;
            // Update animation to match new direction
            var prefix = _currentAnim.Contains("walk") ? "walk" :
                         _currentAnim.Contains("talk") ? "talk" : "idle";
            PlayAnimation($"{prefix}_{dir}");
        }
    }

    /// <summary>Plays the named animation with fallback chain.</summary>
    public void PlayAnimation(string name)
    {
        var resolved = ResolveAnimation(name);
        if (resolved == _currentAnim && _animations.ContainsKey(resolved)) return;

        _currentAnim = resolved;
        _currentFrame = 0;
        _frameTimer = 0;

        if (_animations.TryGetValue(resolved, out var entry))
        {
            _sprite.Texture = entry.Sheet;
            _sprite.RegionEnabled = true;
            UpdateFrame(entry);
            FitFrameSprite(entry);
        }
    }

    /// <summary>Resolves animation name through fallback chain.</summary>
    string ResolveAnimation(string name)
    {
        if (_animations.ContainsKey(name)) return name;

        // Fallback: talk_X → idle_X
        if (name.StartsWith("talk_"))
        {
            var idleName = "idle_" + name[5..];
            if (_animations.ContainsKey(idleName)) return idleName;
        }

        // Fallback: idle_X can use walk_X frame 0
        if (name.StartsWith("idle_"))
        {
            var walkName = "walk_" + name[5..];
            if (_animations.ContainsKey(walkName)) return walkName;
        }

        // Fallback: try front variant
        var parts = name.Split('_');
        if (parts.Length >= 2)
        {
            var frontName = parts[0] + "_front";
            if (_animations.ContainsKey(frontName)) return frontName;
        }

        // Fallback: any animation
        return _animations.Keys.FirstOrDefault() ?? name;
    }

    /// <summary>Sets the current state animation prefix: idle, walk, talk.</summary>
    public void SetState(string state, float moveSpeed = 0f)
    {
        _simpleState = state.ToLowerInvariant();
        _moveSpeed = moveSpeed;
        if (UsesOutfit) return;
        var prefix = state.ToLowerInvariant() switch
        {
            "walking" => "walk",
            "talking" => "talk",
            "working" => "idle",
            "resting" => "idle",
            "interacting" => "idle",
            _ => "idle"
        };
        var target = $"{prefix}_{_lastDirection}";
        PlayAnimation(target);
    }

    void FitFrameSprite(AnimationEntry entry)
    {
        if (entry.FrameHeight <= 0) return;
        _sprite.PixelSize = HeightMeters / entry.FrameHeight;
        _footY = Data.FootOffset + entry.OffsetY;
        // Godot Sprite3D offset is in pixels.
        // Pivot (0.5, 0.5) is default (center).
        // If PivotX = 0, we want to shift right by half width.
        // Offset is shift of the texture relative to the 3D position.
        var px = (0.5f - entry.PivotX) * entry.FrameWidth;
        var py = (0.5f - entry.PivotY) * entry.FrameHeight;
        _sprite.Offset = new Vector2(px, py);
        
        // The position should represent the pivot in 3D world space.
        // Since PivotY=1.0 usually means the feet are at y=0.
        // Wait, HeightMeters is the full sprite height.
        // If pivot is 1.0 (bottom), the 3D position Y should be just _footY.
        // Let's use standard bottom-center pivot logic:
        _sprite.Position = new Vector3(entry.OffsetX, _footY, 0);
    }

    void UpdateFrame(AnimationEntry entry)
    {
        var frameIndex = entry.StartFrame + _currentFrame;
        var col = frameIndex % entry.Columns;
        var row = frameIndex / entry.Columns;
        var fw = entry.FrameWidth;
        var fh = entry.FrameHeight;
        var x = entry.Margin + col * (fw + entry.Spacing);
        var y = entry.Margin + row * (fh + entry.Spacing);

        _sprite.RegionEnabled = true;
        _sprite.RegionRect = new Rect2(x, y, fw, fh);
    }

    public bool Paused { get; set; }

    public int CurrentFrame
    {
        get => _currentFrame;
        set
        {
            if (!_animations.TryGetValue(_currentAnim, out var entry)) return;
            _currentFrame = value;
            if (_currentFrame >= entry.FrameCount) _currentFrame = 0;
            if (_currentFrame < 0) _currentFrame = entry.FrameCount - 1;
            UpdateFrame(entry);
        }
    }

    public override void _Process(double delta)
    {
        if (UsesOutfit)
        {
            if (!Paused) _simpleTime += delta;
            var camera = GetViewport().GetCamera3D();
            if (camera != null)
            {
                var direction = camera.GlobalPosition - GlobalPosition;
                _outfitMesh!.Rotation = new(0, Mathf.Atan2(direction.X, direction.Z), 0);
            }
            if (!Paused)
            {
                _currentFrame = _simpleState == "walking" && _moveSpeed > .01f && _outfitTextures.Count == 3
                    ? 1 + (int)(_simpleTime * Math.Clamp(_outfit!.Fps, 1, 12) * _moveSpeed / 1.4f) % 2 : 0;
                _outfitMaterial!.SetShaderParameter("portrait", _outfitTextures[_currentFrame]);
                _outfitMaterial.SetShaderParameter("phase", (float)_simpleTime);
                _outfitMaterial.SetShaderParameter("talk", _simpleState == "talking" ? .004f : 0f);
            }
            return;
        }
        if (_legacyMode)
        {
            ProcessBreathing(delta);
            return;
        }

        if (!_animations.TryGetValue(_currentAnim, out var entry)) return;

        if (!Paused)
        {
            // Frame cycling
            _frameTimer += (float)delta;
            var frameDuration = 1f / entry.Fps;
            if (_frameTimer >= frameDuration)
            {
                _frameTimer -= frameDuration;
                _currentFrame++;
                if (_currentFrame >= entry.FrameCount)
                {
                    _currentFrame = entry.Loop ? 0 : entry.FrameCount - 1;
                }
                UpdateFrame(entry);
            }
        }

        ProcessBreathing(delta);
    }

    void ProcessBreathing(double delta)
    {
        if (!_breathingEnabled)
        {
            _sprite.Scale = Vector3.One;
            return;
        }

        _breathTime += delta;
        // Minimal breathing — small vertical scale only, no horizontal or positional wobble
        var breath = 1f + Mathf.Sin((float)_breathTime * 1.5f) * 0.004f;
        _sprite.Scale = new Vector3(1, breath, 1);
        // Keep foot position stable — offset the Y to compensate for scale
        // (sprite pivots from center, so scaling changes apparent foot position slightly)
    }

    /// <summary>Whether this animator has real frame animations (not legacy portrait).</summary>
    public bool HasAnimations => !_legacyMode && _animations.Count > 0;

    /// <summary>Current animation direction (front/back/left/right).</summary>
    public string CurrentDirection => _lastDirection;

    /// <summary>Reload animations after CharacterData changes.</summary>
    public void Reload()
    {
        _sheetCache.Clear();
        _animations.Clear();
        LoadAnimations();
        LoadOutfit();
    }
}
