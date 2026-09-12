using Godot;

namespace Heartbeat;

public partial class WardrobeEditor : Control
{
    public CharacterData Data { get; set; } = new();
    public string CurrentOutfitId { get; set; } = "";
    public Action? Closed;
    public Action<string>? Saved;
    OptionButton _outfits = null!, _poses = null!;
    LineEdit _name = null!;
    Label _status = null!, _hint = null!;
    VBoxContainer _advanced = null!;
    SubViewport _viewport = null!;
    NpcAnimator? _animator;
    CheckerBackdrop _background = null!;
    CutoutCanvas _canvas = null!;
    SpinBox _height = null!, _feet = null!, _fps = null!, _breath = null!, _scale = null!, _x = null!, _y = null!;
    CharacterOutfit _selected = null!;
    OutfitPose? _editingPose;
    CancellationTokenSource? _operation;
    bool _loading, _paused;
    string _mode = "Idle";
    readonly List<TextureRect> _thumbnails = new();

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        Wardrobe.Migrate(Data);
        Ui.Panel(this, "GUARDA-ROUPA", out var body, CloseEditor);
        body.AddChild(Ui.Text("Uma imagem já funciona. Acrescente dois passos para uma caminhada simples.", 15));
        var main = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill }; body.AddChild(main);
        var scroll = new ScrollContainer { CustomMinimumSize = new(365, 0), SizeFlagsVertical = SizeFlags.ExpandFill }; main.AddChild(scroll);
        var controls = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; scroll.AddChild(controls);
        _outfits = new OptionButton(); controls.AddChild(_outfits);
        _outfits.ItemSelected += i => SelectOutfit(Data.Outfits[(int)i]);
        var actions = new HBoxContainer(); controls.AddChild(actions);
        actions.AddChild(Ui.Button("Adicionar", () => { FlushMask(); var o = new CharacterOutfit(); Data.Outfits.Add(o); SelectOutfit(o); }));
        actions.AddChild(Ui.Button("Duplicar", () => { FlushMask(); var o = Wardrobe.Duplicate(_selected); Data.Outfits.Add(o); SelectOutfit(o); }));
        controls.AddChild(Ui.Button("Excluir roupa…", ConfirmDelete));
        controls.AddChild(Ui.Text("Nome da roupa", 14)); _name = new LineEdit(); controls.AddChild(_name);
        _name.TextChanged += s => { if (!_loading) { _selected.Name = s; RefreshList(); } };
        controls.AddChild(Ui.Button("Usar esta roupa", () => { CurrentOutfitId = _selected.Id; _status.Text = "Roupa selecionada. Clique em Salvar guarda-roupa para aplicar."; }));
        controls.AddChild(Ui.Text("Imagem para editar", 14));
        _poses = new OptionButton(); controls.AddChild(_poses);
        foreach (var label in new[] { "Parado — obrigatório", "Passo esquerdo — opcional", "Passo direito — opcional" }) _poses.AddItem(label);
        _poses.ItemSelected += _ => LoadPose();
        var slots = new HBoxContainer(); controls.AddChild(slots);
        var names = new[] { "Parado", "Esquerdo", "Direito" };
        for (int i = 0; i < 3; i++)
        {
            int index = i;
            var card = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; slots.AddChild(card);
            var thumb = new TextureRect { CustomMinimumSize = new(90, 85), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered };
            card.AddChild(thumb); _thumbnails.Add(thumb);
            card.AddChild(Ui.Button(names[i], () => { _poses.Select(index); LoadPose(); PickImage(); }));
        }
        controls.AddChild(Ui.Button("Selecionar / substituir imagem", PickImage));
        controls.AddChild(Ui.Button("Remover imagem selecionada", () => { FlushMask(); CancelOperation(); SetPose(new()); LoadPose(); RefreshPreview(); }));
        controls.AddChild(Ui.Button("Remover fundo automaticamente", () => _ = RemoveBackground()));
        controls.AddChild(Ui.Button("Cancelar processamento", CancelOperation));
        controls.AddChild(Ui.Button("Como ativar o recorte local", () => _status.Text = "Execute Tools/Install-Cutout.ps1 com Python 3.11–3.13 uma vez. O primeiro recorte baixa o modelo; fotos não são enviadas. PNG transparente funciona sem instalação."));
        _height = Spin(controls, "Altura do personagem (m)", .5, 2.5, .01, v => Data.HeightMeters = (float)v);
        _feet = Spin(controls, "Apoio dos pés (m)", -.5, .5, .01, v => _selected.FootOffset = (float)v);
        controls.AddChild(Ui.Button("Restaurar ajustes", () => { _selected.FootOffset = 0; _selected.Fps = 4; _selected.Breathing = .003f; foreach (var p in _selected.Poses) { p.Scale = 1; p.X = p.Y = 0; } SelectOutfit(_selected); }));
        var toggle = new CheckButton { Text = "Ajustes avançados" }; controls.AddChild(toggle);
        _advanced = new VBoxContainer { Visible = false }; controls.AddChild(_advanced); toggle.Toggled += v => _advanced.Visible = v;
        _fps = Spin(_advanced, "Ritmo dos passos", 1, 12, .5, v => _selected.Fps = (float)v);
        _breath = Spin(_advanced, "Respiração", 0, .02, .001, v => _selected.Breathing = (float)v);
        _scale = Spin(_advanced, "Escala da pose selecionada", .5, 1.5, .01, v => Pose.Scale = (float)v);
        _x = Spin(_advanced, "Deslocamento horizontal da pose", -.4, .4, .005, v => Pose.X = (float)v);
        _y = Spin(_advanced, "Deslocamento vertical da pose", -.4, .4, .005, v => Pose.Y = (float)v);

        var tabs = new TabContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill }; main.AddChild(tabs);
        var preview = new VBoxContainer { Name = "No mundo" }; tabs.AddChild(preview);
        var modes = new HBoxContainer(); preview.AddChild(modes);
        foreach (var pair in new[] { ("Parado", "Idle"), ("Caminhando", "Walking"), ("Conversando", "Talking") })
            modes.AddChild(Ui.Button(pair.Item1, () => { FlushMask(); _mode = pair.Item2; RefreshPreview(); }));
        preview.AddChild(Ui.Button("Reproduzir / pausar", () => { _paused = !_paused; if (_animator != null) _animator.Paused = _paused; }));
        var frame = new Control { CustomMinimumSize = new(330, 270), SizeFlagsVertical = SizeFlags.ExpandFill }; preview.AddChild(frame);
        _background = new CheckerBackdrop { MouseFilter = MouseFilterEnum.Ignore }; frame.AddChild(_background); _background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var container = new SubViewportContainer { Stretch = true, MouseFilter = MouseFilterEnum.Ignore }; frame.AddChild(container); container.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _viewport = new SubViewport { Size = new(440, 360), TransparentBg = true, OwnWorld3D = true, RenderTargetUpdateMode = SubViewport.UpdateMode.Always }; container.AddChild(_viewport);
        _viewport.AddChild(new Camera3D { Position = new(0, 1.25f, 4), Projection = Camera3D.ProjectionType.Orthogonal, Size = 3.2f, Current = true });
        _viewport.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new(2, .015f, .1f) }, Position = new(0, 0, .05f), MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("74b9a6"), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded } });
        _hint = Ui.Text("", 14); preview.AddChild(_hint);
        var maskTab = new VBoxContainer { Name = "Retocar recorte" }; tabs.AddChild(maskTab);
        maskTab.AddChild(Ui.Text("Edite a imagem selecionada à esquerda. Restaurar recupera os pixels originais.", 14));
        var brushes = new HBoxContainer(); maskTab.AddChild(brushes);
        brushes.AddChild(Ui.Button("Apagar", () => _canvas.Restore = false));
        brushes.AddChild(Ui.Button("Restaurar", () => _canvas.Restore = true));
        brushes.AddChild(Ui.Button("Desfazer", () => _canvas.Undo()));
        _canvas = new CutoutCanvas { CustomMinimumSize = new(320, 240), SizeFlagsVertical = SizeFlags.ExpandFill }; maskTab.AddChild(_canvas);
        var brush = Spin(maskTab, "Tamanho do pincel", 2, 100, 1, v => _canvas.BrushSize = (float)v); brush.Value = 18;
        maskTab.AddChild(Ui.Button("Resetar para o recorte inicial", () => { if (_editingPose != null) { _editingPose.Cutout = _editingPose.AutoCutout; _editingPose.Mask = ""; LoadCanvas(); RefreshPreview(); } }));
        maskTab.AddChild(Ui.Button("Aplicar retoque ao preview", () => { FlushMask(); RefreshPreview(); }));
        var backgrounds = new OptionButton(); body.AddChild(backgrounds);
        foreach (var s in new[] { "Fundo quadriculado", "Fundo claro", "Fundo escuro" }) backgrounds.AddItem(s);
        backgrounds.ItemSelected += i => { _background.Mode = _canvas.Mode = (int)i; _background.QueueRedraw(); _canvas.QueueRedraw(); };
        _status = Ui.Text("Arquivos originais são preservados. Salve para aplicar ao personagem.", 14); body.AddChild(_status);
        body.AddChild(Ui.Button("Salvar guarda-roupa", Save));
        SelectOutfit(Wardrobe.Resolve(Data, CurrentOutfitId)!);
    }

    SpinBox Spin(VBoxContainer parent, string title, double min, double max, double step, Action<double> changed)
    {
        parent.AddChild(Ui.Text(title, 13)); var spin = new SpinBox { MinValue = min, MaxValue = max, Step = step }; parent.AddChild(spin);
        spin.ValueChanged += v => { if (_loading) return; changed(v); if (_viewport != null) RefreshPreview(); }; return spin;
    }
    OutfitPose Pose => _poses.Selected switch { 1 => _selected.Left, 2 => _selected.Right, _ => _selected.Idle };
    void SetPose(OutfitPose pose) { if (_poses.Selected == 1) _selected.Left = pose; else if (_poses.Selected == 2) _selected.Right = pose; else _selected.Idle = pose; }
    void RefreshList()
    {
        _outfits.Clear(); foreach (var o in Data.Outfits) _outfits.AddItem(o.Name);
        _outfits.Select(Data.Outfits.IndexOf(_selected));
    }
    void SelectOutfit(CharacterOutfit outfit)
    {
        FlushMask(); CancelOperation(); _selected = outfit; _loading = true;
        RefreshList(); _name.Text = outfit.Name; _height.Value = Data.HeightMeters; _feet.Value = outfit.FootOffset; _fps.Value = outfit.Fps; _breath.Value = outfit.Breathing;
        _loading = false; LoadPose(); RefreshPreview();
    }
    void LoadPose()
    {
        FlushMask(); _editingPose = Pose; _loading = true;
        _scale.Value = Pose.Scale; _x.Value = Pose.X; _y.Value = Pose.Y; _loading = false;
        LoadCanvas();
        var poses = _selected.Poses.ToArray();
        for (int i = 0; i < _thumbnails.Count; i++)
        { using var image = Wardrobe.Read(Data, poses[i].Cutout); _thumbnails[i].Texture = image == null ? null : ImageTexture.CreateFromImage(image); }
    }
    void LoadCanvas()
    {
        using var original = Wardrobe.Read(Data, Pose.Original);
        using var cutout = Wardrobe.Read(Data, Pose.Cutout);
        if (original != null && cutout != null) { original.Resize(cutout.GetWidth(), cutout.GetHeight(), Image.Interpolation.Lanczos); _canvas.LoadImages(original, cutout); _canvas.Visible = true; }
        else _canvas.Visible = false;
    }
    void FlushMask() { if (_editingPose != null && _canvas != null && _canvas.Visible) _canvas.SaveTo(Data, _editingPose); }
    void RefreshPreview()
    {
        if (_viewport == null || _selected == null) return;
        if (_animator != null) { _viewport.RemoveChild(_animator); _animator.QueueFree(); }
        _animator = new NpcAnimator { Data = Data, OutfitId = _selected.Id, Paused = _paused }; _viewport.AddChild(_animator);
        if (!_animator.UsesOutfit) _animator.SetLegacyTexture(new PortraitCache().Get(Data, new CharacterState { CurrentOutfitId = _selected.Id }));
        _animator.SetState(_mode, _mode == "Walking" ? Data.WalkSpeed : 0);
        _hint.Text = _selected.UseLegacy ? "Padrão antigo: retratos e animações direcionais preservados. Adicione uma roupa para usar o modo simples." :
            string.IsNullOrEmpty(_selected.Idle.Cutout) ? "Adicione a imagem Parado para começar." :
            string.IsNullOrEmpty(_selected.Left.Cutout) || string.IsNullOrEmpty(_selected.Right.Cutout) ? "Imagem única: respiração e deslocamento estilizado. Faltam poses para alternar os passos." : "Duas poses de passos + pose parada. O recorte acompanha a câmera; não gera costas ou perfil.";
    }
    void PickImage()
    {
        FlushMask(); var outfit = _selected; int index = _poses.Selected;
        var picker = new FileDialog { Access = FileDialog.AccessEnum.Filesystem, FileMode = FileDialog.FileModeEnum.OpenFile, Filters = new[] { "*.png,*.jpg,*.jpeg,*.webp ; Imagens" } }; AddChild(picker);
        picker.FileSelected += path =>
        {
            try
            {
                var pose = Wardrobe.Import(Data, path);
                if (index == 1) outfit.Left = pose; else if (index == 2) outfit.Right = pose; else outfit.Idle = pose;
                outfit.UseLegacy = false; _editingPose = null; LoadPose(); RefreshPreview();
                using var image = Wardrobe.Read(Data, pose.Cutout);
                _status.Text = image!.DetectAlpha() == Image.AlphaMode.None ? "Imagem importada. Remova o fundo automaticamente ou use o pincel." : "Transparência mantida. Confira o recorte; você pode mantê-lo ou remover o fundo novamente.";
                if (image.DetectAlpha() == Image.AlphaMode.None && new LocalCutoutService().Available) _ = RemoveBackground();
            }
            catch (Exception e) { _status.Text = e.Message; }
            picker.QueueFree();
        };
        picker.Canceled += picker.QueueFree; picker.PopupCentered(new(850, 550));
    }
    async Task RemoveBackground()
    {
        FlushMask(); CancelOperation();
        var pose = Pose;
        if (string.IsNullOrEmpty(pose.Original)) { _status.Text = "Selecione uma imagem primeiro."; return; }
        var operation = new CancellationTokenSource(); _operation = operation;
        _status.Text = "Removendo fundo localmente… No primeiro uso, o modelo será baixado. Você pode cancelar.";
        try
        {
            var cache = ProjectSettings.GlobalizePath("user://cutout-cache");
            var result = await new LocalCutoutService().RemoveAsync(Wardrobe.PathFor(Data, pose.Original), cache, operation.Token);
            if (operation.IsCancellationRequested || !IsInsideTree()) return;
            using var image = Image.LoadFromFile(result); image.Convert(Image.Format.Rgba8);
            Wardrobe.LimitWorkingSize(image);
            if (image.GetUsedRect().Size.Y == 0 || image.DetectAlpha() == Image.AlphaMode.None) throw new IOException("O modelo não produziu um recorte útil. Original preservado; use retoque ou outro PNG.");
            pose.Cutout = pose.AutoCutout = Wardrobe.Store(Data, image, "cutouts"); pose.Mask = "";
            if (ReferenceEquals(Pose, pose)) LoadCanvas();
            RefreshPreview(); _status.Text = "Fundo removido. Confira cabelo e mãos na aba Retocar recorte.";
        }
        catch (OperationCanceledException) { if (IsInsideTree()) _status.Text = "Processamento cancelado; imagem preservada."; }
        catch (Exception e) { if (IsInsideTree()) _status.Text = e.Message; }
        finally { if (ReferenceEquals(_operation, operation)) _operation = null; operation.Dispose(); }
    }
    void CancelOperation() { _operation?.Cancel(); }
    void ConfirmDelete()
    {
        if (Data.Outfits.Count < 2) { _status.Text = "Mantenha pelo menos uma roupa."; return; }
        var target = _selected;
        var dialog = new ConfirmationDialog { DialogText = $"Excluir a roupa {target.Name}? Os arquivos originais serão preservados." }; AddChild(dialog);
        dialog.Confirmed += () => { FlushMask(); Data.Outfits.Remove(target); Wardrobe.Migrate(Data); if (CurrentOutfitId == target.Id) CurrentOutfitId = Data.DefaultOutfitId; _editingPose = null; SelectOutfit(Data.Outfits[0]); dialog.QueueFree(); };
        dialog.Canceled += dialog.QueueFree; dialog.PopupCentered();
    }
    void Save()
    {
        try
        {
            if (_operation != null) { _status.Text = "Aguarde o recorte ou cancele antes de salvar."; return; }
            FlushMask();
            foreach (var o in Data.Outfits.Where(o => !o.UseLegacy))
            {
                if (string.IsNullOrWhiteSpace(o.Name)) throw new ArgumentException("Dê um nome a cada roupa.");
                using var idle = Wardrobe.Read(Data, o.Idle.Cutout);
                if (idle == null || idle.GetUsedRect().Size.Y == 0) throw new ArgumentException($"{o.Name}: adicione a imagem Parado ou exclua a roupa incompleta.");
            }
            Data.DefaultOutfitId = Wardrobe.Resolve(Data, CurrentOutfitId)?.Id ?? Data.DefaultOutfitId;
            new CharacterRepository().Save(Data); Saved?.Invoke(Data.DefaultOutfitId);
            _status.Text = "Guarda-roupa salvo. Feche o criador para atualizar o mundo.";
        }
        catch (Exception e) { _status.Text = e.Message; }
    }
    void CloseEditor() { FlushMask(); CancelOperation(); Closed?.Invoke(); }
    public override void _ExitTree() => CancelOperation();
}
