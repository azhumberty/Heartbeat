using Godot;
namespace Heartbeat;

public partial class CombatArenaController
{
    void Refresh()
    {
        var s=Manager.State;
        _playerHpText.Text=$"Vida {s.Player.Health}/{s.Player.MaxHealth} · Escudo {s.Player.Shield}";
        _playerManaText.Text=$"Mana {s.Mana}/{s.MaxMana}";
        _enemyHpText.Text=$"Vida {s.Enemy.Health}/{s.Enemy.MaxHealth} · Escudo {s.Enemy.Shield}"+((Status(s.Enemy).Length>0)?" · "+Status(s.Enemy):"");
        _playerHealth.MaxValue=s.Player.MaxHealth;_enemyHealth.MaxValue=s.Enemy.MaxHealth;_playerMana.MaxValue=s.MaxMana;
        var hpTween=CreateTween(); hpTween.SetParallel(true);
        hpTween.TweenProperty(_playerHealth,"value",(double)s.Player.Health,.28).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        hpTween.TweenProperty(_enemyHealth,"value",(double)s.Enemy.Health,.28).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        hpTween.TweenProperty(_playerMana,"value",(double)s.Mana,.22);
        RefreshIntents();
        _turn.Text=$"TURNO {s.Turn} · Monte {s.DrawPile.Count} / Descarte {s.Discard.Count}";
        CardUi.Clear(_hand);
        for(int i=0;i<s.Hand.Count;i++)
        {
            int index=i;var data=CardRules.Copy(s.Cards[s.Hand[i]]);
            if(data.CharacterId.Length>0&&data.ImagePath==""){var person=new CharacterRepository().Load(data.CharacterId);if(person!=null)data.ImagePath=CardArt.PathFor(person,Game.CharacterStates.GetValueOrDefault(person.Id));}
            var card=new CardView {Card=data,Compact=true,Clicked=()=>Select(index)};_hand.AddChild(card);
            card.Modulate=new Color(1,1,1,0);card.Scale=new Vector2(.92f,.92f);
            var appear=card.CreateTween(); appear.SetParallel(true);
            appear.TweenProperty(card,"modulate:a",1f,.18+i*.03).SetTrans(Tween.TransitionType.Sine);
            appear.TweenProperty(card,"scale",Vector2.One,.2+i*.03).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            if(s.Mana<data.Cost)card.SelfModulate=new Color(.6f,.6f,.6f);
        }
        _selected=-1;CardUi.Clear(_detail);_detail.AddChild(Ui.Text("SELECIONE UMA CARTA",16));_detail.AddChild(Ui.Text(Status(s.Player),13));_play.Disabled=true;
        _end.Disabled=Busy||s.Result.Length>0;_flee.Disabled=Busy||s.Result.Length>0;
        if(s.Result.Length>0)
        {
            Manager.Settle();Changed?.Invoke();_message.Text=(s.Result=="Victory"?"VITÓRIA":s.Result=="Defeat"?"DERROTA":"RETIRADA")+"\n"+s.RewardText;_return.Visible=true;_end.Visible=false;_flee.Visible=false;
        }
    }
    public void Select(int index)
    {
        if(Busy||Manager.State.Result.Length>0||index<0||index>=Manager.State.Hand.Count)return;
        _selected=index;var c=Manager.State.Cards[Manager.State.Hand[index]];
        CardUi.Clear(_detail);_detail.AddChild(Ui.Text(c.Name,22));_detail.AddChild(Ui.Text(c.Description,15));_detail.AddChild(Ui.Text(CardRules.Describe(c),16));
        if(c.Passive!=null)_detail.AddChild(Ui.Text("Passiva por turno: "+CardRules.EffectName(c.Passive.Kind)+" "+c.Passive.Value,14));
        string reason=Manager.CanPlay(index);_play.Disabled=reason.Length>0;_message.Text=reason.Length>0?reason:c.Phrase;
    }
    async Task Delay(double seconds)=>await ToSignal(GetTree().CreateTimer(seconds),SceneTreeTimer.SignalName.Timeout);
}
