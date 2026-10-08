using DinoDash.Desktop;

// Synthetic messages only. Does not switch the user's IME, inject keyboard
// events, take focus, or open a visible window.
internal static class KeyboardChecks
{
    public static void Run()
    {
        (Keys Key, int Scan, bool Extended)[] keys =
        [
            (Keys.A, 0x1e, false), (Keys.D, 0x20, false),
            (Keys.Space, 0x39, false), (Keys.ShiftKey, 0x2a, false),
            (Keys.ShiftKey, 0x36, false), (Keys.J, 0x24, false),
            (Keys.K, 0x25, false), (Keys.S, 0x1f, false),
            (Keys.L, 0x26, false), (Keys.Escape, 0x01, false),
            (Keys.Left, 0x4b, true), (Keys.Right, 0x4d, true)
        ];
        foreach (var item in keys)
        foreach (bool down in new[] { true, false })
        foreach (bool ime in new[] { false, true })
        foreach (bool system in new[] { false, true })
        {
            Message message = Key(item.Key, item.Scan, item.Extended, down, ime, system);
            Assert(GameKeyboard.TryRead(message, out int decoded, out bool isDown) &&
                decoded == (int)item.Key && isDown == down,
                $"Key lost/misread: {item.Key}, down={down}, IME={ime}, system={system}");
        }

        var engine = new ActEngine(12345);
        var bindings = new ActBindings();
        var router = new ActInputRouter();
        void Deliver(Keys key, int scan, bool down)
        {
            Message message = Key(key, scan, false, down, true, false);
            Assert(GameKeyboard.TryRead(message, out int decoded, out bool pressed), "IME key not decoded");
            Assert(router.Handle(engine, bindings, decoded, pressed), "IME key not routed");
        }
        Deliver(Keys.D, 0x20, true);
        Deliver(Keys.ShiftKey, 0x36, true);
        Assert(engine.RightHeld && engine.DashRequested, "IME blocked movement + right Shift");
        Deliver(Keys.D, 0x20, false);
        Assert(!engine.RightHeld, "IME key-up left movement stuck");
        Deliver(Keys.J, 0x24, true);
        Assert(engine.AttackRequested, "IME attack ignored");
        engine.AttackRequested = false;
        Deliver(Keys.J, 0x24, true);
        Assert(!engine.AttackRequested, "IME repeat retriggered attack");
        Deliver(Keys.J, 0x24, false);
        Deliver(Keys.J, 0x24, true);
        Assert(engine.AttackRequested, "IME release did not rearm attack");
        Deliver(Keys.Escape, 0x01, true);
        Assert(router.ConsumePause(), "IME pause ignored");
        router.Clear(engine);
        Assert(!engine.RightHeld && !engine.AttackRequested && !engine.DashRequested,
            "Focus-loss clear retained input");
        Message text = Message.Create(IntPtr.Zero, 0x0102, (IntPtr)'中', IntPtr.Zero);
        Assert(!GameKeyboard.TryRead(text, out _, out _), "Text interpreted as action");
        Message invalid = Message.Create(IntPtr.Zero, 0x0100, (IntPtr)Keys.ProcessKey, IntPtr.Zero);
        Assert(!GameKeyboard.TryRead(invalid, out _, out _), "Unknown IME key invented an action");
        Console.WriteLine("PASS 96 ordinary/IME key message cases; movement, Shift, release, repeat, pause and text isolation.");
    }

    private static Message Key(Keys key, int scan, bool extended, bool down, bool ime, bool system)
    {
        long bits = 1 | ((long)scan << 16) | (extended ? 1L << 24 : 0) |
            (down ? 0 : 3L << 30);
        return Message.Create(IntPtr.Zero, system ? (down ? 0x0104 : 0x0105) : (down ? 0x0100 : 0x0101),
            (IntPtr)(ime ? Keys.ProcessKey : key), (IntPtr)bits);
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
