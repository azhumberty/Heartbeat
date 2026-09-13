using Godot;

namespace Heartbeat;

/// <summary>Novo Jogo and Carregar open this list. Each card is an independent world save.</summary>
public partial class WorldSelectController : Control
{
    public GameSettings Settings { get; set; } = new();
    public Action<GameSave>? Play;
    public Action? Cancelled;
    readonly WorldStore _store = new();
    VBoxContainer _list = null!;
    Label _status = null!;
    Control? _overlay;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var shade = new ColorRect { Color = new Color("03070add") };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(shade);
        var panel = new PanelContainer();
        panel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        panel.OffsetLeft = 70; panel.OffsetRight = -70;
        panel.OffsetTop = 28; panel.OffsetBottom = -28;
        panel.ClipContents = true;
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color("0a1220ee"),
            CornerRadiusTopLeft = 14, CornerRadiusTopRight = 14, CornerRadiusBottomLeft = 14, CornerRadiusBottomRight = 14,
            ContentMarginLeft = 28, ContentMarginRight = 28, ContentMarginTop = 22, ContentMarginBottom = 22
        });
        AddChild(panel);
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 12);
        panel.AddChild(box);
        var header = new HBoxContainer();
        box.AddChild(header);
        var title = Ui.Text("MUNDOS", 30);
        title.AddThemeColorOverride("font_color", new Color("e6c27a"));
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        header.AddChild(title);
        header.AddChild(Ui.Button("← Voltar", () => Cancelled?.Invoke()));
        box.AddChild(Ui.Body("Cada mundo é uma campanha isolada. Criar um mundo não apaga os outros.", 15));
        _status = Ui.Body("", 13);
        _status.AddThemeColorOverride("font_color", new Color("8999aa"));
        box.AddChild(_status);
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        box.AddChild(scroll);
        _list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _list.AddThemeConstantOverride("separation", 10);
        scroll.AddChild(_list);
        Ui.FitScrollChild(scroll, _list);
        box.AddChild(Ui.Button("+  CRIAR NOVO MUNDO", OpenCreate));
        Refresh();
    }

    void Refresh()
    {
        foreach (var child in _list.GetChildren()) { _list.RemoveChild(child); child.QueueFree(); }
        var worlds = _store.List();
        _status.Text = worlds.Count == 0
            ? "Ainda não há mundos. Descreve o lugar onde queres viver."
            : worlds.Count == 1 ? "1 mundo guardado." : $"{worlds.Count} mundos guardados.";
        foreach (var world in worlds)
            _list.AddChild(Card(world));
    }

    Control Card(WorldManifest world)
    {
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color("101925ee"),
            BorderColor = new Color("b9985d66"),
            BorderWidthTop = 1, BorderWidthBottom = 1, BorderWidthLeft = 1, BorderWidthRight = 1,
            CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10, CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10,
            ContentMarginLeft = 16, ContentMarginRight = 16, ContentMarginTop = 12, ContentMarginBottom = 12
        });
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 14);
        panel.AddChild(row);
        var thumb = new ColorRect { CustomMinimumSize = new Vector2(72, 72), Color = new Color("2f6d73") };
        row.AddChild(thumb);
        var col = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        col.AddThemeConstantOverride("separation", 4);
        row.AddChild(col);
        var name = Ui.Text(world.Name, 20);
        name.AddThemeColorOverride("font_color", new Color("f0d080"));
        col.AddChild(name);
        col.AddChild(Ui.Body(Clip(world.Summary, 140), 14));
        var meta = Ui.Body($"seed {world.Seed}  ·  {PlayLabel(world.PlayedSeconds)}  ·  {When(world.LastPlayedAt, world.CreatedAt)}", 12);
        meta.AddThemeColorOverride("font_color", new Color("8595a8"));
        col.AddChild(meta);
        if (!string.IsNullOrWhiteSpace(world.Prompt))
        {
            var prompt = Ui.Body("Pedido: " + Clip(world.Prompt, 110), 12);
            prompt.AddThemeColorOverride("font_color", new Color("a0c4e8"));
            col.AddChild(prompt);
        }
        var actions = new VBoxContainer();
        actions.AddThemeConstantOverride("separation", 6);
        row.AddChild(actions);
        var captured = world.Id;
        actions.AddChild(Ui.Button("Continuar", () => LoadWorld(captured)));
        actions.AddChild(Ui.Button("Excluir", () => ConfirmDelete(captured, world.Name)));
        return panel;
    }

    void LoadWorld(string id)
    {
        var save = _store.Load(id);
        if (save == null) { _status.Text = "Não foi possível abrir este mundo."; return; }
        _store.SetActive(id);
        Play?.Invoke(save);
    }

    void ConfirmDelete(string id, string name)
    {
        if (_overlay != null) return;
        var overlay = new ColorRect { Color = new Color("000000aa") };
        overlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _overlay = overlay;
        AddChild(overlay);
        var box = new PanelContainer();
        box.SetAnchorsPreset(LayoutPreset.Center);
        box.OffsetLeft = -240; box.OffsetRight = 240; box.OffsetTop = -110; box.OffsetBottom = 110;
        box.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color("101a21f5"),
            ContentMarginLeft = 22, ContentMarginRight = 22, ContentMarginTop = 18, ContentMarginBottom = 18,
            CornerRadiusTopLeft = 12, CornerRadiusTopRight = 12, CornerRadiusBottomLeft = 12, CornerRadiusBottomRight = 12
        });
        overlay.AddChild(box);
        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 10);
        box.AddChild(body);
        body.AddChild(Ui.Body($"Excluir “{name}”? Esta campanha não pode ser recuperada.", 16));
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        body.AddChild(row);
        row.AddChild(Ui.Button("Cancelar", CloseOverlay));
        row.AddChild(Ui.Button("Excluir mundo", () =>
        {
            _store.Delete(id);
            CloseOverlay();
            Refresh();
        }));
    }

    void CloseOverlay()
    {
        if (_overlay == null) return;
        _overlay.QueueFree();
        _overlay = null;
    }

    void OpenCreate()
    {
        if (_overlay != null) return;
        var create = new WorldCreateController { Settings = Settings };
        create.Cancelled = () => { create.QueueFree(); _overlay = null; Refresh(); };
        create.Completed = save =>
        {
            create.QueueFree();
            _overlay = null;
            Play?.Invoke(save);
        };
        _overlay = create;
        AddChild(create);
    }

    static string Clip(string text, int max)
    {
        text = (text ?? "").Replace('\n', ' ').Trim();
        return text.Length <= max ? text : text[..max].Trim() + "…";
    }

    static string PlayLabel(int seconds)
    {
        if (seconds < 45) return "recém criado";
        if (seconds < 3600) return $"{Math.Max(1, seconds / 60)} min jogados";
        return $"{seconds / 3600} h {(seconds % 3600) / 60} min";
    }

    static string When(string last, string created)
    {
        var raw = string.IsNullOrWhiteSpace(last) ? created : last;
        return DateTime.TryParse(raw, null, System.Globalization.DateTimeStyles.RoundtripKind, out var date)
            ? date.ToLocalTime().ToString("dd/MM HH:mm")
            : "agora";
    }
}
