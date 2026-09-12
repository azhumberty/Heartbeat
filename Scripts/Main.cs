using Godot;

namespace Heartbeat;

public partial class Main : Node
{
    readonly CharacterRepository _characters = new(); readonly SaveManager _saves = new(); readonly ProceduralDialogueProvider _dialogue = new();
    readonly CharacterDesireSystem _desires = new(); readonly ExpressionResolver _expressions = new();
    CharacterData CurrentCharacter { get => _game.Character ??= _characters.Load(_game.CharacterIds.FirstOrDefault() ?? "") ?? _characters.LoadDemo(); set => _game.Character=value; }
    CharacterState CurrentState { get => _game.State ??= _game.CharacterStates.TryGetValue(CurrentCharacter.Id,out var state) ? state : (_game.CharacterStates[CurrentCharacter.Id]=new()); set => _game.State=value; }
    GameSave _game = new(); CharacterData _editing = new();
    Label _name = null!, _status = null!, _dialogueText = null!, _desire = null!; TextureRect _portrait = null!; ColorRect _avatar = null!; VBoxContainer _choices = null!; LineEdit _freeText = null!;
    Panel _creator = null!, _gallery = null!; LineEdit _newName = null!, _personality = null!, _interests = null!, _imageTags = null!; Label _importInfo = null!; string _pendingImage = "";

    public override void _Ready()
    {
        _game = _saves.Load() ?? new GameSave(); _saves.Migrate(_game);
        if (_game.CharacterIds.Count == 0) _game.CharacterIds.Add(_characters.LoadDemo().Id);
        var activeId = _game.CharacterIds[0]; CurrentCharacter = _characters.Load(activeId) ?? _characters.LoadDemo();
        CurrentState = _game.CharacterStates.TryGetValue(activeId, out var current) ? current : new CharacterState(); _game.CharacterStates[activeId] = CurrentState;
        BuildUi(); Refresh("A manhã começa calma. Luna está no café, mexendo distraidamente no caderno.");
    }

    StyleBoxFlat Box(Color color, int radius = 14) { var s = new StyleBoxFlat { BgColor = color, CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius, CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius, ContentMarginLeft = 18, ContentMarginRight = 18, ContentMarginTop = 12, ContentMarginBottom = 12 }; return s; }
    Label Text(string value, int size = 16, Color? color = null) { var label = new Label { Text = value }; label.AddThemeFontSizeOverride("font_size", size); label.AddThemeColorOverride("font_color", color ?? Colors.White); return label; }
    Button Button(string value, Action action) { var b = new Button { Text = value, CustomMinimumSize = new Vector2(0, 42) }; b.AddThemeFontSizeOverride("font_size", 15); b.AddThemeStyleboxOverride("normal", Box(new Color("46365f"), 10)); b.Pressed += action; return b; }

