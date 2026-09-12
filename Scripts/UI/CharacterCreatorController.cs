using Godot;
namespace Heartbeat;

public partial class CharacterCreatorController : Control
{
    public GameSave? Game { get; set; }
    readonly CharacterRepository _repository = new(); CharacterData _data = new();
    VBoxContainer _body = null!; OptionButton _list = null!; Label _status = null!; TextureRect _preview = null!;
    readonly Dictionary<string,LineEdit> _fields = new(); readonly Dictionary<string,TextEdit> _long = new();
    SpinBox _age = null!; LineEdit _tags = null!; OptionButton _images = null!;
    
    // ── Sprite sheet importer controls ──
    SpinBox _sheetCols = null!, _sheetRows = null!, _sheetFps = null!, _sheetFrames = null!, _sheetStart = null!;
    SpinBox _sheetMargin = null!, _sheetSpacing = null!;
    SpinBox _pivotX = null!, _pivotY = null!, _offsetX = null!, _offsetY = null!;
    CheckButton _loopCheck = null!;
    SpinBox _heightSpin = null!, _speedSpin = null!, _interactSpin = null!, _footOffsetSpin = null!;
    LineEdit _homeLocation = null!;
    OptionButton _animTarget = null!;
    
    GridOverlayTextureRect _sheetPreview = null!;
    Label _sheetInfo = null!, _transparencyInfo = null!;
    string _pendingSheetPath = "";
    
    CheckButton _mirrorCheck = null!, _breathCheck = null!;
    public Action? Closed;

    // ── 3D Animated Preview ──
    SubViewport _viewport = null!;
    NpcAnimator _previewAnimator = null!;
    ColorRect _previewBg = null!;
    Label _frameIndicator = null!;
    bool _previewPlaying = true;
    Camera3D _previewCamera = null!;
    float _previewZoom = 3.0f;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Ui.Panel(this,"PERSONAGENS · adultos fictícios",out _body,()=>Closed?.Invoke());
        var row=new HBoxContainer(); _body.AddChild(row); _list=new OptionButton { SizeFlagsHorizontal=SizeFlags.ExpandFill }; row.AddChild(_list);
        var items=_repository.List(); foreach(var c in items) _list.AddItem(c.Name);
        _list.ItemSelected += i=>Edit(items[(int)i]); row.AddChild(Ui.Button("Criar novo",()=>Edit(_repository.Create())));
        _body.AddChild(Ui.Button("Guarda-roupa · criar com 1 a 3 imagens", OpenWardrobe));
        _body.AddChild(Ui.Button("Carta de companheiro · design e golpes", ()=>
        {
            ReadFields();
            var editor=new CompanionCardEditor {Data=_data};
            editor.Closed=editor.QueueFree;
            AddChild(editor);
        }));
        var scroll=new ScrollContainer { SizeFlagsVertical=SizeFlags.ExpandFill }; _body.AddChild(scroll);
        var form=new VBoxContainer { SizeFlagsHorizontal=SizeFlags.ExpandFill }; scroll.AddChild(form);

        // ── Identity fields ──
        foreach(var key in new[]{"Nome","Traits","Gostos","Desgostos","Interesses","Profissão","Hobbies","Estilo de fala"}) { form.AddChild(Ui.Text(key,14)); var field=new LineEdit(); form.AddChild(field); _fields[key]=field; }
        form.AddChild(Ui.Text("Idade (18+)",14)); _age=new SpinBox { MinValue=18,MaxValue=120,Value=28 }; form.AddChild(_age);
        foreach(var key in new[]{"Descrição","Personalidade","Comportamento","Informações adicionais"}) { form.AddChild(Ui.Text(key,14)); var field=new TextEdit { CustomMinimumSize=new(0,90),WrapMode=TextEdit.LineWrappingMode.Boundary }; form.AddChild(field); _long[key]=field; }

