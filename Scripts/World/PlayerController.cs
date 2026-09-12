using Godot;
namespace Heartbeat;

/// <summary>Stable first-person controller. The player has collision but no rendered body or hands.</summary>
public partial class PlayerController : CharacterBody3D
{
    public bool Locked { get; set; }
    [Export] public float WalkSpeed { get; set; } = 4.2f;
    [Export] public float RunSpeed { get; set; } = 6.5f;
    [Export] public float MouseSensitivity { get; set; } = .0023f;
    [Export] public float JumpVelocity { get; set; } = 5.3f;
    [Export] public float GroundAcceleration { get; set; } = 18f;
    [Export] public float GroundDeceleration { get; set; } = 22f;
    [Export] public float AirAcceleration { get; set; } = 5f;
    public bool FirstPerson => true;
    public float CameraYRotation => _yaw?.Rotation.Y ?? 0;
    public Camera3D ActiveCamera => _camera;
    Node3D _yaw = null!, _pitch = null!;
    Camera3D _camera = null!;
    RayCast3D _interactor = null!;
    float _jumpBuffer;
    float _groundGrace;

    public override void _Ready()
    {
        AddChild(new CollisionShape3D { Shape = new CapsuleShape3D { Radius = .3f, Height = 1.8f }, Position = new(0, .9f, 0) });
        _yaw = new Node3D { Position = new(0, 1.62f, 0) }; AddChild(_yaw);
        _pitch = new Node3D(); _yaw.AddChild(_pitch);
        _camera = new Camera3D { Current = true, Fov = 72, Near = .04f }; _pitch.AddChild(_camera);
        _interactor = new RayCast3D { TargetPosition = new(0, 0, -3.2f), CollideWithAreas = true, CollideWithBodies = true, Enabled = true };
        _camera.AddChild(_interactor); _interactor.AddException(this);
    }

    public void SetInitialView(bool ignored, float yaw) => _yaw.Rotation = new(0, yaw, 0);

    public NpcActor? AimedNpc()
    {
        _interactor.ForceRaycastUpdate();
        if (!_interactor.IsColliding()) return null;
        Node? node = _interactor.GetCollider() as Node;
        while (node != null && node is not NpcActor) node = node.GetParent();
        return node as NpcActor;
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventKey escape && escape.Pressed && escape.Keycode == Key.Escape && Input.MouseMode == Input.MouseModeEnum.Captured)
        { Input.MouseMode = Input.MouseModeEnum.Visible; GetViewport().SetInputAsHandled(); return; }
        if (Locked) return;
        if (e is InputEventKey jump && jump.Pressed && !jump.Echo && jump.PhysicalKeycode == Key.Space)
        { _jumpBuffer = .14f; GetViewport().SetInputAsHandled(); return; }
        if (e is InputEventMouseButton button && button.Pressed && button.ButtonIndex == MouseButton.Left)
        { Input.MouseMode = Input.MouseModeEnum.Captured; return; }
        if (e is InputEventMouseMotion motion && Input.MouseMode == Input.MouseModeEnum.Captured)
        {
            _yaw.Rotation = new(0, _yaw.Rotation.Y - motion.Relative.X * MouseSensitivity, 0);
            _pitch.Rotation = new(Mathf.Clamp(_pitch.Rotation.X - motion.Relative.Y * MouseSensitivity, -1.35f, 1.25f), 0, 0);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        float d = (float)delta;
        Vector2 input = Vector2.Zero;
        if (!Locked) input = new((Input.IsPhysicalKeyPressed(Key.D) ? 1 : 0) - (Input.IsPhysicalKeyPressed(Key.A) ? 1 : 0), (Input.IsPhysicalKeyPressed(Key.S) ? 1 : 0) - (Input.IsPhysicalKeyPressed(Key.W) ? 1 : 0));
        input = input.LimitLength();
        var dir = new Vector3(input.X, 0, input.Y).Rotated(Vector3.Up, _yaw.Rotation.Y);
        float speed = Input.IsPhysicalKeyPressed(Key.Shift) ? RunSpeed : WalkSpeed;
        bool grounded = IsOnFloor();
        _jumpBuffer = Math.Max(0, _jumpBuffer - d);
        _groundGrace = grounded ? .1f : Math.Max(0, _groundGrace - d);
        float vertical = grounded ? -.15f : Velocity.Y - 20f * d;
        if (_jumpBuffer > 0 && _groundGrace > 0)
        {
            vertical = JumpVelocity;
            _jumpBuffer = 0;
            _groundGrace = 0;
        }
        float acceleration = dir.LengthSquared() > 0 ? (grounded ? GroundAcceleration : AirAcceleration) : GroundDeceleration;
        Velocity = new(Mathf.MoveToward(Velocity.X, dir.X * speed, acceleration * d), vertical, Mathf.MoveToward(Velocity.Z, dir.Z * speed, acceleration * d));
        MoveAndSlide();
        if (Position.Y < -8) Position = new(0, 1, 5);
    }
}
