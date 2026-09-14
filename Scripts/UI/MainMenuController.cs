using Godot;

namespace Heartbeat;

/// <summary>Entry point for the narrative campaign. Its four primary actions are kept deliberately small.</summary>
public partial class MainMenuController : Control
{
	readonly SaveManager _saves=new();
	Control? _screen;

	public override void _Ready()
	{
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);Input.MouseMode=Input.MouseModeEnum.Visible;
		var backdrop=new TextureRect {Texture=ChromaArt.LoadArt("UI/Frames/menu_background.png"),ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.KeepAspectCovered,Modulate=new Color(.62f,.68f,.74f)};
		backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);AddChild(backdrop);
		var veil=new ColorRect {Color=new Color("071015b8"),MouseFilter=MouseFilterEnum.Ignore};veil.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);AddChild(veil);
		var center=new CenterContainer();center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);AddChild(center);
		var panel=new PanelContainer {CustomMinimumSize=new Vector2(490,560)};panel.AddThemeStyleboxOverride("panel",PanelStyle());center.AddChild(panel);
		var body=new VBoxContainer();body.AddThemeConstantOverride("separation",16);panel.AddChild(body);
		var title=Ui.Text("HEARTBEAT",48);title.HorizontalAlignment=HorizontalAlignment.Center;title.AddThemeColorOverride("font_color",new Color("e7c88a"));body.AddChild(title);
		var subtitle=Ui.Text("historias que respiram entre cartas e lembrancas",16);subtitle.HorizontalAlignment=HorizontalAlignment.Center;subtitle.AddThemeColorOverride("font_color",new Color("bdc5c8"));body.AddChild(subtitle);
		body.AddChild(new HSeparator());
		body.AddChild(MenuButton("NOVO JOGO",OpenNewGame));
		body.AddChild(MenuButton("CARREGAR JOGO",LoadGame));
		body.AddChild(MenuButton("CRIATIVO",OpenCreative));
		body.AddChild(MenuButton("OPCOES",OpenOptions));
		body.AddChild(MenuButton("MOMENTOS",OpenGallery));
		body.AddChild(new Control {SizeFlagsVertical=SizeFlags.ExpandFill});
		var worlds=new WorldStore();
		var dropped=worlds.DiscardIncompatible();
		worlds.ImportLegacy(_saves);
		var count=worlds.Count;
		var note=Ui.Text(dropped>0?"Saves antigos foram apagados. Cria um mundo novo.":count==0?"Nenhum mundo ainda.":count==1?"1 mundo a espera.":$"{count} mundos salvos.",14);note.HorizontalAlignment=HorizontalAlignment.Center;note.AddThemeColorOverride("font_color",new Color("9da8ae"));body.AddChild(note);
		GD.Print("[HEARTBEAT] P1 mundos ligado");
		Modulate=new Color(1,1,1,0);CreateTween().TweenProperty(this,"modulate:a",1f,.3);
	}

	static StyleBoxFlat PanelStyle()=>new(){BgColor=new Color("101a21ee"),BorderColor=new Color("c9af7288"),BorderWidthTop=1,BorderWidthBottom=1,BorderWidthLeft=1,BorderWidthRight=1,CornerRadiusTopLeft=18,CornerRadiusTopRight=18,CornerRadiusBottomLeft=18,CornerRadiusBottomRight=18,ContentMarginLeft=42,ContentMarginRight=42,ContentMarginTop=34,ContentMarginBottom=26,ShadowColor=new Color(0,0,0,.65f),ShadowSize=16};
	static Button MenuButton(string text,Action action){var button=Ui.Button(text,action);button.CustomMinimumSize=new Vector2(0,54);button.AddThemeFontSizeOverride("font_size",20);return button;}
	void OpenNewGame(){OpenWorlds();}
	void LoadGame(){OpenWorlds();}
	void OpenWorlds()
	{
		if(_screen!=null)return;
		var worlds=new WorldStore();worlds.ImportLegacy(_saves);
		var select=new WorldSelectController{Settings=_saves.LoadSettings()};
		select.Play=StartCampaign;select.Cancelled=Close;
		_screen=select;AddChild(select);
	}
	void StartCampaign(GameSave save){_saves.Save(save);WorldController.InitialSave=save;GetTree().ChangeSceneToFile("res://Scenes/World/World.tscn");}
	void OpenCreative(){if(_screen!=null)return;var creative=new CreativeModeController {Closed=Close};_screen=creative;AddChild(creative);}
	void OpenOptions(){if(_screen!=null)return;var settings=_saves.LoadSettings();_screen=AuxiliaryScreens.Settings(this,settings,()=>{_saves.SaveSettings(settings);if(_saves.Load() is { } save){save.Settings=settings;_saves.Save(save);}Close();});}
	void OpenGallery()
	{
		if(_screen!=null)return;
		var worlds=new WorldStore();worlds.ImportLegacy(_saves);
		GameSave save;
		if(worlds.TryLoadActive(out var active)&&active!=null) save=active;
		else
		{
			var listed=worlds.List();
			save=listed.Count>0 ? worlds.Load(listed[0].Id)??new GameSave() : _saves.Load()??new GameSave();
		}
		_screen=AuxiliaryScreens.Gallery(this,save,Close);
	}
	void ShowNotice(string text){if(_screen!=null)return;var notice=new Control();_screen=notice;AddChild(notice);Ui.Panel(notice,"HEARTBEAT",out var body,Close);body.AddChild(Ui.Text(text,18));}
	void Close(){if(IsInstanceValid(_screen))_screen.QueueFree();_screen=null;}
	public override void _UnhandledInput(InputEvent e){if(e is InputEventKey {Pressed:true,Keycode:Key.Escape})Close();}
}