        // ── Physical properties ──
        form.AddChild(Ui.Text("─── PROPRIEDADES FÍSICAS globais ───",16));
        form.AddChild(Ui.Text("Altura (metros)",14)); _heightSpin=new SpinBox { MinValue=0.5,MaxValue=2.5,Step=0.01,Value=1.78 }; form.AddChild(_heightSpin);
        form.AddChild(Ui.Text("Velocidade (m/s)",14)); _speedSpin=new SpinBox { MinValue=0.1,MaxValue=5,Step=0.1,Value=1.4 }; form.AddChild(_speedSpin);
        form.AddChild(Ui.Text("Raio de interação (m)",14)); _interactSpin=new SpinBox { MinValue=0.5,MaxValue=10,Step=0.1,Value=2.5 }; form.AddChild(_interactSpin);
        form.AddChild(Ui.Text("Offset vertical dos pés (m)",14)); _footOffsetSpin=new SpinBox { MinValue=-1,MaxValue=1,Step=0.01,Value=0 }; form.AddChild(_footOffsetSpin);
        form.AddChild(Ui.Text("Local de casa/âncora",14)); _homeLocation=new LineEdit { Text="Home",PlaceholderText="Home, Cafe, Park..." }; form.AddChild(_homeLocation);
        _mirrorCheck=new CheckButton { Text="Espelhar esquerda↔direita (roupa simétrica)" }; form.AddChild(_mirrorCheck);
        _breathCheck=new CheckButton { Text="Respiração procedural (desativa com idle animado)",ButtonPressed=true }; form.AddChild(_breathCheck);

        // ── Portrait images ──
        form.AddChild(Ui.Text("─── RETRATOS E EXPRESSÕES (LEGADO) ───",16));
        _tags=new LineEdit { Text="neutral", PlaceholderText="Tags separadas por vírgula; special para cinematográfica" }; form.AddChild(_tags);
        form.AddChild(Ui.Button("Importar retratos PNG / JPG / WebP",ImportPortrait)); _images=new OptionButton(); form.AddChild(_images);
        _images.ItemSelected+=i=>{ if(i>=0 && i<_data.Images.Count) { _data.MainImagePath=_data.Images[(int)i].ProcessedPath; _preview.Texture=new PortraitCache().Get(_data); } };
        form.AddChild(Ui.Button("Aplicar tags à imagem selecionada",()=> { if(_images.Selected>=0 && _images.Selected<_data.Images.Count) _data.Images[_images.Selected].Tags=Split(_tags.Text); }));
        _preview=new TextureRect { CustomMinimumSize=new(0,240),ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.KeepAspectCentered }; form.AddChild(_preview);

        // ── Sprite Sheet Importer ──
        form.AddChild(Ui.Text("─── IMPORTADOR DE SPRITE SHEET ───",16));
        form.AddChild(Ui.Text("Importa faixas de animação e sprite sheets com transparência.",13));

        var sheetRow=new HBoxContainer(); form.AddChild(sheetRow);
        sheetRow.AddChild(Ui.Button("Importar Sprite Sheet",ImportSheet));
        sheetRow.AddChild(Ui.Button("Preset: Faixa 6 frames",()=>{ _sheetCols.Value=6; _sheetRows.Value=1; _sheetFrames.Value=6; _sheetStart.Value=0; _sheetFps.Value=8; _sheetMargin.Value=0; _sheetSpacing.Value=0; }));

        _sheetInfo=Ui.Text("Nenhuma sprite sheet carregada.",13); form.AddChild(_sheetInfo);
        _transparencyInfo=Ui.Text("", 13); _transparencyInfo.Modulate = new Color("ffcc00"); form.AddChild(_transparencyInfo);
        
        _sheetPreview=new GridOverlayTextureRect { CustomMinimumSize=new(0,180),ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.KeepAspectCentered }; form.AddChild(_sheetPreview);

