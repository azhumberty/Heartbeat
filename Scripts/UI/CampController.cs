using Godot;

namespace Heartbeat;

/// <summary>Camp roster: every world person, recruit, talk, companion card, moments.</summary>
public partial class CampController : Control
{
    public GameSave Game { get; set; } = null!;
    public Action? Closed;
    public Action<string>? TalkRequested;
    public Action? Rested;
    VBoxContainer _list = null!;
    Label _status = null!;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var panel = new PanelContainer();
        panel.SetAnchorsPreset(LayoutPreset.BottomWide);
        panel.OffsetLeft = 40;
        panel.OffsetRight = -40;
        panel.OffsetTop = -380;
        panel.OffsetBottom = -18;
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color("081019ee"),
            BorderColor = new Color("b9985d99"),
            BorderWidthTop = 1, BorderWidthBottom = 1, BorderWidthLeft = 1, BorderWidthRight = 1,
            CornerRadiusTopLeft = 14, CornerRadiusTopRight = 14, CornerRadiusBottomLeft = 14, CornerRadiusBottomRight = 14,
            ContentMarginLeft = 18, ContentMarginRight = 18, ContentMarginTop = 14, ContentMarginBottom = 14
        });
        AddChild(panel);
        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 8);
        panel.AddChild(body);
        body.AddChild(Ui.Text("ACAMPAMENTO", 24));
        body.AddChild(Ui.Text("Só mora aqui quem convidares. Baralho começa fraco. Poções no inventário.", 14));
        _status = Ui.Text("", 14);
        _status.AddThemeColorOverride("font_color", new Color("c9b27a"));
        body.AddChild(_status);
        _list = new VBoxContainer();
        _list.AddThemeConstantOverride("separation", 8);
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 160) };
        scroll.AddChild(_list);
        Ui.FitScrollChild(scroll, _list);
        body.AddChild(scroll);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        body.AddChild(row);
        row.AddChild(Ui.Button("Descansar", Rest));
        row.AddChild(Ui.Button("Baralho", OpenDeck));
        row.AddChild(Ui.Button("Poção", Drink));
        row.AddChild(Ui.Button("Momentos", ShowMoments));
        row.AddChild(Ui.Button("Voltar ao atlas", () => Closed?.Invoke()));
        Refresh();
    }

    public void Refresh()
    {
        foreach (var child in _list.GetChildren()) child.QueueFree();
        var people = WorldCast.For(Game)
            .Where(c => Game.CampResidents.Contains(c.Id))
            .GroupBy(c => c.Id).Select(g => g.First()).ToList();
        if (people.Count == 0)
            _list.AddChild(Ui.Text("Acampamento vazio. Convida no perfil ou na conversa (afeto 50).", 15));
        foreach (var data in people)
        {
            if (!Game.CharacterStates.TryGetValue(data.Id, out var state)) { state = new CharacterState(); Game.CharacterStates[data.Id] = state; }
            _list.AddChild(PersonRow(data, state));
        }
        _status.Text = Inventory.Label(Game);
    }

    Control PersonRow(CharacterData data, CharacterState state)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        var portrait = new TextureRect
        {
            Texture = new PortraitCache().Get(data, state),
            CustomMinimumSize = new Vector2(64, 88),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
        };
        row.AddChild(portrait);
        if (!string.IsNullOrWhiteSpace(data.GeneratedPortraitPath) || data.Tags.Contains("generated"))
            ChromaArt.ApplyChroma(portrait, 0.42f);
        PortraitMotion.Breath(portrait);
        var col = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        col.AddThemeConstantOverride("separation", 2);
        var resident = Game.CampResidents.Contains(data.Id);
        var role = data.CanBuildRelationship ? state.Relationship : (data.Profession.Length>0?data.Profession:"visita");
        col.AddChild(Ui.Text(data.Name+(resident?" · mora aqui":""), 18));
        col.AddChild(Ui.Text($"{role} · afeto {state.Affection} · conf {state.Trust} · rom {state.Romance}", 13));
        if (data.CanBuildRelationship) col.AddChild(Ui.Text(CompanionProgress.Label(Game, data, state)+" · "+MomentsHint(data), 13));
        row.AddChild(col);
        var actions = new VBoxContainer();
        var captured = data.Id;
        actions.AddChild(Ui.Button("Falar", () => TalkRequested?.Invoke(captured)));
        if (data.CanBuildRelationship && !resident)
        {
            var invite = Ui.Button("Convidar", () => Invite(captured, data));
            invite.Disabled = !CampService.CanInvite(Game, data, state);
            actions.AddChild(invite);
        }
        row.AddChild(actions);
        return row;
    }

    string MomentsHint(CharacterData data)
    {
        var beats = SocialBeatService.BeatsFor(data);
        int n = beats.Count(b => Game.UnlockedCinematics.Contains(SocialBeatService.Key(data.Id, b.Id)));
        return n+"/"+beats.Count+" momentos";
    }

    void Invite(string id, CharacterData data)
    {
        if(!Game.CharacterStates.TryGetValue(id,out var state)||!CampService.Invite(Game,data,state))return;
        SocialBeatService.TryUnlock(Game, data, state, "camp");
        DeckManager.SyncUnlocks(Game, new CardRepository().Catalog());
        _status.Text = $"{data.Name} aceitou um lugar junto a fogueira. Carta de companheiro pronta.";
        Rested?.Invoke();
        Refresh();
    }

    void Rest()
    {
        Game.Player.Health = Game.Player.MaxHealth;
        Game.Player.Mana = Game.Player.MaxMana;
        Game.Player.Energy = 100;
        _status.Text = "Descanso completo. "+Inventory.Label(Game);
        Rested?.Invoke();
        Refresh();
    }

    void Drink()
    {
        if (!Inventory.UseHp(Game, 25)) { _status.Text = "Sem poções de vida."; return; }
        _status.Text = "Bebeste uma poção. "+Inventory.Label(Game);
        Rested?.Invoke();
        Refresh();
    }

    void OpenDeck()
    {
        DeckEditor? ed = null;
        ed = new DeckEditor
        {
            Game = Game,
            Closed = () => { if (GodotObject.IsInstanceValid(ed)) ed!.QueueFree(); },
            Saved = () => Rested?.Invoke()
        };
        AddChild(ed);
    }

    void ShowMoments()
    {
        try
        {
            var host = GetTree()?.Root ?? this;
            Control? gal=null;
            gal=AuxiliaryScreens.Gallery(host, Game, () => { if(GodotObject.IsInstanceValid(gal)) gal.QueueFree(); });
        }
        catch (Exception e)
        {
            GD.PushWarning("[Momentos] " + e.GetType().Name);
            _status.Text = "Momentos indisponiveis.";
        }
    }
}
