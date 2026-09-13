using Godot;
namespace Heartbeat;

public static class AuxiliaryScreens
{
    public static Control Settings(Control parent, GameSettings settings, Action close)
    {
        var screen=new Control(); parent.AddChild(screen); screen.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var shade=new ColorRect { Color=new Color("03070add") };
        shade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        screen.AddChild(shade);
        var center=new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        screen.AddChild(center);
        var panel=new PanelContainer { CustomMinimumSize=new Vector2(620, 0) };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat {
            BgColor=new Color("0d1522f2"),
            CornerRadiusTopLeft=14, CornerRadiusTopRight=14, CornerRadiusBottomLeft=14, CornerRadiusBottomRight=14,
            ContentMarginLeft=22, ContentMarginRight=22, ContentMarginTop=16, ContentMarginBottom=16
        });
        center.AddChild(panel);
        var box=new VBoxContainer(); box.AddThemeConstantOverride("separation", 8); panel.AddChild(box);
        var header=new HBoxContainer(); box.AddChild(header);
        var title=Ui.Text("CONFIGURACOES", 26); title.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;
        title.AddThemeColorOverride("font_color", new Color("e6c27a"));
        header.AddChild(title);
        var closeBtn=Ui.Button("Fechar - Esc", close);
        closeBtn.SizeFlagsHorizontal=Control.SizeFlags.ShrinkEnd;
        closeBtn.CustomMinimumSize=new Vector2(140, 40);
        var shortcut=new Shortcut(); shortcut.Events.Add(new InputEventKey { Keycode=Key.Escape });
        closeBtn.Shortcut=shortcut; header.AddChild(closeBtn);

        var scroll=new ScrollContainer { CustomMinimumSize=new Vector2(0, 420), HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled };
        box.AddChild(scroll);
        var form=new VBoxContainer { SizeFlagsHorizontal=Control.SizeFlags.ExpandFill };
        form.AddThemeConstantOverride("separation", 7);
        scroll.AddChild(form);
        Ui.FitScrollChild(scroll, form);

        form.AddChild(Toggle("IA padrao: OpenRouter Dolphin (gratis, pouca censura)", settings.UseOpenRouter, v=>settings.UseOpenRouter=v));
        form.AddChild(Ui.Body("Cria uma chave em openrouter.ai/keys e cola abaixo. Sem cartao.", 13));
        form.AddChild(Field("Chave OpenRouter", settings.OpenRouterApiKey, t=>settings.OpenRouterApiKey=t.Trim(), secret:true, "sk-or-v1-..."));
        if (string.IsNullOrWhiteSpace(settings.OpenRouterModel) || settings.OpenRouterModel.Contains("mistral-7b-instruct"))
            settings.OpenRouterModel = OpenRouterClient.DefaultModel;
        form.AddChild(Field("Modelo OpenRouter", settings.OpenRouterModel, t=>settings.OpenRouterModel=t.Trim()));
        form.AddChild(Toggle("Usar Groq se o OpenRouter falhar", settings.UseOnlineAi, v=>settings.UseOnlineAi=v));
        form.AddChild(Field("Endpoint Groq", settings.Endpoint, t=>settings.Endpoint=t));
        form.AddChild(Field("Modelo Groq", settings.Model, t=>settings.Model=t));
        form.AddChild(Ui.Text("Volume", 13));
        var volume=new HSlider { MinValue=0, MaxValue=1, Step=.05, Value=settings.Volume, CustomMinimumSize=new Vector2(0, 22) };
        form.AddChild(volume); volume.ValueChanged+=v=>{ settings.Volume=(float)v; AudioServer.SetBusVolumeDb(0, Mathf.LinearToDb(Math.Max(.001f,(float)v))); };
        form.AddChild(Ui.Text("Velocidade do relogio", 13));
        var timeSpeed=new HSlider { MinValue=.5, MaxValue=4, Step=.5, Value=settings.WorldTimeScale, CustomMinimumSize=new Vector2(0, 22) };
        form.AddChild(timeSpeed); timeSpeed.ValueChanged+=v=>settings.WorldTimeScale=(float)v;
        form.AddChild(Toggle("Iniciar com relogio pausado", settings.WorldTimePaused, v=>settings.WorldTimePaused=v));
        form.AddChild(Toggle("Tela cheia", DisplayServer.WindowGetMode()==DisplayServer.WindowMode.Fullscreen, v=>DisplayServer.WindowSetMode(v?DisplayServer.WindowMode.Fullscreen:DisplayServer.WindowMode.Windowed)));

        var status=Ui.Body(OpenRouterClient.HasKey(settings) ? "Chave detetada. Testa a conexao." : "Sem chave: o dialogo fica offline.", 13);
        status.AddThemeColorOverride("font_color", new Color("a0c4e8"));
        box.AddChild(status);
        var testing=false;
        box.AddChild(Ui.Button("Testar OpenRouter", async ()=>
        {
            if(testing)return; testing=true; status.Text="Testando...";
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
        var edit=new LineEdit { Text=value, Secret=secret, PlaceholderText=placeholder, CustomMinimumSize=new Vector2(0, 38) };
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