        // Grid controls
        form.AddChild(Ui.Text("── GRADE DA FOLHA",14));
        var gridRow=new HBoxContainer(); form.AddChild(gridRow);
        gridRow.AddChild(Ui.Text("Colunas:",14)); _sheetCols=new SpinBox { MinValue=1,MaxValue=128,Value=6 }; gridRow.AddChild(_sheetCols);
        gridRow.AddChild(Ui.Text("Linhas:",14)); _sheetRows=new SpinBox { MinValue=1,MaxValue=128,Value=1 }; gridRow.AddChild(_sheetRows);
        gridRow.AddChild(Ui.Text("Margem(px):",14)); _sheetMargin=new SpinBox { MinValue=0,MaxValue=128,Value=0 }; gridRow.AddChild(_sheetMargin);
        gridRow.AddChild(Ui.Text("Espaçamento(px):",14)); _sheetSpacing=new SpinBox { MinValue=0,MaxValue=128,Value=0 }; gridRow.AddChild(_sheetSpacing);

        // Frame controls
        form.AddChild(Ui.Text("── RECORTE DE ANIMAÇÃO",14));
        var frameRow=new HBoxContainer(); form.AddChild(frameRow);
        frameRow.AddChild(Ui.Text("Frame inicial:",14)); _sheetStart=new SpinBox { MinValue=0,MaxValue=1024,Value=0 }; frameRow.AddChild(_sheetStart);
        frameRow.AddChild(Ui.Text("Qtd. frames:",14)); _sheetFrames=new SpinBox { MinValue=1,MaxValue=1024,Value=6 }; frameRow.AddChild(_sheetFrames);
        frameRow.AddChild(Ui.Text("FPS:",14)); _sheetFps=new SpinBox { MinValue=1,MaxValue=60,Value=8 }; frameRow.AddChild(_sheetFps);
        _loopCheck=new CheckButton { Text="Loop", ButtonPressed=true }; frameRow.AddChild(_loopCheck);

        // Alignment controls
        form.AddChild(Ui.Text("── ALINHAMENTO E PIVÔ",14));
        var alignRow=new HBoxContainer(); form.AddChild(alignRow);
        alignRow.AddChild(Ui.Text("Pivot X (0-1):",14)); _pivotX=new SpinBox { MinValue=0,MaxValue=1,Step=0.01,Value=0.5 }; alignRow.AddChild(_pivotX);
        alignRow.AddChild(Ui.Text("Pivot Y (0-1):",14)); _pivotY=new SpinBox { MinValue=0,MaxValue=1,Step=0.01,Value=1.0 }; alignRow.AddChild(_pivotY);
        alignRow.AddChild(Ui.Text("Offset X (m):",14)); _offsetX=new SpinBox { MinValue=-5,MaxValue=5,Step=0.01,Value=0 }; alignRow.AddChild(_offsetX);
        alignRow.AddChild(Ui.Text("Offset Y (m):",14)); _offsetY=new SpinBox { MinValue=-5,MaxValue=5,Step=0.01,Value=0 }; alignRow.AddChild(_offsetY);

        // Connect value changes to update preview immediately
        Action updatePreview = () => { UpdatePreviewConfig(); };
        _sheetCols.ValueChanged += _ => updatePreview(); _sheetRows.ValueChanged += _ => updatePreview();
        _sheetMargin.ValueChanged += _ => updatePreview(); _sheetSpacing.ValueChanged += _ => updatePreview();
        _sheetStart.ValueChanged += _ => updatePreview(); _sheetFrames.ValueChanged += _ => updatePreview();
        _sheetFps.ValueChanged += _ => updatePreview(); _loopCheck.Toggled += _ => updatePreview();
        _pivotX.ValueChanged += _ => updatePreview(); _pivotY.ValueChanged += _ => updatePreview();
        _offsetX.ValueChanged += _ => updatePreview(); _offsetY.ValueChanged += _ => updatePreview();
        _heightSpin.ValueChanged += _ => updatePreview(); _footOffsetSpin.ValueChanged += _ => updatePreview();

