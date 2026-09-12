using Godot;
namespace Heartbeat;

/// <summary>
/// Visual representation of an NPC in the world.
/// Manages collision, animation, walking to destinations, and interaction states.
/// Logical identity and persistence are handled by NpcManager.
/// </summary>
public partial class NpcActor : Node3D
{
    public CharacterData Data { get; set; } = new();
    public CharacterState State { get; set; } = new();
    public PortraitCache Portraits { get; set; } = new();

    // ── Components ──
    NpcAnimator _animator = null!;
    Label3D _nameLabel = null!;
    StaticBody3D _body = null!;
    CollisionShape3D _collision = null!;

    // ── Movement ──
    Vector3 _destination;
    bool _hasDestination;
    Vector3 _moveDirection;
    public bool IsTalking { get; set; }

    // ── State tracking ──
    string _currentState = "Idle";
    Camera3D? _camera;

    public override void _Ready()
    {
        // Animator component
        var outfit = Wardrobe.Resolve(Data, State.CurrentOutfitId);
        if (!string.IsNullOrEmpty(State.CurrentOutfitId) && outfit?.Id != State.CurrentOutfitId)
            GD.PushWarning("[Guarda-roupa] Roupa ausente; usando a padrão.");
        State.CurrentOutfitId = outfit?.Id ?? "";
        _animator = new NpcAnimator { Data = Data, OutfitId = State.CurrentOutfitId };
        AddChild(_animator);

        // Name label above head
        var labelHeight = Data.HeightMeters * Data.VisualScale + 0.25f + Data.FootOffset;
        _nameLabel = new Label3D
        {
            Text = Data.Name,
            Position = new(0, labelHeight, 0),
            FontSize = 32,
            PixelSize = .006f,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            Modulate = new Color("ffd6e8")
        };
        AddChild(_nameLabel);

        // Collision body
        var height = Math.Max(0.5f, Data.HeightMeters * Data.VisualScale);
        _body = new StaticBody3D();
        _collision = new CollisionShape3D
        {
            Shape = new CylinderShape3D
            {
                Radius = Data.CollisionRadius,
                Height = height
            },
            Position = new(0, height / 2f + Data.FootOffset, 0)
        };
        _body.AddChild(_collision);
        AddChild(_body);

        // Set legacy portrait if no animations
        if (!_animator.HasAnimations)
        {
            _animator.SetLegacyTexture(Portraits.Get(Data, State));
        }

        _destination = Position;
    }

    /// <summary>Set movement destination. NPC will walk there.</summary>
    public void WalkTo(Vector3 target)
    {
        if (IsTalking) return;
        _destination = new Vector3(target.X, Position.Y, target.Z);
        _hasDestination = true;
        SetNpcState("Walking");
    }

    /// <summary>Stop walking and go to idle.</summary>
    public void StopWalking()
    {
        _hasDestination = false;
        _moveDirection = Vector3.Zero;
        SetNpcState("Idle");
    }

    /// <summary>Pause movement for dialogue.</summary>
    public void PauseForDialogue()
    {
        IsTalking = true;
        _hasDestination = false;
        _moveDirection = Vector3.Zero;
        SetNpcState("Talking");
    }

    /// <summary>Resume activity after dialogue ends.</summary>
    public void ResumeAfterDialogue()
    {
        IsTalking = false;
        SetNpcState(State.CurrentActivity == "Walking" ? "Walking" : "Idle");
    }

    void SetNpcState(string state)
    {
        _currentState = state;
        State.CurrentActivity = state;
        _animator.SetState(state, state == "Walking" ? Data.WalkSpeed : 0);
    }

    public override void _Process(double delta)
    {
        // Find camera for direction resolution
        _camera ??= GetViewport()?.GetCamera3D();

        if (IsTalking)
        {
            // When talking, face the camera
            _animator.UpdateDirection(-Vector3.Forward, _camera);
            return;
        }

        // Walking logic
        if (_hasDestination)
        {
            var toTarget = _destination - Position;
            toTarget.Y = 0;
            var distance = toTarget.Length();

            if (distance < 0.3f)
            {
                // Arrived
                _hasDestination = false;
                _moveDirection = Vector3.Zero;
                SetNpcState("Idle");
            }
            else
            {
                _moveDirection = toTarget.Normalized();
                var speed = Data.WalkSpeed * (float)delta;
                Position += _moveDirection * Math.Min(speed, distance);
                _animator.SetState("Walking", (float)delta > 0 ? Math.Min(speed, distance) / (float)delta : 0);

                // Update logical position
                State.WorldX = Position.X;
                State.WorldZ = Position.Z;

                // Update animator direction
                _animator.UpdateDirection(_moveDirection, _camera);
                if (_currentState != "Walking")
                    SetNpcState("Walking");
            }
        }
        else if (_currentState == "Walking")
        {
            SetNpcState("Idle");
        }

        // Update direction even when idle (camera may rotate)
        if (_currentState != "Walking" && _camera != null)
        {
            _animator.UpdateDirection(_moveDirection.LengthSquared() > 0.001f ? _moveDirection : -Vector3.Forward, _camera);
        }
    }

    /// <summary>Refresh portrait for legacy mode (expression change during dialogue).</summary>
    public void RefreshPortrait()
    {
        var texture = Portraits.Get(Data, State);
        _animator.RefreshLegacy(texture);
    }

    /// <summary>Whether this NPC has reached its destination or has no destination.</summary>
    public bool IsAtDestination => !_hasDestination;

    /// <summary>Current visual state.</summary>
    public string CurrentNpcState => _currentState;
}
