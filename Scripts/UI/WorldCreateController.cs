using Godot;

namespace Heartbeat;

/// <summary>Create World: prompt → optional library → validated WorldDefinition → player name.</summary>
public partial class WorldCreateController : Control
{
    public GameSettings Settings { get; set; } = new();
    public Action<GameSave>? Completed;
    public Action? Cancelled;
    readonly CancellationTokenSource _cancel = new();
    ScrollContainer _scroll = null!;
    VBoxContainer _body = null!;
    VBoxContainer _footer = null!;
    Label _status = null!;
    string _prompt = "";
    readonly List<string> _selected = new();
    WorldDefinition _definition = new();
    string _providerStatus = "Offline · mundo procedural";

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var shade = new ColorRect { Color = new Color("03070add") };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(shade);
        var panel = new PanelContainer();
        panel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        panel.OffsetLeft = 36; panel.OffsetRight = -36;
        panel.OffsetTop = 16; panel.OffsetBottom = -16;
        panel.ClipContents = true;
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color("0a1220ee"),
            CornerRadiusTopLeft = 14, CornerRadiusTopRight = 14, CornerRadiusBottomLeft = 14, CornerRadiusBottomRight = 14,
            ContentMarginLeft = 18, ContentMarginRight = 18, ContentMarginTop = 14, ContentMarginBottom = 14
        });
        AddChild(panel);
        var outer = new VBoxContainer();
        outer.AddThemeConstantOverride("separation", 8);
        panel.AddChild(outer);
        var header = new HBoxContainer();
        outer.AddChild(header);
        var title = Ui.Text("CRIAR NOVO MUNDO", 22);
        title.AddThemeColorOverride("font_color", new Color("e6c27a"));
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        header.AddChild(title);
        header.AddChild(Ui.Button("Voltar", () => Cancelled?.Invoke()));
        _scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        outer.AddChild(_scroll);
        _body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _body.AddThemeConstantOverride("separation", 10);
        _scroll.AddChild(_body);
        Ui.FitScrollChild(_scroll, _body);
        _footer = new VBoxContainer();
        _footer.AddThemeConstantOverride("separation", 8);
        outer.AddChild(_footer);
        ShowPrompt();
    }

    void Clear()
    {
        while (_body.GetChildCount() > 0)
        {
            var c = _body.GetChild(0);
            _body.RemoveChild(c);
            c.QueueFree();
        }
        while (_footer.GetChildCount() > 0)
        {
            var c = _footer.GetChild(0);
            _footer.RemoveChild(c);
            c.QueueFree();
        }
    }

    void ShowPrompt()
    {
        Clear();
        _body.AddChild(Ui.Body("Descreva o mundo que deseja viver.", 22));
        _body.AddChild(Ui.Body("A IA cria o começo — não a campanha inteira. O Atlas e a história crescem durante o jogo.", 15));
        var edit = new TextEdit
        {
            Text = string.IsNullOrWhiteSpace(_prompt)
                ? ""
                : _prompt,
            PlaceholderText = "Um mundo medieval sombrio onde humanos e criaturas humanoides convivem. Existe uma enorme floresta amaldiçoada, vilas decadentes, mercadores viajantes, romance, mistérios, lobisomens, minotauros e uma guerra entre três facções.",
            CustomMinimumSize = new Vector2(0, 110),
            WrapMode = TextEdit.LineWrappingMode.Boundary,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        edit.AddThemeFontSizeOverride("font_size", 16);
        _body.AddChild(edit);
        _status = Ui.Body("", 14);
        _status.AddThemeColorOverride("font_color", new Color("e5b18b"));
        _body.AddChild(_status);
        _footer.AddChild(Ui.Button("Continuar - Biblioteca (opcional)", () =>
        {
            var value = edit.Text.Trim();
            if (value.Length < 8) { _status.Text = "Escreve pelo menos um pedido curto (8 caracteres)."; return; }
            _prompt = value[..Math.Min(value.Length, 1500)];
            ShowLibrary();
        }));
        edit.GrabFocus();
    }

    void ShowLibrary()
    {
        Clear();
        _body.AddChild(Ui.Body("Deseja adicionar conteúdo da sua biblioteca?", 22));
        _body.AddChild(Ui.Body("É opcional. Podes pular: 0 NPCs persistentes é válido — o mundo nasce só do pedido. Ou marca um ferreiro e deixa o resto ser gerado.", 15));
        var assets = UniqueLibraryPicks();
        var checks = new Dictionary<string, CheckBox>(StringComparer.OrdinalIgnoreCase);
        if (assets.Count == 0)
            _body.AddChild(Ui.Body("A biblioteca esta vazia. Pular continua — a campanha sera gerada.", 14));
        else
        {
            foreach (var group in assets.GroupBy(a => a.Category == "Personagens" ? "NPCs" : a.Category))
            {
                var heading = Ui.Text(group.Key.ToUpperInvariant(), 13);
                heading.AddThemeColorOverride("font_color", new Color("c9b27a"));
                _body.AddChild(heading);
                foreach (var asset in group)
                {
                    var box = new CheckBox { Text = CleanLabel(asset.DisplayName), ButtonPressed = _selected.Contains(asset.Id) };
                    box.AddThemeFontSizeOverride("font_size", 14);
                    _body.AddChild(box);
                    checks[asset.Id] = box;
                }
            }
        }
        void Collect()
        {
            _selected.Clear();
            foreach (var pair in checks)
                if (pair.Value.ButtonPressed) _selected.Add(pair.Key);
        }
        _footer.AddChild(Ui.Button("Pular - so o pedido", () => { _selected.Clear(); _ = Generate(); }));
        if (assets.Count > 0)
            _footer.AddChild(Ui.Button("Usar o que marquei", () => { Collect(); _ = Generate(); }));
    }

    static List<ContentAssetRecord> UniqueLibraryPicks()
    {
        var raw = new ContentLibrary().Load()
            .Where(a => a.Enabled && a.Category is "Personagens" or "NPCs" or "Mercadores" or "Inimigos")
            .ToList();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var unique = new List<ContentAssetRecord>();
        foreach (var asset in raw)
        {
            var key = CleanLabel(asset.DisplayName);
            if (key.Length < 2 || !seen.Add(key)) continue;
            unique.Add(asset);
            if (unique.Count >= 8) break;
        }
        return unique;
    }

    static string CleanLabel(string name)
    {
        var n = (name ?? "").ToLowerInvariant();
        foreach (var noise in new[] { " chroma", " fullbody", " portrait", " idle", " combat", " dressed" })
            n = n.Replace(noise, "");
        n = n.Replace('_', ' ').Replace('-', ' ').Trim();
        if (n.Length == 0) return name ?? "";
        return char.ToUpperInvariant(n[0]) + n[1..];
    }

    async Task Generate()
    {
        Clear();
        _body.AddChild(Ui.Body("O começo deste mundo está tomando forma…", 22));
        _body.AddChild(Ui.Body("A campanha verdadeira começa no Atlas, não nesta tela.", 15));
        try
        {
            var seed = Random.Shared.NextInt64();
            _definition = await WorldGenerationService.CreateAsync(_prompt, seed, _selected, Settings, _cancel.Token);
            if (_cancel.IsCancellationRequested || !IsInsideTree()) return;
            _providerStatus = string.IsNullOrWhiteSpace(_definition.ProviderStatus) ? "Offline · mundo procedural" : _definition.ProviderStatus;
            ShowReview();
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            if (!IsInsideTree()) return;
            _definition = WorldGenerationService.CreateOffline(_prompt, Random.Shared.NextInt64(), _selected);
            _providerStatus = "Falha na IA · mundo criado offline (" + e.GetType().Name + ")";
            ShowReview();
        }
    }

    void ShowReview()
    {
        Clear();
        var nameLbl = Ui.Text(_definition.Name.ToUpperInvariant(), 30);
        nameLbl.AddThemeColorOverride("font_color", new Color("f0d080"));
        _body.AddChild(nameLbl);
        _body.AddChild(Ui.Body(_definition.Description, 18));
        _body.AddChild(new HSeparator());
        _body.AddChild(Ui.Body("⚔  " + _definition.Threat, 16));
        _body.AddChild(Ui.Body("⚑  " + string.Join(" · ", _definition.Factions), 15));
        _body.AddChild(Ui.Body("Pedido original: " + _definition.Prompt, 13));
        var persist = _definition.SelectedLibraryIds.Count == 0
            ? "Nenhum conteúdo persistente — a campanha pode nascer toda da IA."
            : $"{_definition.SelectedLibraryIds.Count} item(ns) da biblioteca entram neste mundo.";
        var persistLbl = Ui.Body(persist, 13);
        persistLbl.AddThemeColorOverride("font_color", new Color("a0c4e8"));
        _body.AddChild(persistLbl);
        var status = Ui.Body(_providerStatus + "  ·  seed " + _definition.Seed, 12);
        status.AddThemeColorOverride("font_color", new Color("8999aa"));
        _body.AddChild(status);
        _body.AddChild(Ui.Body("Nome do mundo (podes ajustar):", 14));
        var nameEdit = new LineEdit { Text = _definition.Name, MaxLength = 70, CustomMinimumSize = new Vector2(0, 46) };
        nameEdit.AddThemeFontSizeOverride("font_size", 18);
        _body.AddChild(nameEdit);
        _footer.AddChild(Ui.Button("Continuar - o teu nome", () =>
        {
            var value = nameEdit.Text.Trim();
            if (value.Length >= 2) _definition.Name = value;
            ShowName();
        }));
    }

    void ShowName()
    {
        Clear();
        _body.AddChild(Ui.Body("Como devemos chamá-lo?", 26));
        _body.AddChild(Ui.Body("Só um nome. Não há avatar. Tu existes nas escolhas, nas cartas e nas relações.", 16));
        var name = new LineEdit { PlaceholderText = "Digite seu nome...", MaxLength = 32, CustomMinimumSize = new Vector2(0, 52) };
        name.AddThemeFontSizeOverride("font_size", 20);
        _body.AddChild(name);
        _status = Ui.Body("", 15);
        _status.AddThemeColorOverride("font_color", new Color("e5b18b"));
        _body.AddChild(_status);
        void Confirm()
        {
            var value = name.Text.Trim();
            if (value.Length < 2) { _status.Text = "Escolhe um nome com pelo menos 2 letras."; return; }
            var save = WorldGenerationService.BuildSave(_definition, Settings);
            save.PlayerName = value;
            new WorldStore().Save(save);
            Completed?.Invoke(save);
        }
        name.TextSubmitted += _ => Confirm();
        _footer.AddChild(Ui.Button("Entrar no Atlas", Confirm));
        name.GrabFocus();
    }

    public override void _ExitTree()
    {
        try { _cancel.Cancel(); } catch (ObjectDisposedException) { }
    }
}