        // Target animation
        form.AddChild(Ui.Text("Animação alvo:",14));
        _animTarget=new OptionButton(); form.AddChild(_animTarget);
        foreach(var name in new[]{"idle_front","idle_back","idle_left","idle_right","walk_front","walk_back","walk_left","walk_right","talk_front","talk_left","talk_right"})
            _animTarget.AddItem(name);

        form.AddChild(Ui.Button("Salvar animação no personagem",AddAnimation));

        // Animated preview UI
        form.AddChild(Ui.Text("── PREVIEW 3D ANIMADO",16));
        var pbRow = new HBoxContainer(); form.AddChild(pbRow);
        pbRow.AddChild(Ui.Button("Play/Pause", () => { _previewPlaying = !_previewPlaying; _previewAnimator.Paused = !_previewPlaying; }));
        pbRow.AddChild(Ui.Button("<", () => { _previewAnimator.Paused = true; _previewPlaying = false; _previewAnimator.CurrentFrame--; }));
        pbRow.AddChild(Ui.Button(">", () => { _previewAnimator.Paused = true; _previewPlaying = false; _previewAnimator.CurrentFrame++; }));
        _frameIndicator = Ui.Text("Frame 0", 14); pbRow.AddChild(_frameIndicator);
        
        var bgRow = new HBoxContainer(); form.AddChild(bgRow);
        bgRow.AddChild(Ui.Text("Fundo:", 14));
        var bgSelect = new OptionButton(); bgRow.AddChild(bgSelect);
        bgSelect.AddItem("Quadriculado"); bgSelect.AddItem("Claro"); bgSelect.AddItem("Escuro");
        bgSelect.ItemSelected += i => UpdatePreviewBackground((int)i);
        bgRow.AddChild(Ui.Text("  Zoom:", 14));
        var zoomSpin = new SpinBox { MinValue=1, MaxValue=10, Step=0.1, Value=3.0 }; bgRow.AddChild(zoomSpin);
        zoomSpin.ValueChanged += v => { _previewZoom = (float)v; if (_previewCamera != null) _previewCamera.Position = new Vector3(0, 1.2f, _previewZoom); };

        var previewContainer = new Control { CustomMinimumSize = new(256, 256), SizeFlagsHorizontal = SizeFlags.ShrinkCenter }; form.AddChild(previewContainer);
        _previewBg = new ColorRect { Color = new Color("2a2a2a"), AnchorsPreset = (int)LayoutPreset.FullRect }; previewContainer.AddChild(_previewBg);
        
        var vpContainer = new SubViewportContainer { AnchorsPreset = (int)LayoutPreset.FullRect, Stretch = true }; previewContainer.AddChild(vpContainer);
        _viewport = new SubViewport { TransparentBg = true, Size = new Vector2I(256, 256) }; vpContainer.AddChild(_viewport);
        
