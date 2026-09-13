using Godot;
namespace Heartbeat;

public static class AuxiliaryScreens
{
    public static Control Settings(Control parent, GameSettings settings, Action close)
    {
        var screen=new Control(); parent.AddChild(screen); screen.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); Ui.Panel(screen,"CONFIGURACOES",out var body,close);
        var scroll=new ScrollContainer { SizeFlagsVertical=Control.SizeFlags.ExpandFill, HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled };
        body.AddChild(scroll);
        var form=new VBoxContainer { SizeFlagsHorizontal=Control.SizeFlags.ExpandFill };
        form.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(form);
        Ui.FitScrollChild(scroll, form);
        var openRouter=new CheckButton { Text="IA padrao · OpenRouter Dolphin (sem censura, gratis)",ButtonPressed=settings.UseOpenRouter }; form.AddChild(openRouter); openRouter.Toggled+=v=>settings.UseOpenRouter=v;
        form.AddChild(Ui.Body("1) Abre openrouter.ai/keys  2) cria chave gratis  3) cola abaixo. Sem cartao.",14));
        form.AddChild(Ui.Text("Chave OpenRouter",14));
        var key=new LineEdit { Text=settings.OpenRouterApiKey, Secret=true, PlaceholderText="sk-or-v1-...", CustomMinimumSize=new Vector2(0,42) };
        form.AddChild(key); key.TextChanged+=t=>settings.OpenRouterApiKey=t.Trim();
        form.AddChild(Ui.Text("Modelo OpenRouter",14));
        var orModel=new LineEdit { Text=settings.OpenRouterModel, CustomMinimumSize=new Vector2(0,42) };
        form.AddChild(orModel); orModel.TextChanged+=t=>settings.OpenRouterModel=t.Trim();
        var groq=new CheckButton { Text="Usar Groq se OpenRouter falhar",ButtonPressed=settings.UseOnlineAi }; form.AddChild(groq); groq.Toggled+=v=>settings.UseOnlineAi=v;
        foreach(var field in new[]{"Endpoint Groq","Modelo Groq"}) { form.AddChild(Ui.Text(field,14)); var edit=new LineEdit { Text=field.Contains("Modelo")?settings.Model:settings.Endpoint }; form.AddChild(edit); edit.TextChanged+=t=> { if(field.Contains("Modelo")) settings.Model=t; else settings.Endpoint=t; }; }
        var volume=new HSlider { MinValue=0,MaxValue=1,Step=.05,Value=settings.Volume }; form.AddChild(Ui.Text("Volume",14)); form.AddChild(volume); volume.ValueChanged+=v=>{ settings.Volume=(float)v; AudioServer.SetBusVolumeDb(0,Mathf.LinearToDb(Math.Max(.001f,(float)v))); };
        form.AddChild(Ui.Text("Velocidade inicial do relogio",14));
        var timeSpeed=new HSlider { MinValue=.5,MaxValue=4,Step=.5,Value=settings.WorldTimeScale }; form.AddChild(timeSpeed); timeSpeed.ValueChanged+=v=>settings.WorldTimeScale=(float)v;
        var pauseTime=new CheckButton { Text="Iniciar com relogio pausado",ButtonPressed=settings.WorldTimePaused }; form.AddChild(pauseTime); pauseTime.Toggled+=v=>settings.WorldTimePaused=v;
        var fullscreen=new CheckButton { Text="Tela cheia",ButtonPressed=DisplayServer.WindowGetMode()==DisplayServer.WindowMode.Fullscreen }; form.AddChild(fullscreen); fullscreen.Toggled+=v=>DisplayServer.WindowSetMode(v?DisplayServer.WindowMode.Fullscreen:DisplayServer.WindowMode.Windowed);
        var status=Ui.Body(OpenRouterClient.HasKey(settings) ? "Chave OpenRouter detectada. Testa a conexao." : "Sem chave: o jogo fica offline ate colares sk-or-v1-...",14);
        form.AddChild(status);
        var testing=false; form.AddChild(Ui.Button("Testar OpenRouter",async ()=> { if(testing)return; testing=true; status.Text="Testando..."; var r=await new OpenRouterDialogueProvider().ReplyAsync(new CharacterData { Name="Silas",Age=32 },new CharacterState(),"Ola",settings); if(GodotObject.IsInstanceValid(status))status.Text=r.ProviderStatus; testing=false; })); return screen;
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
