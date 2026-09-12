using Godot;

namespace Heartbeat;

public partial class CheckerBackdrop : Control
{
    public int Mode;
    public override void _Draw()
    {
        if (Mode != 0) { DrawRect(new Rect2(Vector2.Zero, Size), Mode == 1 ? new Color("eeeeee") : new Color("18202a")); return; }
        for (int y = 0; y < Size.Y; y += 20)
            for (int x = 0; x < Size.X; x += 20)
                DrawRect(new Rect2(x, y, 20, 20), new Color((x / 20 + y / 20) % 2 == 0 ? "505661" : "737984"));
    }
}

// Brush edits the alpha mask, never the stored source. One undo snapshot per stroke.
public partial class CutoutCanvas : CheckerBackdrop
{
    Image? _original, _mask, _result;
    byte[]? _undo;
    ImageTexture? _texture;
    Vector2? _previous;
    public bool Restore { get; set; }
    public float BrushSize { get; set; } = 18;
    public bool Dirty { get; private set; }
    public Action? Edited;
    Rect2 ImageRect => _result == null ? new() : Fit(Size, new Vector2(_result.GetWidth(), _result.GetHeight()));
    static Rect2 Fit(Vector2 space, Vector2 image)
    {
        var scale = Math.Min(space.X / image.X, space.Y / image.Y);
        return new Rect2((space - image * scale) / 2, image * scale);
    }

    public void LoadImages(Image original, Image cutout)
    {
        Release();
        _original = Image.CreateFromData(original.GetWidth(), original.GetHeight(), false, Image.Format.Rgba8, original.GetData());
        _result = Image.CreateFromData(original.GetWidth(), original.GetHeight(), false, Image.Format.Rgba8, original.GetData());
        _mask = Image.CreateEmpty(original.GetWidth(), original.GetHeight(), false, Image.Format.Rgba8);
        // Read alpha as mask coverage relative to source alpha; restoring keeps original transparency.
        for (int y = 0; y < _mask.GetHeight(); y++)
            for (int x = 0; x < _mask.GetWidth(); x++)
            {
                var oa = original.GetPixel(x, y).A;
                var a = cutout.GetPixel(Math.Min(x, cutout.GetWidth() - 1), Math.Min(y, cutout.GetHeight() - 1)).A;
                var coverage = oa > .001f ? Math.Clamp(a / oa, 0, 1) : 0;
                _mask.SetPixel(x, y, new Color(coverage, coverage, coverage, 1));
            }
        Dirty = false; UpdateTexture();
    }

    void UpdateTexture()
    {
        if (_original == null || _mask == null || _result == null) return;
        for (int y = 0; y < _result.GetHeight(); y++)
            for (int x = 0; x < _result.GetWidth(); x++)
            { var c = _original.GetPixel(x, y); c.A *= _mask.GetPixel(x, y).R; _result.SetPixel(x, y, c); }
        if (_texture == null) _texture = ImageTexture.CreateFromImage(_result); else _texture.Update(_result);
        QueueRedraw();
    }

    public override void _Draw() { base._Draw(); if (_texture != null) DrawTextureRect(_texture, ImageRect, false); }
    public void BeginStroke() { if (_mask != null) _undo = _mask.GetData(); }
    public void PaintAt(Vector2 imagePoint, float radius)
    {
        if (_mask == null || _original == null || _result == null) return;
        var minX = Math.Max(0, (int)(imagePoint.X - radius)); var maxX = Math.Min(_mask.GetWidth() - 1, (int)(imagePoint.X + radius));
        var minY = Math.Max(0, (int)(imagePoint.Y - radius)); var maxY = Math.Min(_mask.GetHeight() - 1, (int)(imagePoint.Y + radius));
        for (int y = minY; y <= maxY; y++) for (int x = minX; x <= maxX; x++)
        {
            var distance = new Vector2(x, y).DistanceTo(imagePoint);
            if (distance > radius) continue;
            var strength = Math.Clamp(radius - distance, 0, 1);
            var a = Mathf.Lerp(_mask.GetPixel(x, y).R, Restore ? 1 : 0, strength);
            _mask.SetPixel(x, y, new Color(a, a, a, 1));
            var c = _original.GetPixel(x, y); c.A *= a; _result.SetPixel(x, y, c);
        }
        _texture?.Update(_result); Dirty = true; QueueRedraw(); Edited?.Invoke();
    }
    public override void _GuiInput(InputEvent e)
    {
        if (_result == null) return;
        if (e is InputEventMouseButton b && b.ButtonIndex == MouseButton.Left)
        {
            if (!b.Pressed) { _previous = null; return; }
            if (!ImageRect.HasPoint(b.Position)) return;
            BeginStroke(); _previous = null; Stroke(b.Position); AcceptEvent();
        }
        if (e is InputEventMouseMotion m && (m.ButtonMask & MouseButtonMask.Left) != 0)
        { Stroke(m.Position); AcceptEvent(); }
    }
    void Stroke(Vector2 point)
    {
        var rect = ImageRect;
        var p = (point - rect.Position) * (_result!.GetWidth() / rect.Size.X);
        var radius = BrushSize * _result.GetWidth() / rect.Size.X / 2;
        var from = _previous ?? p;
        int count = Math.Max(1, (int)(from.DistanceTo(p) / Math.Max(1, radius / 2)));
        for (int i = 1; i <= count; i++) PaintAt(from.Lerp(p, i / (float)count), radius);
        _previous = p;
    }
    public void Undo()
    {
        if (_undo == null || _mask == null) return;
        var restored = Image.CreateFromData(_mask.GetWidth(), _mask.GetHeight(), false, Image.Format.Rgba8, _undo);
        _mask.Dispose(); _mask = restored; _undo = null; Dirty = true; UpdateTexture(); Edited?.Invoke();
    }
    public void SaveTo(CharacterData data, OutfitPose pose)
    {
        if (!Dirty || _result == null || _mask == null) return;
        pose.Cutout = Wardrobe.Store(data, _result, "cutouts"); pose.Mask = Wardrobe.Store(data, _mask, "masks"); Dirty = false;
    }
    void Release() { _original?.Dispose(); _mask?.Dispose(); _result?.Dispose(); _texture?.Dispose(); _original = _mask = _result = null; _texture = null; _undo = null; }
    public override void _ExitTree() => Release();
}