        // Ground line inside 3D
        var groundMesh = new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(10, 0.02f, 10) }, Position = new Vector3(0, -0.01f, 0), MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("00ff00"), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded } };
        _viewport.AddChild(groundMesh);

        _previewAnimator = new NpcAnimator { Data = _data }; _viewport.AddChild(_previewAnimator);
        _previewCamera = new Camera3D { Position = new Vector3(0, 1.2f, _previewZoom), Fov = 50 }; _viewport.AddChild(_previewCamera);

        UpdatePreviewBackground(0); // init bg

        // Animation list
        form.AddChild(Ui.Text("── ANIMAÇÕES CONFIGURADAS",14));
        form.AddChild(Ui.Button("Listar animações",ListAnimations));

        _status=Ui.Text("A primeira imagem pode ser neutral. Imagens transparentes mantêm o canal alpha.\nNovos campos: altura, velocidade, animações, rotina.",14); _body.AddChild(_status);
        _body.AddChild(Ui.Button("Salvar personagem",Save)); Edit(items.FirstOrDefault()??_repository.Create());
    }

    void UpdatePreviewBackground(int type)
    {
        // 0: Checkered, 1: Light, 2: Dark
        if (type == 0)
        {
            // Simple checkered pattern using a custom shader or simple repeating texture
            // For now, we will just use a middle gray that clearly contrasts
            _previewBg.Color = new Color("666666");
        }
        else if (type == 1) _previewBg.Color = new Color("eeeeee");
        else _previewBg.Color = new Color("1a1a1a");
    }

    void UpdatePreviewConfig()
    {
        if(string.IsNullOrEmpty(_pendingSheetPath) || _sheetPreview.Texture==null) return;
        var cols=(int)_sheetCols.Value; var rows=(int)_sheetRows.Value;
        var margin=(int)_sheetMargin.Value; var spacing=(int)_sheetSpacing.Value;
        
        _sheetPreview.Cols = cols; _sheetPreview.Rows = rows;
        _sheetPreview.MarginPx = margin; _sheetPreview.SpacingPx = spacing;
        _sheetPreview.QueueRedraw();

        // Update 3D preview
        // We use a temporary CharacterData to pass to NpcAnimator so it reloads exactly what is in the UI
        var tempData = new CharacterData
        {
            Id = _data.Id,
            HeightMeters = (float)_heightSpin.Value,
            VisualScale = 1.0f,
            CollisionRadius = 0.35f,
            FootOffset = (float)_footOffsetSpin.Value,
            EnableBreathing = false
        };
        
        tempData.SpriteAnimations.Add(new SpriteAnimationDef
        {
            Name = "preview",
            ImagePath = _pendingSheetPath,
            Columns = cols, Rows = rows,
            Margin = margin, Spacing = spacing,
            StartFrame = (int)_sheetStart.Value,
            FrameCount = (int)_sheetFrames.Value,
            Fps = (float)_sheetFps.Value,
            Loop = _loopCheck.ButtonPressed,
            PivotX = (float)_pivotX.Value,
            PivotY = (float)_pivotY.Value,
            OffsetX = (float)_offsetX.Value,
            OffsetY = (float)_offsetY.Value
        });

        _previewAnimator.Data = tempData;
        _previewAnimator.Reload();
        _previewAnimator.PlayAnimation("preview");
        _previewAnimator.Paused = !_previewPlaying;
    }

    static List<string> Split(string value)=>value.Split(',',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries).ToList();

    void Edit(CharacterData c)
    {
        _data=c; _fields["Nome"].Text=c.Name; _age.Value=Math.Max(18,c.Age);
        _fields["Traits"].Text=string.Join(", ",c.Traits); _fields["Gostos"].Text=string.Join(", ",c.Likes); _fields["Desgostos"].Text=string.Join(", ",c.Dislikes);
        _fields["Interesses"].Text=string.Join(", ",c.Interests); _fields["Profissão"].Text=c.Profession; _fields["Hobbies"].Text=string.Join(", ",c.Hobbies); _fields["Estilo de fala"].Text=c.SpeechStyle;
        _long["Descrição"].Text=c.Description; _long["Personalidade"].Text=c.Personality; _long["Comportamento"].Text=c.BehaviorNotes; _long["Informações adicionais"].Text=c.AdditionalInfo;
        _heightSpin.Value=c.HeightMeters; _speedSpin.Value=c.WalkSpeed; _interactSpin.Value=c.InteractionRadius; _footOffsetSpin.Value=c.FootOffset;
        _homeLocation.Text=c.HomeLocation; _mirrorCheck.ButtonPressed=c.MirrorHorizontal; _breathCheck.ButtonPressed=c.EnableBreathing;
        Images();
        
        // Auto-load first animation into form if exists
        if (_data.SpriteAnimations.Count > 0)
        {
            var a = _data.SpriteAnimations[0];
            _pendingSheetPath = a.ImagePath;
            _sheetCols.Value = a.Columns; _sheetRows.Value = a.Rows;
            _sheetMargin.Value = a.Margin; _sheetSpacing.Value = a.Spacing;
            _sheetStart.Value = a.StartFrame; _sheetFrames.Value = a.FrameCount;
            _sheetFps.Value = a.Fps; _loopCheck.ButtonPressed = a.Loop;
            _pivotX.Value = a.PivotX; _pivotY.Value = a.PivotY;
            _offsetX.Value = a.OffsetX; _offsetY.Value = a.OffsetY;
            
            var destPath = ProjectSettings.GlobalizePath($"user://Characters/{_data.Id}/{_pendingSheetPath}");
            if (!File.Exists(destPath)) destPath = ProjectSettings.GlobalizePath(_pendingSheetPath);
            _sheetPreview.Texture = new PortraitCache().LoadRaw(destPath);
            UpdatePreviewConfig();
        }
    }

    void Images() { _images.Clear(); foreach(var i in _data.Images) _images.AddItem(string.Join(",",i.Tags)+" · "+i.OriginalPath.GetFile()); _preview.Texture=new PortraitCache().Get(_data); }

    void ImportPortrait()
    {
        var picker=new FileDialog { Access=FileDialog.AccessEnum.Filesystem,FileMode=FileDialog.FileModeEnum.OpenFiles,Filters=new[]{"*.png,*.jpg,*.jpeg,*.webp ; Imagens"} }; AddChild(picker);
        picker.FilesSelected+=paths=> { try { foreach(var path in paths) { var folder=$"user://Characters/{_data.Id}/portrait"; var processed=new BackgroundRemovalService().Process(path,folder); var original=folder+"/originals/"+processed.GetFile().GetBaseName()+Path.GetExtension(path); Directory.CreateDirectory(ProjectSettings.GlobalizePath(folder+"/originals")); if(!File.Exists(ProjectSettings.GlobalizePath(original))) File.Copy(path,ProjectSettings.GlobalizePath(original)); _data.Images.Add(new CharacterImage { OriginalPath=original,ProcessedPath=processed,Tags=Split(_tags.Text) }); } Images(); _status.Text="Imagens importadas. Salve o personagem."; } catch(Exception e) { _status.Text="Não foi possível importar: "+e.Message; } picker.QueueFree(); };
        picker.Canceled+=picker.QueueFree; picker.PopupCentered(new(850,550));
    }

    void ImportSheet()
    {
        var picker=new FileDialog { Access=FileDialog.AccessEnum.Filesystem,FileMode=FileDialog.FileModeEnum.OpenFile,Filters=new[]{"*.png,*.webp ; Sprite Sheets (PNG/WebP)"} }; AddChild(picker);
        picker.FileSelected+=path=>
        {
            try
            {
                _pendingSheetPath=path;
                // Copy to character folder
                var folder=$"user://Characters/{_data.Id}/sprites";
                DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(folder));
                var fileName=Path.GetFileName(path);
                var destPath=ProjectSettings.GlobalizePath(folder+"/"+fileName);
                if(!File.Exists(destPath)) File.Copy(path,destPath);
                _pendingSheetPath=folder+"/"+fileName;

                // Load and display
                var cache=new PortraitCache();
                var tex=cache.LoadRaw(destPath);
                if(tex!=null)
                {
                    _sheetPreview.Texture=tex;
                    var w=tex.GetWidth(); var h=tex.GetHeight();
                    var cols=(int)_sheetCols.Value; var rows=(int)_sheetRows.Value;
                    var fw = (w - (int)_sheetMargin.Value * 2 - (int)_sheetSpacing.Value * (Math.Max(1, cols) - 1)) / Math.Max(1, cols);
                    var fh = (h - (int)_sheetMargin.Value * 2 - (int)_sheetSpacing.Value * (Math.Max(1, rows) - 1)) / Math.Max(1, rows);
                    var cellOk=w%cols==0 && h%rows==0; // this is just a hint now, not strict with margins
                    
                    _sheetInfo.Text=$"Tamanho: {w}×{h} px | Grade: {cols}×{rows} | Célula (aprox): {fw}×{fh} px"
                        +(cellOk?"":" ⚠ Verifique margens se a divisão não for exata.");
                        
                    // Check transparency
                    var img = tex.GetImage();
                    bool hasTransp = false;
                    for (int y = 0; y < img.GetHeight() && !hasTransp; y += 4)
                        for (int x = 0; x < img.GetWidth() && !hasTransp; x += 4)
                            if (img.GetPixel(x, y).A < 1.0f) hasTransp = true;
                    
                    _transparencyInfo.Text = hasTransp ? "✓ Imagem possui pixels transparentes." : "⚠ AVISO: Nenhuma transparência detectada na imagem.";
                }
                _status.Text=$"Sprite sheet carregada: {fileName}";
                UpdatePreviewConfig();
            }
            catch(Exception e) { _status.Text="Erro ao importar sheet: "+e.Message; }
            picker.QueueFree();
        };
        picker.Canceled+=picker.QueueFree; picker.PopupCentered(new(850,550));
    }

    void AddAnimation()
    {
        if(string.IsNullOrEmpty(_pendingSheetPath))
        {
            _status.Text="Importe uma sprite sheet primeiro.";
            return;
        }

        var cols=(int)_sheetCols.Value; var rows=(int)_sheetRows.Value;
        var margin=(int)_sheetMargin.Value; var spacing=(int)_sheetSpacing.Value;
        var start=(int)_sheetStart.Value; var count=(int)_sheetFrames.Value;
        var fps=(float)_sheetFps.Value;
        var totalCells=cols*rows;

        // Validate
        if(start+count>totalCells)
        {
            _status.Text=$"Frame inicial ({start}) + quantidade ({count}) excede total de células ({totalCells}).";
            return;
        }

        // Validate dimensions against actual image
        var destPath=ProjectSettings.GlobalizePath(_pendingSheetPath);
        if(!File.Exists(destPath)) destPath = ProjectSettings.GlobalizePath($"user://Characters/{_data.Id}/{_pendingSheetPath}");
        if(File.Exists(destPath))
        {
            using var img=Image.LoadFromFile(destPath);
            if(img!=null)
            {
                var w=img.GetWidth(); var h=img.GetHeight();
                var expectedW = margin * 2 + cols * ((w - margin * 2 - spacing * (cols - 1)) / cols) + spacing * (cols - 1);
                // Math validation can be loose, but user will see it visually.
            }
        }

        var animName=_animTarget.GetItemText(_animTarget.Selected);
        _data.SpriteAnimations.RemoveAll(a=>a.Name==animName);

        _data.SpriteAnimations.Add(new SpriteAnimationDef
        {
            Name=animName,
            ImagePath=_pendingSheetPath,
            Columns=cols, Rows=rows,
            Margin=margin, Spacing=spacing,
            StartFrame=start, FrameCount=count,
            Fps=fps, Loop=_loopCheck.ButtonPressed,
            PivotX=(float)_pivotX.Value, PivotY=(float)_pivotY.Value,
            OffsetX=(float)_offsetX.Value, OffsetY=(float)_offsetY.Value
        });

        _status.Text=$"Animação '{animName}' adicionada: {count} frames a {fps} FPS.";
    }

    void ListAnimations()
    {
        if(_data.SpriteAnimations.Count==0) { _status.Text="Nenhuma animação configurada."; return; }
        var lines=_data.SpriteAnimations.Select(a=>$"  {a.Name}: {a.FrameCount}f @ {a.Fps}fps ({a.Columns}×{a.Rows} start={a.StartFrame})");
        _status.Text="Animações:\n"+string.Join("\n",lines);
    }

    public override void _Process(double delta)
    {
        if (_previewAnimator != null && _previewAnimator.HasAnimations)
        {
            _frameIndicator.Text = $"Frame: {_previewAnimator.CurrentFrame}";
        }
    }

    void ReadFields()
    {
        _data.Name=_fields["Nome"].Text.Trim(); _data.Age=(int)_age.Value;
        _data.Traits=Split(_fields["Traits"].Text); _data.Likes=Split(_fields["Gostos"].Text); _data.Dislikes=Split(_fields["Desgostos"].Text); _data.Interests=Split(_fields["Interesses"].Text); _data.Profession=_fields["Profissão"].Text; _data.Hobbies=Split(_fields["Hobbies"].Text); _data.SpeechStyle=_fields["Estilo de fala"].Text;
        _data.Description=_long["Descrição"].Text; _data.Personality=_long["Personalidade"].Text; _data.BehaviorNotes=_long["Comportamento"].Text; _data.AdditionalInfo=_long["Informações adicionais"].Text;
        _data.HeightMeters=(float)_heightSpin.Value; _data.WalkSpeed=(float)_speedSpin.Value;
        _data.InteractionRadius=(float)_interactSpin.Value; _data.FootOffset=(float)_footOffsetSpin.Value;
        _data.HomeLocation=_homeLocation.Text.Trim(); _data.MirrorHorizontal=_mirrorCheck.ButtonPressed; _data.EnableBreathing=_breathCheck.ButtonPressed;
    }
    void Save()
    {
        ReadFields();
        try { if(string.IsNullOrWhiteSpace(_data.Name)) throw new ArgumentException("Digite o nome."); _repository.Save(_data); _status.Text="Personagem salvo."; } catch(Exception e) { _status.Text=e.Message; }
    }
    void OpenWardrobe()
    {
        ReadFields();
        var current = Game?.CharacterStates.GetValueOrDefault(_data.Id)?.CurrentOutfitId ?? _data.DefaultOutfitId;
        var editor = new WardrobeEditor { Data = _data, CurrentOutfitId = current };
        editor.Saved = outfit =>
        {
            if (Game == null) return;
            if (!Game.CharacterIds.Contains(_data.Id)) Game.CharacterIds.Add(_data.Id);
            if (!Game.CharacterStates.TryGetValue(_data.Id, out var state)) Game.CharacterStates[_data.Id] = state = new();
            state.CurrentOutfitId = outfit;
        };
        editor.Closed = () => { _heightSpin.Value = _data.HeightMeters; editor.QueueFree(); Images(); };
        AddChild(editor);
    }
}

