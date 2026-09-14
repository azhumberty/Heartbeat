using Godot;

namespace Heartbeat;

/// <summary>World Forge: prompt + persistent Creative assets → lore/atlas, then play. Saved worlds listed separately.</summary>
public partial class NewGameController : Control
{
    public GameSettings Settings { get; set; } = new();
    public Action<GameSave>? Completed;
    public Action? Cancelled;

    readonly CancellationTokenSource _cancel = new();
    readonly WorldStore _store = new();
    VBoxContainer _body = null!;
    VBoxContainer _footer = null!;
    Label _status = null!;
    bool _busy;

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
        var title = Ui.Text("FORJA DE MUNDOS", 22);
        title.AddThemeColorOverride("font_color", new Color("e6c27a"));
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        header.AddChild(title);
        header.AddChild(Ui.Button("Voltar", () => { if (!_busy) Cancelled?.Invoke(); }));
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        outer.AddChild(scroll);
        _body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _body.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(_body);
        Ui.FitScrollChild(scroll, _body);
        _footer = new VBoxContainer();
        _footer.AddThemeConstantOverride("separation", 8);
        outer.AddChild(_footer);
        ShowHub();
    }

    void Clear()
    {
        foreach (var child in _body.GetChildren()) { _body.RemoveChild(child); child.QueueFree(); }
        foreach (var child in _footer.GetChildren()) { _footer.RemoveChild(child); child.QueueFree(); }
    }

    void ShowHub()
    {
        Clear();
        _body.AddChild(Ui.Body("A Forja lê o teu pedido e os assets do Criativo. Nada é aleatório sem o prompt.", 16));
        _footer.AddChild(Ui.Button("Mundos Salvos", ShowSaves));
        _footer.AddChild(Ui.Button("Forjar Novo Mundo", ShowForge));
    }

    void ShowSaves()
    {
        Clear();
        _body.AddChild(Ui.Text("MUNDOS SALVOS", 22));
        var worlds = _store.List();
        if (worlds.Count == 0)
            _body.AddChild(Ui.Body("Ainda não há mundos. Forja um novo.", 16));
        foreach (var world in worlds)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 10);
            var col = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            col.AddChild(Ui.Text(world.Name, 18));
            col.AddChild(Ui.Body((world.Summary.Length > 0 ? world.Summary : world.Prompt).Length > 90
                ? (world.Summary.Length > 0 ? world.Summary : world.Prompt)[..90] + "..."
                : (world.Summary.Length > 0 ? world.Summary : world.Prompt), 13));
            row.AddChild(col);
            var captured = world.Id;
            row.AddChild(Ui.Button("Jogar", () =>
            {
                var save = _store.Load(captured);
                if (save == null) return;
                Completed?.Invoke(save);
            }));
            _body.AddChild(row);
        }
        _footer.AddChild(Ui.Button("Voltar", ShowHub));
    }

    void ShowForge()
    {
        Clear();
        _body.AddChild(Ui.Text("FORJAR NOVO MUNDO", 22));
        _body.AddChild(Ui.Body("Escreve o pedido. A IA usa isto no lore e no Atlas.", 15));
        var prompt = new TextEdit
        {
            PlaceholderText = "Ex: Mundo dominado por vampiros no deserto",
            CustomMinimumSize = new Vector2(0, 90),
            WrapMode = TextEdit.LineWrappingMode.Boundary
        };
        prompt.AddThemeFontSizeOverride("font_size", 16);
        _body.AddChild(prompt);

        _body.AddChild(Ui.Body("Assets persistentes do Criativo (opcional):", 14));
        var assets = new ContentLibrary().Load()
            .Where(a => a.Enabled && a.Category is "Personagens" or "NPCs" or "Inimigos" or "Mercadores")
            .GroupBy(a => a.Id).Select(g => g.First())
            .Take(24).ToList();
        var checks = new Dictionary<string, CheckBox>();
        if (assets.Count == 0)
            _body.AddChild(Ui.Body("Nenhum asset no Criativo. O mundo nasce só do prompt.", 13));
        else
        {
            foreach (var asset in assets)
            {
                var box = new CheckBox
                {
                    Text = $"{asset.DisplayName}  ·  {asset.Category}" + (asset.BuiltIn ? "  (kit)" : ""),
                    ClipText = false
                };
                box.AddThemeFontSizeOverride("font_size", 14);
                _body.AddChild(box);
                checks[asset.Id] = box;
            }
        }

        _body.AddChild(Ui.Body("O teu nome neste mundo:", 14));
        var name = new LineEdit { PlaceholderText = "Nome", MaxLength = 32, CustomMinimumSize = new Vector2(0, 44) };
        name.AddThemeFontSizeOverride("font_size", 18);
        _body.AddChild(name);

        _status = Ui.Body("", 15);
        _status.AddThemeColorOverride("font_color", new Color("e5b18b"));
        _body.AddChild(_status);

        _footer.AddChild(Ui.Button("Iniciar", () => _ = Forge(prompt.Text, name.Text, checks)));
        _footer.AddChild(Ui.Button("Voltar", ShowHub));
        prompt.GrabFocus();
    }

    async Task Forge(string promptText, string playerName, Dictionary<string, CheckBox> checks)
    {
        if (_busy) return;
        var prompt = (promptText ?? "").Trim();
        var who = (playerName ?? "").Trim();
        if (prompt.Length < 8) { _status.Text = "O pedido precisa de pelo menos 8 letras."; return; }
        if (who.Length < 2) { _status.Text = "Escolhe um nome com pelo menos 2 letras."; return; }
        var selected = checks.Where(kv => GodotObject.IsInstanceValid(kv.Value) && kv.Value.ButtonPressed).Select(kv => kv.Key).ToList();
        _busy = true;
        var overlay = Loading("A IA está forjando seu mundo...");
        try
        {
            var seed = Random.Shared.NextInt64();
            IWorldLoreProvider loreProvider = Settings.UseOpenRouter || Settings.UseOnlineAi
                ? new GroqWorldLoreProvider()
                : new OfflineWorldLoreProvider();
            if (GodotObject.IsInstanceValid(overlay.Item2)) overlay.Item2.Text = "A IA está forjando o lore...";
            WorldDefinition def;
            try
            {
                def = await WorldGenerationService.CreateAsync(prompt, seed, selected, Settings, _cancel.Token);
            }
            catch (OperationCanceledException) { return; }
            catch
            {
                def = WorldGenerationService.CreateOffline(prompt, seed, selected);
            }
            try
            {
                var generated = await loreProvider.CreateAsync(seed, Settings, _cancel.Token, prompt);
                if (!string.IsNullOrWhiteSpace(generated.Lore.Premise))
                {
                    def.Threat = string.IsNullOrWhiteSpace(generated.Lore.Threat) ? def.Threat : generated.Lore.Threat;
                    def.Atmosphere = string.IsNullOrWhiteSpace(generated.Lore.Atmosphere) ? def.Atmosphere : generated.Lore.Atmosphere;
                    if (!string.IsNullOrWhiteSpace(generated.Lore.RegionName)) def.Name = generated.Lore.RegionName;
                    if (generated.Lore.Factions.Count > 0) def.Factions = generated.Lore.Factions.ToList();
                    if (generated.Lore.Rumors.Count > 0) def.StoryHooks = generated.Lore.Rumors.ToList();
                    def.ProviderStatus = generated.ProviderStatus;
                }
            }
            catch (OperationCanceledException) { return; }
            catch { /* keep WorldGenerationService result */ }

            if (GodotObject.IsInstanceValid(overlay.Item2)) overlay.Item2.Text = "A montar o Atlas e o elenco...";
            var save = WorldGenerationService.BuildSave(def, Settings);
            save.PlayerName = who;
            save.CampResidents = new();
            InjectPersistentAssets(save, selected);

            if (GodotObject.IsInstanceValid(overlay.Item2)) overlay.Item2.Text = "A gerar o visual (Pollinations)...";
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(_cancel.Token);
                cts.CancelAfter(TimeSpan.FromSeconds(50));
                await WorldPrep.Run(save, (_, i, n) =>
                {
                    if (GodotObject.IsInstanceValid(overlay.Item2))
                        overlay.Item2.Text = $"A IA está forjando seu mundo... {i}/{n}";
                }, cts.Token);
            }
            catch { save.ImagePaths["prep:done"] = "1"; }

            _store.Save(save);
            Completed?.Invoke(save);
        }
        catch (Exception e)
        {
            GD.PushWarning("[Forja] " + e.GetType().Name + ": " + e.Message);
            if (GodotObject.IsInstanceValid(_status)) _status.Text = "Falha ao forjar: " + e.GetType().Name;
        }
        finally
        {
            _busy = false;
            if (GodotObject.IsInstanceValid(overlay.Item1)) overlay.Item1.QueueFree();
        }
    }

    static void InjectPersistentAssets(GameSave save, List<string> selected)
    {
        var library = new ContentLibrary();
        var repo = new CharacterRepository();
        foreach (var id in selected)
        {
            var rec = library.Find(id) ?? library.Load().FirstOrDefault(a => a.Id == id);
            if (rec == null) continue;
            if (rec.Category is "Personagens" or "NPCs" or "Mercadores")
            {
                var person = repo.Load(id);
                if (person != null)
                {
                    if (!save.CharacterIds.Contains(person.Id)) save.CharacterIds.Add(person.Id);
                    save.CharacterStates.TryAdd(person.Id, new CharacterState { CurrentLocation = "road" });
                }
            }
            if (rec.Category == "Inimigos" && save.GeneratedEnemies.All(e => e.Id != rec.Id))
            {
                save.GeneratedEnemies.Add(new GeneratedEnemy
                {
                    Id = rec.Id,
                    Name = rec.DisplayName,
                    Health = Math.Max(40, rec.Health),
                    Damage = Math.Max(8, rec.Damage),
                    RewardXp = Math.Max(12, rec.RewardXp),
                    CoinMin = rec.CoinMin,
                    CoinMax = Math.Max(rec.CoinMin, rec.CoinMax),
                    LootTier = 2
                });
            }
        }
        StoryDirector.BindAtlas(save);
    }

    (Control, Label) Loading(string text)
    {
        var overlay = new ColorRect { Color = new Color("03070af4"), MouseFilter = MouseFilterEnum.Stop };
        overlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var label = Ui.Body(text, 22);
        label.Position = new Vector2(60, 300);
        label.ClipText = false;
        overlay.AddChild(label);
        AddChild(overlay);
        return (overlay, label);
    }

    public override void _ExitTree()
    {
        try { _cancel.Cancel(); } catch (ObjectDisposedException) { }
    }
}