    void BuildUi()
    {
        var root = new Control { GrowHorizontal = Control.GrowDirection.Both, GrowVertical = Control.GrowDirection.Both }; root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        root.AddThemeStyleboxOverride("panel", Box(new Color("151324"))); AddChild(root);
        var background = new ColorRect { Color = new Color("17142b") }; background.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); root.AddChild(background);
        var main = new MarginContainer(); main.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        main.AddThemeConstantOverride("margin_left", 32); main.AddThemeConstantOverride("margin_right", 32); main.AddThemeConstantOverride("margin_top", 24); main.AddThemeConstantOverride("margin_bottom", 26); root.AddChild(main);
        var rows = new VBoxContainer(); rows.AddThemeConstantOverride("separation", 14); main.AddChild(rows);
        var top = new HBoxContainer(); rows.AddChild(top); var title = Text("HEARTBEAT", 26, new Color("ffb6d8")); top.AddChild(title); top.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        top.AddChild(Button("Character Creator", ShowCreator)); top.AddChild(Button("Gallery", ShowGallery)); top.AddChild(Button("Config.", ShowSettings)); top.AddChild(Button("Salvar", () => { _saves.Save(_game); Say("Progresso salvo no slot 1."); })); top.AddChild(Button("Carregar", () => { var g = _saves.Load(); if (g != null) { _game = g; Refresh("Save carregado."); } else Say("Ainda não existe um save no slot 1."); }));
        var scene = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill }; scene.AddThemeConstantOverride("separation", 28); rows.AddChild(scene);
        var left = new VBoxContainer { CustomMinimumSize = new Vector2(300, 0) }; left.AddThemeConstantOverride("separation", 12); scene.AddChild(left);
        left.AddChild(Text("SUA ROTINA", 13, new Color("b4a7d6"))); _status = Text("", 16); left.AddChild(_status); _desire = Text("", 15, new Color("ffd1a3")); left.AddChild(_desire); left.AddChild(new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill });
        left.AddChild(Button("Rua", () => TravelTo("Street"))); left.AddChild(Button("Café", () => TravelTo("Cafe"))); left.AddChild(Button("Parque", () => TravelTo("Park"))); left.AddChild(Button("Passar tempo", AdvanceTime)); left.AddChild(Button("Encontrar no parque", TryParkEvent)); left.AddChild(Button("Perfil", ShowProfile));
        var center = new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill }; scene.AddChild(center);
        _avatar = new ColorRect { Color = new Color("b063a7"), CustomMinimumSize = new Vector2(380, 420), AnchorLeft = .5f, AnchorTop = .5f, AnchorRight = .5f, AnchorBottom = .5f, OffsetLeft = -190, OffsetTop = -210, OffsetRight = 190, OffsetBottom = 210 };
        _avatar.AddThemeStyleboxOverride("panel", Box(new Color("b063a7"), 190)); center.AddChild(_avatar);
        var face = Text("✦", 115, new Color("fff2f8")); face.HorizontalAlignment = HorizontalAlignment.Center; face.VerticalAlignment = VerticalAlignment.Center; face.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); _avatar.AddChild(face);
        _portrait = new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, Visible = false }; _portrait.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); _avatar.AddChild(_portrait);
        var right = new VBoxContainer { CustomMinimumSize = new Vector2(320, 0) }; right.AddThemeConstantOverride("separation", 10); scene.AddChild(right); right.AddChild(Text("CONEXÃO", 13, new Color("b4a7d6"))); _name = Text("", 28, new Color("ffb6d8")); right.AddChild(_name); right.AddChild(new HSeparator()); _dialogueText = Text("", 17); _dialogueText.AutowrapMode = TextServer.AutowrapMode.WordSmart; _dialogueText.SizeFlagsVertical = Control.SizeFlags.ExpandFill; right.AddChild(_dialogueText); _choices = new VBoxContainer(); _choices.AddThemeConstantOverride("separation", 8); right.AddChild(_choices);
        var composer = new HBoxContainer(); right.AddChild(composer); _freeText = new LineEdit { PlaceholderText = "Diga algo para " + CurrentCharacter.Name }; _freeText.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; _freeText.TextSubmitted += text => { if (!string.IsNullOrWhiteSpace(text)) { _freeText.Clear(); _ = TalkAsync(text); } }; composer.AddChild(_freeText); composer.AddChild(Button("Enviar", () => { var text = _freeText.Text; if (!string.IsNullOrWhiteSpace(text)) { _freeText.Clear(); _ = TalkAsync(text); } }));
        _creator = CreateCreator(); root.AddChild(_creator); _gallery = CreateGallery(); root.AddChild(_gallery);
    }

    void Refresh(string message)
    {
        var c = CurrentCharacter; var s = CurrentState; s.Clamp(); s.CurrentEmotion = _expressions.Resolve(c, s); _name.Text = c.Name; _status.Text = $"Dia {_game.Day} · {_game.Period}\n{s.Relationship}\nAções: {_game.ActionsLeft}";
        _desire.Text = $"Local: {s.CurrentLocation}\nHoje quer: {s.CurrentDesire}"; _dialogueText.Text = message; SetPortrait(); BuildChoices();
    }
    void SetPortrait()
    {
        var pic = _expressions.ImageFor(CurrentCharacter, CurrentState.CurrentEmotion); Texture2D? texture = null;
        if (pic != null && File.Exists(ProjectSettings.GlobalizePath(pic.ProcessedPath))) { var image = Image.LoadFromFile(ProjectSettings.GlobalizePath(pic.ProcessedPath)); texture = ImageTexture.CreateFromImage(image); }
        _portrait.Texture = texture; _portrait.Visible = texture != null; _avatar.Color = CurrentState.CurrentEmotion switch { "happy" => new Color("df6ca0"), "romantic" => new Color("d65577"), "tired" => new Color("66658f"), "angry" => new Color("a24b5d"), _ => new Color("8e69a8") };
        var tween = CreateTween(); tween.TweenProperty(_avatar, "scale", new Vector2(1.025f, 1.025f), .8); tween.TweenProperty(_avatar, "scale", Vector2.One, .8);
    }
    void BuildChoices()
    {
        foreach (var child in _choices.GetChildren()) child.QueueFree();
        _choices.AddChild(Button("Perguntar como ela está", () => Talk("Como você está hoje?")));
        _choices.AddChild(Button("Conversar sobre interesses", () => Talk("Quero conversar sobre seus interesses.")));
        _choices.AddChild(Button("Dar um presente", () => Talk("Eu trouxe um pequeno presente para você.")));
    }
    void Talk(string input)
    {
        if (!UseAction()) return; var r = _dialogue.Reply(CurrentCharacter, CurrentState, input); var s = CurrentState; s.Affection += r.AffectionDelta; s.Trust += r.TrustDelta; s.Energy += r.EnergyDelta; s.CurrentEmotion = r.Emotion; s.CurrentDesire = r.Desire == "spend_time" ? "passar um tempo juntos" : "conversar"; AddMemory(r.Memory); UpdateRelationship(); Refresh(r.Dialogue);
    }
    async Task TalkAsync(string input)
    {
        if (!UseAction()) return; Say($"{CurrentCharacter.Name} está pensando...");
        IDialogueProvider provider = _game.Settings.UseOnlineAi ? new GroqDialogueProvider() : _dialogue;
        var result = await provider.ReplyAsync(CurrentCharacter, CurrentState, input, _game.Settings); ApplyDialogue(result); Refresh(result.Dialogue);
    }
    void ApplyDialogue(DialogueResult r)
    {
        r = DialogueValidator.Sanitize(r); var s = CurrentState; s.Affection += r.AffectionDelta; s.Trust += r.TrustDelta; s.Romance += r.RomanceDelta; s.Attraction += r.AttractionDelta; s.Energy += r.EnergyDelta; s.Stress += r.StressDelta; s.CurrentEmotion = r.Emotion; s.CurrentDesire = r.Desire; AddMemory(r.Memory); if (r.ImportantMemory) s.ImportantMemories.Insert(0, r.Memory); UpdateRelationship(); s.Clamp();
    }
    bool UseAction() { if (_game.ActionsLeft > 0) { _game.ActionsLeft--; return true; } Say("Você já fez tudo o que podia neste período. Passe o tempo para continuar."); return false; }
    void AdvanceTime()
    {
        string[] periods = { "Morning", "Afternoon", "Evening", "Night" }; var i = Array.IndexOf(periods, _game.Period) + 1; if (i == periods.Length) { i = 0; _game.Day++; } _game.Period = periods[i]; _game.ActionsLeft = 3; CurrentState.Energy = Math.Min(100, CurrentState.Energy + (_game.Period == "Morning" ? 18 : 3)); CurrentState.CurrentDesire = _desires.Choose(CurrentCharacter, CurrentState, _game.Period); Refresh($"Agora é {_game.Period}. {CurrentCharacter.Name} parece querer {CurrentState.CurrentDesire}.");
    }
    void TravelTo(string location) { CurrentState.CurrentLocation = location; Refresh($"Você caminha até {location}. {CurrentCharacter.Name} está por perto."); }
    void TryParkEvent()
    {
        if (!UseAction()) return;
        if (CurrentState.Affection >= 20 && !_game.UnlockedCinematics.Contains("rainy_evening")) { _game.UnlockedCinematics.Add("rainy_evening"); CurrentState.Romance += 5; CurrentState.Trust += 2; AddMemory("Uma caminhada sob a chuva no parque."); UpdateRelationship(); ShowCinematic(); }
        else Refresh(CurrentState.Affection < 20 ? "O parque parece distante hoje. Talvez um pouco mais de conversa ajude." : "Vocês passeiam pelo parque e dividem um silêncio confortável.");
    }
    void UpdateRelationship() { var s = CurrentState; s.Relationship = s.Romance >= 40 ? "Dating" : s.Affection >= 65 && s.Trust >= 50 ? "Romantic Interest" : s.Affection >= 45 ? "Close Friend" : s.Affection >= 25 ? "Friend" : s.Affection >= 10 ? "Acquaintance" : "Stranger"; }
    void AddMemory(string memory) { if (string.IsNullOrEmpty(memory)) return; CurrentState.RecentMemories.Insert(0, memory); if (CurrentState.RecentMemories.Count > 8) CurrentState.RecentMemories.RemoveAt(8); }
    void Say(string text) { _dialogueText.Text = text; }

    Panel CreateCreator()
    {
        var p = new Panel { Visible = false, Position = new Vector2(310, 160), Size = new Vector2(660, 560) };
        p.AddThemeStyleboxOverride("panel", Box(new Color("28213d"), 18)); var box = new VBoxContainer { Position = new Vector2(24, 22), Size = new Vector2(612, 516) }; box.AddThemeConstantOverride("separation", 10); p.AddChild(box);
        var header = new HBoxContainer(); box.AddChild(header); header.AddChild(Text("CHARACTER CREATOR", 24, new Color("ffb6d8"))); header.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }); header.AddChild(Button("Fechar", () => p.Hide()));
        box.AddChild(Text("Todos os personagens devem ser adultos fictícios. Fotos são usadas somente como arte que você escolheu importar.", 13, new Color("c7bedb")));
        _newName = new LineEdit { PlaceholderText = "Nome", Text = CurrentCharacter.Name }; box.AddChild(_newName);
        _personality = new LineEdit { PlaceholderText = "Personalidade (ex.: extrovertida, brincalhona)", Text = CurrentCharacter.Personality }; box.AddChild(_personality);
        _interests = new LineEdit { PlaceholderText = "Interesses separados por vírgula", Text = string.Join(", ", CurrentCharacter.Interests) }; box.AddChild(_interests);
        _imageTags = new LineEdit { PlaceholderText = "Tags da imagem: neutral, happy, shy ou customizada", Text = "neutral" }; box.AddChild(_imageTags);
        var import = new HBoxContainer(); box.AddChild(import); import.AddChild(Button("Importar PNG/JPG/WebP", OpenImagePicker)); _importInfo = Text("Nenhuma imagem selecionada", 14, new Color("c7bedb")); _importInfo.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; import.AddChild(_importInfo);
        box.AddChild(new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill }); box.AddChild(Button("Salvar personagem", SaveCharacter)); return p;
    }
    void ShowCreator() { _creator.Show(); }
    void OpenImagePicker()
    {
        var dialog = new FileDialog { FileMode = FileDialog.FileModeEnum.OpenFile, Access = FileDialog.AccessEnum.Filesystem, Filters = new[] { "*.png, *.jpg, *.jpeg, *.webp ; Imagens" } }; AddChild(dialog); dialog.FileSelected += path => { _pendingImage = path; _importInfo.Text = path.GetFile(); dialog.QueueFree(); }; dialog.Canceled += dialog.QueueFree; dialog.PopupCentered(new Vector2I(760, 520));
    }
    void SaveCharacter()
    {
        var c = CurrentCharacter; c.Name = string.IsNullOrWhiteSpace(_newName.Text) ? "Novo personagem" : _newName.Text.Trim(); c.Personality = _personality.Text.Trim(); c.Interests = _interests.Text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();
        if (!string.IsNullOrEmpty(_pendingImage) && File.Exists(_pendingImage))
        {
            var targetFolder = $"user://Characters/{c.Id}/portrait"; var ext = _pendingImage.GetExtension(); var original = targetFolder.PathJoin("original" + ext); DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(targetFolder)); File.Copy(_pendingImage, ProjectSettings.GlobalizePath(original), true);
            var processed = new BackgroundRemovalService().Process(_pendingImage, targetFolder); c.Images.Add(new CharacterImage { OriginalPath = original, ProcessedPath = processed, Tags = _imageTags.Text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).DefaultIfEmpty("neutral").ToList() }); _pendingImage = "";
        }
        _characters.Save(c); _creator.Hide(); Refresh($"{c.Name} foi salvo. A imagem original e a versão em cache estão no armazenamento local do jogo.");
    }
    Panel CreateGallery()
    {
        var p = new Panel { Visible = false, Position = new Vector2(310, 190), Size = new Vector2(660, 460) }; p.AddThemeStyleboxOverride("panel", Box(new Color("28213d"), 18)); var box = new VBoxContainer { Position = new Vector2(24, 22), Size = new Vector2(612, 416) }; box.AddThemeConstantOverride("separation", 12); p.AddChild(box);
        var header = new HBoxContainer(); box.AddChild(header); header.AddChild(Text("GALLERY", 24, new Color("ffb6d8"))); header.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }); header.AddChild(Button("Fechar", () => p.Hide()));
        var card = new ColorRect { Color = new Color("514768"), CustomMinimumSize = new Vector2(0, 220) }; box.AddChild(card); var caption = Text("", 17); caption.HorizontalAlignment = HorizontalAlignment.Center; caption.VerticalAlignment = VerticalAlignment.Center; caption.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); card.AddChild(caption); p.SetMeta("caption", caption); return p;
    }
    void ShowGallery() { ((Label)_gallery.GetMeta("caption")).Text = _game.UnlockedCinematics.Contains("rainy_evening") ? "✦  Caminhada na chuva\nLuna — Cena desbloqueada" : "▣  Slot bloqueado\nAproxime-se de Luna e visite o parque."; _gallery.Show(); }
    void ShowCinematic()
    {
        var overlay = new ColorRect { Color = new Color("44244f"), Modulate = new Color(1, 1, 1, 0) }; overlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); GetChild<Control>(0).AddChild(overlay); var t = Text("CINEMATIC UNLOCKED\n\nCaminhada na chuva\n\nLuna sorri quando as luzes do parque refletem nas poças.\n\"Eu não queria que essa noite acabasse tão cedo.\"", 22, new Color("fff0fa")); t.HorizontalAlignment = HorizontalAlignment.Center; t.VerticalAlignment = VerticalAlignment.Center; t.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); overlay.AddChild(t); var close = Button("Continuar", () => overlay.QueueFree()); close.Position = new Vector2(540, 590); close.Size = new Vector2(200, 44); overlay.AddChild(close); var tween = CreateTween(); tween.TweenProperty(overlay, "modulate:a", 1f, .6);
    }
    void ShowProfile()
    {
        var s = CurrentState; Say($"Perfil de {CurrentCharacter.Name}\n\n{s.Relationship}\nHumor: {s.Mood} · Afeto: {s.Affection} · Confiança: {s.Trust}\nEnergia: {s.Energy} · Romance: {s.Romance}\n\nMemória recente: {(s.RecentMemories.FirstOrDefault() ?? "Ainda não há uma memória marcante.")}");
    }
    void ShowSettings()
    {
        var panel = new Panel { Position = new Vector2(410, 215), Size = new Vector2(460, 290) }; panel.AddThemeStyleboxOverride("panel", Box(new Color("28213d"), 18)); GetChild<Control>(0).AddChild(panel);
        var box = new VBoxContainer { Position = new Vector2(22, 20), Size = new Vector2(416, 250) }; box.AddThemeConstantOverride("separation", 12); panel.AddChild(box); box.AddChild(Text("CONFIGURAÇÕES", 22, new Color("ffb6d8")));
        var online = Button($"IA online: {(_game.Settings.UseOnlineAi ? "ligada" : "desligada")}", () => { _game.Settings.UseOnlineAi = !_game.Settings.UseOnlineAi; panel.QueueFree(); ShowSettings(); }); box.AddChild(online);
        box.AddChild(Button($"Volume: {Mathf.RoundToInt(_game.Settings.Volume * 100)}%", () => { _game.Settings.Volume = _game.Settings.Volume >= 1 ? 0 : _game.Settings.Volume + .2f; panel.QueueFree(); ShowSettings(); }));
        box.AddChild(Text("Groq: use GROQ_API_KEY no ambiente. Sem chave, o jogo usa diálogo procedural offline.", 13, new Color("c7bedb"))); box.AddChild(new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill }); box.AddChild(Button("Fechar", () => panel.QueueFree()));
    }
}
