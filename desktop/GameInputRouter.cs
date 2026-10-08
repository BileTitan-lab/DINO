namespace DinoDash.Desktop;

internal sealed class GameInputRouter
{
    private readonly HashSet<int> pressed = new();

    public bool Handle(
        GameEngine engine,
        int keyCode,
        bool keyDown,
        bool hasModifier,
        int jumpKey,
        int crouchKey)
    {
        if (keyCode != jumpKey && keyCode != crouchKey) return false;

        if (!keyDown)
        {
            pressed.Remove(keyCode);
            if (keyCode == crouchKey) engine.CrouchHeld = false;
            return true;
        }

        if (hasModifier) return false;
        if (keyCode == crouchKey)
        {
            pressed.Add(keyCode);
            engine.CrouchHeld = true;
        }
        else if (pressed.Add(keyCode))
        {
            engine.JumpRequested = true;
        }
        return true;
    }

    public void Clear(GameEngine engine)
    {
        pressed.Clear();
        engine.ClearInput();
    }
}