public partial class GridOverlayTextureRect : TextureRect
{
    public int Cols { get; set; } = 1;
    public int Rows { get; set; } = 1;
    public int MarginPx { get; set; } = 0;
    public int SpacingPx { get; set; } = 0;

    public override void _Draw()
    {
        if (Texture == null) return;
        
        var imgW = Texture.GetWidth();
        var imgH = Texture.GetHeight();
        
        // Map image pixels to UI rect dimensions
        var rectSize = Size;
        
        // Calculate scale to fit image inside rect while preserving aspect
        float scaleX = rectSize.X / imgW;
        float scaleY = rectSize.Y / imgH;
        float scale = Math.Min(scaleX, scaleY);
        
        var drawW = imgW * scale;
        var drawH = imgH * scale;
        var offsetX = (rectSize.X - drawW) / 2f;
        var offsetY = (rectSize.Y - drawH) / 2f;

        var color = new Color("ff00ff88"); // Semi-transparent magenta for grid

        var fw = (imgW - MarginPx * 2 - SpacingPx * (Math.Max(1, Cols) - 1)) / (float)Math.Max(1, Cols);
        var fh = (imgH - MarginPx * 2 - SpacingPx * (Math.Max(1, Rows) - 1)) / (float)Math.Max(1, Rows);

        for (int r = 0; r <= Rows; r++)
        {
            var py = MarginPx + r * (fh + SpacingPx) - (r == Rows ? SpacingPx : 0);
            var y = offsetY + py * scale;
            DrawLine(new Vector2(offsetX, y), new Vector2(offsetX + drawW, y), color, 1f);
        }

        for (int c = 0; c <= Cols; c++)
        {
            var px = MarginPx + c * (fw + SpacingPx) - (c == Cols ? SpacingPx : 0);
            var x = offsetX + px * scale;
            DrawLine(new Vector2(x, offsetY), new Vector2(x, offsetY + drawH), color, 1f);
        }
    }
}
