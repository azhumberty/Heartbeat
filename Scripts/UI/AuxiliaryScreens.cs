using Godot;
namespace Heartbeat;

public static class AuxiliaryScreens
{
    public static Control Settings(Control parent, GameSettings settings, Action close)
    {
        var screen=new Control(); parent.AddChild(screen); screen.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); Ui.Panel(screen,"CONFIGURAÇÕES",out var body,close);
        var toggle=new CheckButton { Text="Usar IA online · Groq",ButtonPressed=settings.UseOnlineAi }; body.AddChild(toggle); toggle.Toggled+=v=>settings.UseOnlineAi=v;
        body.AddChild(Ui.Text("As mensagens e memórias recentes são enviadas à Groq ao ativar IA online. A chave vem de GROQ_API_KEY; não é salva no jogo.",15));
        foreach(var field in new[]{"Endpoint","Modelo"}) { body.AddChild(Ui.Text(field,14)); var edit=new LineEdit { Text=field=="Modelo"?settings.Model:settings.Endpoint }; body.AddChild(edit); edit.TextChanged+=t=> { if(field=="Modelo") settings.Model=t; else settings.Endpoint=t; }; }
        var volume=new HSlider { MinValue=0,MaxValue=1,Step=.05,Value=settings.Volume }; body.AddChild(Ui.Text("Volume",14)); body.AddChild(volume); volume.ValueChanged+=v=>{ settings.Volume=(float)v; AudioServer.SetBusVolumeDb(0,Mathf.LinearToDb(Math.Max(.001f,(float)v))); };
        body.AddChild(Ui.Text("Velocidade inicial do relógio",14));
        var timeSpeed=new HSlider { MinValue=.5,MaxValue=4,Step=.5,Value=settings.WorldTimeScale }; body.AddChild(timeSpeed); timeSpeed.ValueChanged+=v=>settings.WorldTimeScale=(float)v;
        var pauseTime=new CheckButton { Text="Iniciar com relógio pausado",ButtonPressed=settings.WorldTimePaused }; body.AddChild(pauseTime); pauseTime.Toggled+=v=>settings.WorldTimePaused=v;
        var fullscreen=new CheckButton { Text="Tela cheia",ButtonPressed=DisplayServer.WindowGetMode()==DisplayServer.WindowMode.Fullscreen }; body.AddChild(fullscreen); fullscreen.Toggled+=v=>DisplayServer.WindowSetMode(v?DisplayServer.WindowMode.Fullscreen:DisplayServer.WindowMode.Windowed);
        var status=Ui.Text(new GroqDialogueProvider().IsConfigured ? "Chave detectada. Use Testar conexão para verificar." : "GROQ_API_KEY ausente. O jogo usará diálogo offline.",15); body.AddChild(status);
        var testing=false; body.AddChild(Ui.Button("Testar conexão",async ()=> { if(testing)return; testing=true; status.Text="Testando…"; var r=await new GroqDialogueProvider().ReplyAsync(new CharacterData { Name="Daniel",Age=28 },new CharacterState(),"Olá",settings); if(GodotObject.IsInstanceValid(status))status.Text=r.ProviderStatus; testing=false; })); return screen;
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
