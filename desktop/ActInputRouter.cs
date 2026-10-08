namespace DinoDash.Desktop;

internal enum ActAction { Left, Right, Jump, Dash, Attack, Tail, Stomp, Ranged, Pause,
    BattleArt1, BattleArt2, Interact, PreviousForm, NextForm, FormLibrary }

internal sealed class ActBindings
{
    public int Left { get; set; } = 65;
    public int Right { get; set; } = 68;
    public int Jump { get; set; } = 32;
    public int Dash { get; set; } = 16;
    public int Attack { get; set; } = 74;
    public int Tail { get; set; } = 75;
    public int Stomp { get; set; } = 83;
    public int Ranged { get; set; } = 76;
    public int Pause { get; set; } = 27;
    public int BattleArt1 { get; set; } = 49;
    public int BattleArt2 { get; set; } = 50;
    public int Interact { get; set; } = 69;
    public int PreviousForm { get; set; } = 81;
    public int NextForm { get; set; } = 82;
    public int FormLibrary { get; set; } = 70;

    public static ActAction[] Actions { get; } =
        [ActAction.Left, ActAction.Right, ActAction.Jump, ActAction.Dash,
            ActAction.Attack, ActAction.Tail, ActAction.Stomp,
            ActAction.Ranged, ActAction.Pause, ActAction.BattleArt1,
            ActAction.BattleArt2, ActAction.Interact, ActAction.PreviousForm, ActAction.NextForm, ActAction.FormLibrary];

    public int Get(ActAction action) => action switch
    {
        ActAction.Left => Left, ActAction.Right => Right, ActAction.Jump => Jump,
        ActAction.Dash => Dash, ActAction.Attack => Attack,
        ActAction.Tail => Tail, ActAction.Stomp => Stomp,
        ActAction.Ranged => Ranged, ActAction.BattleArt1 => BattleArt1,
        ActAction.BattleArt2 => BattleArt2, ActAction.Interact => Interact,
        ActAction.PreviousForm => PreviousForm, ActAction.NextForm => NextForm, ActAction.FormLibrary => FormLibrary, _ => Pause
    };

    public void Set(ActAction action, int key)
    {
        switch (action)
        {
            case ActAction.Left: Left = key; break;
            case ActAction.Right: Right = key; break;
            case ActAction.Jump: Jump = key; break;
            case ActAction.Dash: Dash = key; break;
            case ActAction.Attack: Attack = key; break;
            case ActAction.Tail: Tail = key; break;
            case ActAction.Stomp: Stomp = key; break;
            case ActAction.Ranged: Ranged = key; break;
            case ActAction.Pause: Pause = key; break;
            case ActAction.BattleArt1: BattleArt1 = key; break;
            case ActAction.BattleArt2: BattleArt2 = key; break;
            case ActAction.Interact: Interact = key; break;
            case ActAction.PreviousForm: PreviousForm = key; break;
            case ActAction.NextForm: NextForm = key; break;
            case ActAction.FormLibrary: FormLibrary = key; break;
        }
    }

    public bool Valid() => Actions.Select(Get).Distinct().Count() == Actions.Length &&
        Actions.All(action => Get(action) > 0);

    public ActBindings Clone() => new()
    {
        Left = Left, Right = Right, Jump = Jump, Dash = Dash, Attack = Attack,
        Tail = Tail, Stomp = Stomp, Ranged = Ranged, Pause = Pause,
        BattleArt1 = BattleArt1, BattleArt2 = BattleArt2, Interact = Interact,
        PreviousForm=PreviousForm,NextForm=NextForm,FormLibrary=FormLibrary
    };
}

internal sealed class ActInputRouter
{
    private readonly HashSet<int> pressed = [];
    public bool PauseRequested { get; private set; }

    public bool Handle(ActEngine engine, ActBindings bindings, int key, bool down)
    {
        ActAction? action = ActBindings.Actions.Cast<ActAction?>()
            .FirstOrDefault(item => bindings.Get(item!.Value) == key);
        if (action is null) return false;
        if (!down) pressed.Remove(key);
        else if (!pressed.Add(key)) return true;

        switch (action)
        {
            case ActAction.Left: engine.LeftHeld = down; break;
            case ActAction.Right: engine.RightHeld = down; break;
            case ActAction.Jump:
                engine.JumpHeld = down;
                if (down) engine.JumpRequested = true;
                break;
            case ActAction.Dash: if (down) engine.DashRequested = true; break;
            case ActAction.Attack: if (down) engine.AttackRequested = true; break;
            case ActAction.Tail: if (down) engine.TailRequested = true; break;
            case ActAction.Stomp: if (down) engine.StompRequested = true; break;
            case ActAction.Ranged: if (down) engine.RangedRequested = true; break;
            case ActAction.Pause: if (down) PauseRequested = true; break;
            case ActAction.BattleArt1: if (down) engine.BattleArt1Requested = true; break;
            case ActAction.BattleArt2: if (down) engine.BattleArt2Requested = true; break;
            case ActAction.Interact: if (down) engine.InteractRequested = true; break;
            case ActAction.PreviousForm: if (down) engine.FormStepRequested = -1; break;
            case ActAction.NextForm: if (down) engine.FormStepRequested = 1; break;
            case ActAction.FormLibrary: if (down) engine.FormMenuRequested = true; break;
        }
        return true;
    }

    public bool ConsumePause()
    {
        bool requested = PauseRequested;
        PauseRequested = false;
        return requested;
    }

    public void Clear(ActEngine engine)
    {
        pressed.Clear();
        PauseRequested = false;
        engine.ClearInput();
    }
}
