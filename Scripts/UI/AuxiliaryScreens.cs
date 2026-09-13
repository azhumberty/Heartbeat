using Godot;
namespace Heartbeat;

public static class AuxiliaryScreens
{
    public static Control Settings(Control parent, GameSettings settings, Action close)
    {
        var saves=new SaveManager();
        void Persist(){ saves.SaveSettings(settings); }

        var screen=new Control(); parent.AddChild(screen); screen.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var shade=new ColorRect { Color=new Color("03070add") };
        shade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        screen.AddChild(shade);
        var center=new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        screen.AddChild(center);
        var panel=new PanelContainer { CustomMinimumSize=new Vector2(560, 0) };
        panel.SizeFlagsHorizontal=Control.SizeFlags.ShrinkCenter;
        panel.SizeFlagsVertical=Control.SizeFlags.ShrinkCenter;
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat {
            BgColor=new Color("0d1522f2"),
            CornerRadiusTopLeft=14, CornerRadiusTopRight=14, CornerRadiusBottomLeft=14, CornerRadiusBottomRight=14,
            ContentMarginLeft=20, ContentMarginRight=20, ContentMarginTop=14, ContentMarginBottom=14
        });
        center.AddChild(panel);
        var box=new VBoxContainer(); box.AddThemeConstantOverride("separation", 8); panel.AddChild(box);
        var header=new HBoxContainer(); box.AddChild(header);
        var title=Ui.Text("OPCOES", 24); title.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;
        title.AddThemeColorOverride("font_color", new Color("e6c27a"));
        header.AddChild(title);
        var closeBtn=Ui.Button("Fechar e gravar", ()=>{ Persist(); close(); });
        closeBtn.SizeFlagsHorizontal=Control.SizeFlags.ShrinkEnd;
        closeBtn.CustomMinimumSize=new Vector2(180, 40);
        var shortcut=new Shortcut(); shortcut.Events.Add(new InputEventKey { Keycode=Key.Escape });
        closeBtn.Shortcut=shortcut; header.AddChild(closeBtn);

        if (string.IsNullOrWhiteSpace(settings.OpenRouterModel)
            || settings.OpenRouterModel.Contains("dolphin-mistral-24b-venice", StringComparison.OrdinalIgnoreCase)
            || settings.OpenRouterModel.Contains("mistral-7b-instruct", StringComparison.OrdinalIgnoreCase))
            settings.OpenRouterModel = OpenRouterClient.DefaultModel;

        box.AddChild(Toggle("Usar OpenRouter (gratis)", settings.UseOpenRouter, v=>{ settings.UseOpenRouter=v; Persist(); }));
        box.AddChild(Ui.Body("Chave: openrouter.ai/keys  —  cola e grava. O modelo gratuito muda sozinho se um sair do ar.", 13));
        box.AddChild(Field("Chave OpenRouter", settings.OpenRouterApiKey, t=>{ settings.OpenRouterApiKey=t.Trim(); Persist(); }, secret:true, "sk-or-v1-..."));
        box.AddChild(Field("Modelo", settings.OpenRouterModel, t=>{ settings.OpenRouterModel=t.Trim(); Persist(); }));
        box.AddChild(Toggle("Groq como reserva", settings.UseOnlineAi, v=>{ settings.UseOnlineAi=v; Persist(); }));
        box.AddChild(Ui.Text("Volume", 13));
        var volume=new HSlider { MinValue=0, MaxValue=1, Step=.05, Value=settings.Volume, CustomMinimumSize=new Vector2(0, 18) };
        box.AddChild(volume); volume.ValueChanged+=v=>{ settings.Volume=(float)v; AudioServer.SetBusVolumeDb(0, Mathf.LinearToDb(Math.Max(.001f,(float)v))); Persist(); };
        box.AddChild(Toggle("Tela cheia", DisplayServer.WindowGetMode()==DisplayServer.WindowMode.Fullscreen, v=>DisplayServer.WindowSetMode(v?DisplayServer.WindowMode.Fullscreen:DisplayServer.WindowMode.Windowed)));

        var status=Ui.Body(OpenRouterClient.HasKey(settings) ? "Chave gravada. Clica Testar." : "Ainda sem chave.", 14);
        status.AutowrapMode=TextServer.AutowrapMode.WordSmart;
        status.AddThemeColorOverride("font_color", new Color("a0c4e8"));
        box.AddChild(status);
        var testing=false;
        box.AddChild(Ui.Button("TESTAR IA", async ()=>
        {
            if(testing)return; testing=true; Persist(); status.Text="A testar OpenRouter...";
            var r=await new OpenRouterDialogueProvider().ReplyAsync(new CharacterData { Name="Silas", Age=32 }, new CharacterState(), "Ola", settings);
            if(GodotObject.IsInstanceValid(status)) status.Text=r.ProviderStatus;
            testing=false;
        }));
        return screen;
    }

    static CheckBox Toggle(string text, bool on, Action<bool> set)
    {
        var box=new CheckBox { Text=text, ButtonPressed=on };
        box.AddThemeFontSizeOverride("font_size", 15);
        box.Toggled+=v=>set(v);
        return box;
    }

    static Control Field(string label, string value, Action<string> set, bool secret=false, string placeholder="")
    {
        var col=new VBoxContainer(); col.AddThemeConstantOverride("separation", 3);
        col.AddChild(Ui.Text(label, 13));
        var edit=new LineEdit { Text=value, Secret=secret, PlaceholderText=placeholder, CustomMinimumSize=new Vector2(0, 36) };
        edit.AddThemeFontSizeOverride("font_size", 15);
        edit.TextChanged+=t=>set(t);
        col.AddChild(edit);
        return col;
    }

    public static Control Gallery(Control parent, GameSave game, Action close)
    {
        var screen=new Control(); parent.AddChild(screen); screen.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); Ui.Panel(screen,"MOMENTOS",out var body,close);
        var scroll=new ScrollContainer { SizeFlagsVertical=Control.SizeFlags.ExpandFill }; body.AddChild(scroll); var list=new VBoxContainer { SizeFlagsHorizontal=Control.SizeFlags.ExpandFill }; scroll.AddChild(list);
        foreach(var c in new CharacterRepository().List())
        {
            var key=c.Id+":park_first_date"; var unlocked=game.UnlockedCinematics.Contains(key); list.AddChild(Ui.Text(c.Name+" · "+(unlocked?"Um instante no parque":"Momento bloqueado"),22));
            list.AddChild(Ui.Text(unlocked?game.MomentDetails.GetValueOrDefault(key,"Encontro no parque"):"Parque à noite · afeição 40 · confiança 30 · desejo spend_time",14));
            if(unlocked) list.AddChild(new TextureRect { Texture=new PortraitCache().Get(c,cinematic:true),CustomMinimumSize=new(0,240),ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.KeepAspectCentered });
        }
        foreach(var legacy in game.UnlockedCinematics.Where(k=>!k.Contains(':'))) list.AddChild(Ui.Text("Momento preservado do save antigo: "+legacy,16));
        return screen;
    }
}
