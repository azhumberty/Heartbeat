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
		var backdrop=new ColorRect {Color=new Color("182735")};
		backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);AddChild(backdrop);
		var veil=new ColorRect {Color=new Color("071015b8"),MouseFilter=MouseFilterEnum.Ignore};veil.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);AddChild(veil);
		var center=new CenterContainer();center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);AddChild(center);
		var panel=new PanelContainer {CustomMinimumSize=new Vector2(490,500)};panel.AddThemeStyleboxOverride("panel",PanelStyle());center.AddChild(panel);
		var body=new VBoxContainer();body.AddThemeConstantOverride("separation",16);panel.AddChild(body);
		var title=Ui.Text("HEARTBEAT",48);title.HorizontalAlignment=HorizontalAlignment.Center;title.AddThemeColorOverride("font_color",new Color("e7c88a"));body.AddChild(title);
		var subtitle=Ui.Text("histórias que respiram entre cartas e lembranças",16);subtitle.HorizontalAlignment=HorizontalAlignment.Center;subtitle.AddThemeColorOverride("font_color",new Color("bdc5c8"));body.AddChild(subtitle);
		body.AddChild(new HSeparator());
		body.AddChild(MenuButton("NOVO JOGO",OpenNewGame));
		body.AddChild(MenuButton("CARREGAR JOGO",LoadGame));
		body.AddChild(MenuButton("CRIATIVO",OpenCreative));
		body.AddChild(MenuButton("OPÇÕES",OpenOptions));
		body.AddChild(new Control {SizeFlagsVertical=SizeFlags.ExpandFill});
		var note=Ui.Text(_saves.Load()==null?"Nenhuma campanha salva ainda.":"Um save está pronto para continuar.",14);note.HorizontalAlignment=HorizontalAlignment.Center;note.AddThemeColorOverride("font_color",new Color("9da8ae"));body.AddChild(note);
		Modulate=new Color(1,1,1,0);CreateTween().TweenProperty(this,"modulate:a",1f,.3);
	}

	static StyleBoxFlat PanelStyle()=>new(){BgColor=new Color("101a21ee"),BorderColor=new Color("c9af7288"),BorderWidthTop=1,BorderWidthBottom=1,BorderWidthLeft=1,BorderWidthRight=1,CornerRadiusTopLeft=18,CornerRadiusTopRight=18,CornerRadiusBottomLeft=18,CornerRadiusBottomRight=18,ContentMarginLeft=42,ContentMarginRight=42,ContentMarginTop=34,ContentMarginBottom=26,ShadowColor=new Color(0,0,0,.65f),ShadowSize=16};
	static Button MenuButton(string text,Action action){var button=Ui.Button(text,action);button.CustomMinimumSize=new Vector2(0,54);button.AddThemeFontSizeOverride("font_size",20);return button;}
	void OpenNewGame(){if(_screen!=null)return;var flow=new NewGameController();flow.Completed=StartCampaign;flow.Cancelled=Close;_screen=flow;AddChild(flow);}
	void StartCampaign(GameSave save){_saves.Save(save);WorldController.InitialSave=save;GetTree().ChangeSceneToFile("res://Scenes/World/World.tscn");}
	void LoadGame(){var save=_saves.Load();if(save==null){ShowNotice("Ainda não existe uma campanha para carregar.");return;}WorldController.InitialSave=save;GetTree().ChangeSceneToFile("res://Scenes/World/World.tscn");}
	void OpenCreative(){if(_screen!=null)return;var creative=new CreativeModeController {Closed=Close};_screen=creative;AddChild(creative);}
	void OpenOptions(){if(_screen!=null)return;var settings=_saves.Load()?.Settings??new GameSettings();_screen=AuxiliaryScreens.Settings(this,settings,()=>{if(_saves.Load() is { } save){save.Settings=settings;_saves.Save(save);}Close();});}
	void ShowNotice(string text){if(_screen!=null)return;var notice=new Control();_screen=notice;AddChild(notice);Ui.Panel(notice,"HEARTBEAT",out var body,Close);body.AddChild(Ui.Text(text,18));}
	void Close(){if(IsInstanceValid(_screen))_screen.QueueFree();_screen=null;}
	public override void _UnhandledInput(InputEvent e){if(e is InputEventKey {Pressed:true,Keycode:Key.Escape})Close();}
}
