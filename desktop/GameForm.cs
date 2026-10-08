using System.Diagnostics;

namespace DinoDash.Desktop;

internal enum ActPlayState { MainMenu, Entering, Ready, Running, Reward, Paused, Dying, Dead, Complete }

internal sealed class GameForm : Form, IMessageFilter
{
    private readonly ActEngine engine;
    private readonly bool debugMode;
    private readonly GameSettings settings = SettingsStore.Load();
    private readonly ActInputRouter input = new();
    private readonly GameCanvas canvas;
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 8 };
    private readonly Stopwatch clock = new();
    private readonly Label hintLabel = new();
    private readonly Label debugLabel = new();
    private readonly ToolTip debugTip = new();
    private readonly Button pauseButton = new ActUiButton();
    private readonly Button settingsButton = new ActUiButton();
    private readonly Button mapDebugButton = new ActUiButton();
    private readonly Panel gameHost = new();
    private readonly ActMainMenu mainMenu = new();
    private readonly Panel overlay = new ActUiPanel();
    private readonly Label overlayTitle = new();
    private readonly Label overlayCaption = new();
    private readonly Label overlayMessage = new();
    private readonly Button overlayButton = new ActUiButton();
    private readonly Button sameSeedButton = new ActUiButton();
    private readonly Button pauseSettings = new ActUiButton(), pauseMap = new ActUiButton(), pauseRestart = new ActUiButton(), pauseHome = new ActUiButton();
    private ActPlayState state = ActPlayState.MainMenu;
    private double lastTime;
    private float accumulator;
    private bool pauseKeyHeld;
    private float deathDelay;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            timer.Dispose();
            debugTip.Dispose();
            Application.RemoveMessageFilter(this);
        }
        base.Dispose(disposing);
    }

    public GameForm(int? seed = null, bool debugMode = false)
    {
        this.debugMode = debugMode;
        engine = new ActEngine(seed,roomAdventure:true);
        canvas = new GameCanvas(engine) { Dock = DockStyle.Fill,
            Bindings = settings.Bindings };
        Text = "恐龙行动";
        AutoScaleMode = AutoScaleMode.None;
        var work = Screen.FromPoint(Cursor.Position).WorkingArea;
        ClientSize = new Size(Math.Clamp(work.Width - 80, 780, 1440),
            Math.Clamp(work.Height - 100, 540, 810));
        MinimumSize = new Size(780, 540);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(18, 18, 18);
        Font = new Font("Microsoft YaHei UI", 10f);
        KeyPreview = true;
        ImeMode = ImeMode.Disable;
        BuildInterface();
        canvas.FormLibraryRequested+=OpenFormLibrary;
        UpdateHints();
        UpdateHud();
        UpdateDebugLabel();
        overlay.Visible = false;
        overlayButton.Text = "继续游戏";
        gameHost.Controls.Add(mainMenu);
        mainMenu.BringToFront();
        mainMenu.Adventure.Click += (_, _) => StartAdventure();
        mainMenu.Settings.Click += (_, _) => OpenSettings();
        mainMenu.ExitGame.Click += (_, _) => Close();
        Shown += (_, _) => mainMenu.Adventure.Focus();
        clock.Start();
        timer.Tick += Tick;
        timer.Start();
        Deactivate += (_, _) =>
        {
            pauseKeyHeld = false;
            if (state == ActPlayState.Running) Pause();
        };
        FormClosed += (_, _) => Application.RemoveMessageFilter(this);
        Application.AddMessageFilter(this);
    }

    private void BuildInterface()
    {
        // The game owns the client area. All interface controls are children of
        // the canvas, so no external header/footer can steal space from combat.
        gameHost.Dock = DockStyle.Fill;
        gameHost.Padding = Padding.Empty;
        gameHost.Controls.Add(canvas);
        Controls.Add(gameHost);
        pauseButton.Text = "暂停";
        settingsButton.Text = "按键设置";
        mapDebugButton.Text = "地图调试";
        foreach (Button button in new[] { pauseButton, settingsButton, mapDebugButton })
        {
            StyleButton(button);
            canvas.Controls.Add(button);
        }
        pauseButton.Enabled = false;
        debugLabel.Font = new Font("Consolas", 9f);
        debugLabel.ForeColor = ActHudRenderer.Muted;
        debugLabel.BackColor = ActHudRenderer.Panel;
        debugLabel.AutoEllipsis = true;
        debugLabel.Cursor = Cursors.Hand;
        debugLabel.Visible = debugMode;
        canvas.Controls.Add(debugLabel);

        overlay.BackColor = Color.Transparent;
        overlay.BorderStyle = BorderStyle.None;
        overlayTitle.TextAlign = overlayCaption.TextAlign = overlayMessage.TextAlign =
            hintLabel.TextAlign = ContentAlignment.MiddleCenter;
        overlayTitle.Font = new Font("Microsoft YaHei UI", 23f, FontStyle.Bold);
        overlayCaption.Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold);
        overlayMessage.Font = new Font("Microsoft YaHei UI", 10f);
        hintLabel.Font = new Font("Microsoft YaHei UI", 9f);
        hintLabel.ForeColor = ActHudRenderer.Muted;
        StyleButton(overlayButton);
        overlayButton.BackColor = ActHudRenderer.Gold;
        overlayButton.ForeColor = ActHudRenderer.Panel;
        StyleButton(sameSeedButton);
        sameSeedButton.Text = "同种子重开";
        sameSeedButton.Visible = false;
        overlay.Controls.AddRange([overlayTitle, overlayCaption, overlayMessage,
            hintLabel, overlayButton, sameSeedButton,pauseSettings,pauseMap,pauseRestart,pauseHome]);
        foreach(var label in new[]{overlayTitle,overlayCaption,overlayMessage,hintLabel})label.BackColor=Color.Transparent;
        pauseSettings.Text="按键设置";pauseMap.Text="地图调试";pauseRestart.Text="重新开始";pauseHome.Text="返回主菜单";
        foreach(var button in new[]{pauseSettings,pauseMap,pauseRestart,pauseHome}){StyleButton(button);button.Visible=false;}
        pauseSettings.Click+=(_,_)=>OpenSettings();pauseMap.Click+=(_,_)=>OpenMapDebugDialog();
        pauseRestart.Click+=(_,_)=>{engine.Reset();state=ActPlayState.Ready;canvas.ResetFx();BeginRun();};
        pauseHome.Click+=(_,_)=>{input.Clear(engine);engine.Reset();canvas.ResetFx();overlay.Visible=false;canvas.Dimmed=false;state=ActPlayState.MainMenu;mainMenu.ShowMenu();};
        canvas.Controls.Add(overlay);
        overlay.BringToFront();
        canvas.Resize += (_, _) => LayoutInterface();
        DpiChanged += (_, _) => LayoutInterface();
        LayoutInterface();
        pauseButton.Click += (_, _) => { if (state == ActPlayState.Running) Pause(); };
        settingsButton.Click += (_, _) => OpenSettings();
        mapDebugButton.Click += (_, _) => OpenMapDebugDialog();
        debugLabel.Click += (_, _) =>
        {
            if (debugMode) MessageBox.Show(this,
                $"Seed: {engine.Seed}\n\n" +
                string.Join("\n", engine.Layout.Rooms.Select(room => room.Summary)) +
                "\n\n捷径: " + string.Join("、",
                    engine.Layout.Shortcuts.Select(shortcut => shortcut.Id)),
                "当前地图调试信息");
        };
        overlayButton.Click += (_, _) => BeginRun();
        sameSeedButton.Click += (_, _) => BeginRun(true);
    }

    private static void StyleButton(Button button)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = Color.FromArgb(94, 91, 82);
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(74, 70, 60);
        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(100, 89, 63);
        button.BackColor = ActHudRenderer.Panel;
        button.ForeColor = ActVisualTheme.Paper;
        button.Font = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold);
        button.TextAlign = ContentAlignment.MiddleCenter;
        button.UseCompatibleTextRendering = false;
        button.Padding = Padding.Empty;
        button.Margin = Padding.Empty;
        button.Cursor = Cursors.Hand;
    }

    private void LayoutInterface()
    {
        float scale = ActHudRenderer.UiScale(canvas.ClientSize, DeviceDpi);
        // Match native control fonts to the canvas HUD, including a small
        // window on a monitor with 150% / 200% display scaling.
        float fontScale = scale * 96f / DeviceDpi;
        foreach (Button button in new[] { pauseButton, settingsButton, mapDebugButton, overlayButton, sameSeedButton })
            FitFont(button, 10f * fontScale);
        FitFont(overlayTitle, 23f * fontScale);
        FitFont(overlayCaption, 9f * fontScale);
        FitFont(overlayMessage, 10f * fontScale);
        FitFont(hintLabel, 9f * fontScale);
        FitFont(debugLabel, 9f * fontScale);
        int inset = (int)(18 * scale), gap = (int)(8 * scale);
        int right = canvas.ClientSize.Width - inset;
        // Actual font measurement, not the WinForms 75 x 23 default. This also
        // reserves enough height for Chinese text on high-DPI monitors.
        foreach (Button button in new[] { settingsButton, pauseButton, mapDebugButton })
        {
            Size text = TextRenderer.MeasureText(button.Text, button.Font);
            int extras=button is ActUiButton styled?(styled.HasSymbol?28:0)+(styled.KeyHint.Length>0?35:0):0;
            int width = Math.Max((int)(66 * scale), text.Width + (int)(24 * scale)+extras);
            int height = Math.Max((int)(36 * scale), text.Height + (int)(14 * scale));
            button.SetBounds(right - width, inset, width, height);
            right -= width + gap;
        }
        debugLabel.SetBounds(inset, canvas.ClientSize.Height - (int)(29 * scale),
            canvas.ClientSize.Width - 2 * inset, (int)(22 * scale));
        PositionOverlay();
    }

    private static void FitFont(Control control, float points)
    {
        if (Math.Abs(control.Font.Size - points) < .01f) return;
        Font old = control.Font;
        control.Font = new Font(old.FontFamily, points, old.Style);
        old.Dispose();
    }

    private void PositionOverlay()
    {
        float s = ActHudRenderer.UiScale(canvas.ClientSize, DeviceDpi);
        int U(float value) => (int)Math.Round(value * s);
        bool paused=state==ActPlayState.Paused;
        overlay.Size = new Size(Math.Min(U(paused?440:560), canvas.Width - U(32)),
            Math.Min(U(paused?390:338), canvas.Height - U(32)));
        overlay.Location = new Point((canvas.Width - overlay.Width) / 2,
            (canvas.Height - overlay.Height) / 2 + U(12));
        int width = overlay.ClientSize.Width;
        overlayCaption.SetBounds(U(24), U(24), width - U(48), U(23));
        overlayTitle.SetBounds(U(20), U(56), width - U(40), U(53));
        overlayMessage.SetBounds(U(26), U(123), width - U(52), U(55));
        hintLabel.SetBounds(U(20), U(188), width - U(40), U(65));
        bool retryPair = debugMode && (state is ActPlayState.Dead or ActPlayState.Complete);
        int actionWidth = retryPair ? (width - U(66)) / 2 : U(200);
        int actionHeight = Math.Max(U(44), TextRenderer.MeasureText(overlayButton.Text, overlayButton.Font).Height + U(12));
        int actionY = overlay.Height - U(24) - actionHeight;
        overlayButton.SetBounds(retryPair ? U(22) : (width - actionWidth) / 2,
            actionY, actionWidth, actionHeight);
        sameSeedButton.SetBounds(width / 2 + U(11), actionY, actionWidth, actionHeight);
        foreach(var b in new[]{pauseSettings,pauseMap,pauseRestart,pauseHome})b.Visible=paused;
        overlayMessage.Visible=hintLabel.Visible=!paused;
        if(paused)
        {
            overlayTitle.SetBounds(U(28),U(17),width-U(56),U(43));
            overlayCaption.SetBounds(width-U(120),U(25),U(96),U(26));
            overlayTitle.TextAlign=ContentAlignment.MiddleLeft;overlayCaption.Text="PAUSE";
            var actions=new[]{overlayButton,pauseSettings,pauseMap,pauseRestart,pauseHome};
            for(int i=0;i<actions.Length;i++)actions[i].SetBounds(U(28),U(82+i*48),width-U(56),U(40));
        }
        else overlayTitle.TextAlign=ContentAlignment.MiddleCenter;
    }
    private void ShowOverlay(string title, string message, string action)
    {
        overlayTitle.Text = title;
        overlayMessage.Text = message;
        overlayButton.Text = action;

        overlayCaption.Text = state switch
        {
            ActPlayState.Dead => "本次远征结束",
            ActPlayState.Complete => engine.MapIndex < ActChapters.Count ? "前方：" + ActChapters.Name(engine.MapIndex + 1) : "远征完成",
            ActPlayState.Paused => "休息片刻",
            _ => "探索  ·  战斗  ·  前进"
        };
        overlayTitle.ForeColor = ActVisualTheme.Paper;
        overlayMessage.ForeColor = ActHudRenderer.Muted;
        overlayCaption.ForeColor = ActHudRenderer.Gold;
        pauseButton.Visible = false;
        sameSeedButton.Visible = debugMode &&
            (state is ActPlayState.Dead or ActPlayState.Complete);
        PositionOverlay();
        overlay.Visible = true;
        canvas.Dimmed = true;
        overlay.BringToFront();
        overlayButton.Focus();
    }

    private void BeginRun(bool sameSeed = false)
    {
        mainMenu.FinishTransition();
        if (state is ActPlayState.Dead or ActPlayState.Complete)
        {
            if (state == ActPlayState.Complete && engine.MapIndex < ActChapters.Count)
                engine.EnterNextMap(sameSeed ? engine.Seed : null);
            else engine.Reset(sameSeed ? engine.ChapterSeed : null);
            canvas.ResetFx();
        }
        input.Clear(engine);
        state = ActPlayState.Running;
        overlay.Visible = false;
        canvas.Dimmed = false;
        pauseButton.Enabled = true;
        pauseButton.Visible = true;
        accumulator = 0f;
        deathDelay = 0f;
        clock.Restart();
        lastTime = 0d;
        UpdateHud();
        if (engine.RunTime == 0f)
        {
            canvas.UpdateHud(true);
        }
        UpdateDebugLabel();
        canvas.Focus();
    }

    private void StartAdventure()
    {
        if (state != ActPlayState.MainMenu) return;
        input.Clear(engine);
        pauseKeyHeld = false;
        state = ActPlayState.Entering;
        canvas.ResetFx();
        canvas.UpdateHud(true);
        canvas.Dimmed = false;
        overlay.Visible = false;
        pauseButton.Visible = true;
        pauseButton.Enabled = true;
        var scene = new Bitmap(Math.Max(1, canvas.Width), Math.Max(1, canvas.Height));
        canvas.DrawToBitmap(scene, canvas.ClientRectangle);
        mainMenu.StartTransition(scene);
        clock.Restart(); lastTime = 0; accumulator = 0;
    }

    private void Pause()
    {
        if (state != ActPlayState.Running) return;
        state = ActPlayState.Paused;
        input.Clear(engine);
        pauseButton.Enabled = false;
        ShowOverlay("已暂停", "按继续游戏恢复，或打开按键设置。", "继续游戏");
    }

    private void Tick(object? sender, EventArgs e)
    {
        double now = clock.Elapsed.TotalSeconds;
        double elapsed = now - lastTime;
        lastTime = now;
        float wallDt = (float)Math.Clamp(elapsed, 0d, .05d);
        if (state is ActPlayState.MainMenu or ActPlayState.Entering)
        {
            if (mainMenu.Advance(wallDt) && state == ActPlayState.Entering)
            {
                BeginRun();
                if (Form.ActiveForm != this) Pause();
            }
            return;
        }
        if (state == ActPlayState.Dying)
        {
            engine.Update(wallDt);
            canvas.Tick(wallDt);
            deathDelay -= wallDt;
            if (deathDelay <= 0f)
            {
                state = ActPlayState.Dead;
                ShowOverlay("远征未竟",
                    $"本次用时 {TimeSpan.FromSeconds(engine.RunTime):mm\\:ss}  ·  击败 {engine.DefeatedEnemies} 个敌人\n整装再出发，重返这片荒野。",
                    "重新出发");
            }
        }
        if (state == ActPlayState.Running)
        {
            if (elapsed > .25d) { Pause(); return; }
            accumulator += (float)Math.Max(0d, elapsed);
            while (accumulator >= ActTuning.Step && state == ActPlayState.Running)
            {
                engine.Update(ActTuning.Step);
                accumulator -= ActTuning.Step;
                if (engine.IsDead)
                {
                    state = ActPlayState.Dying;
                    pauseButton.Visible = false;
                    input.Clear(engine);
                    deathDelay = .7f;
                }
                else if (engine.PendingPowerReward != ActRewardChoice.None)
                {
                    OpenPowerReward();
                }
                else if (engine.ShopRequested) OpenRestShop();
                else if (engine.FormMenuRequested) OpenFormLibrary();
                else if (engine.IsComplete)
                {
                    if (!engine.IsResting || engine.MapIndex < ActChapters.Count)
                    {
                        BeginSceneTravel();
                        break;
                    }
                    state = ActPlayState.Complete;
                    input.Clear(engine);
                    ShowOverlay(ActChapters.Name(engine.MapIndex) + "已通过",
                        $"用时 {TimeSpan.FromSeconds(engine.RunTime):mm\\:ss} · 击败 {engine.DefeatedEnemies} 个敌人 · 剩余 {engine.Health} HP",
                        engine.MapIndex < ActChapters.Count ? "进入" + ActChapters.Name(engine.MapIndex + 1) : "重新出发");
                }
            }
            canvas.Tick(wallDt);
            UpdateHud();
        }

        canvas.Invalidate();
    }

    private void UpdateHud() => canvas.UpdateHud();

    private void BeginSceneTravel()
    {
        input.Clear(engine);
        state = ActPlayState.Entering;
        var source = new Bitmap(canvas.Width, canvas.Height);
        canvas.DrawToBitmap(source, canvas.ClientRectangle);
        engine.TravelFromExit();
        canvas.ResetFx(); canvas.UpdateHud(true);
        var target = new Bitmap(canvas.Width, canvas.Height);
        canvas.DrawToBitmap(target, canvas.ClientRectangle);
        mainMenu.StartSceneTransition(source, target);
        accumulator = 0; clock.Restart(); lastTime = 0;
    }

    private void OpenRestShop()
    {
        state = ActPlayState.Reward; input.Clear(engine);
        using var dialog = new ActRestShopDialog(engine);
        dialog.ShowDialog(this);
        engine.CloseShop(); input.Clear(engine);
        state = ActPlayState.Running;
        canvas.UpdateHud(); canvas.Focus();
        accumulator = 0; clock.Restart(); lastTime = 0;
    }

    private void OpenPowerReward()
    {
        if (engine.PendingPowerReward == ActRewardChoice.None) return;
        state = ActPlayState.Reward;
        input.Clear(engine);
        pauseKeyHeld = false;
        accumulator = 0f;
        using var backdrop = new Bitmap(canvas.Width, canvas.Height);
        canvas.DrawToBitmap(backdrop, canvas.ClientRectangle);
        using var scene = canvas.RenderRewardScene();
        using var dialog = new ActPowerRewardDialog(engine, backdrop, scene)
        {
            StartPosition = FormStartPosition.Manual,
            Bounds = canvas.RectangleToScreen(canvas.ClientRectangle)
        };
        dialog.ShowDialog(this);
        input.Clear(engine);
        state = ActPlayState.Running;
        canvas.UpdateHud();
        canvas.Focus();
        clock.Restart();
        lastTime = 0d;
    }

    private void OpenFormLibrary()
    {
        if(state is not (ActPlayState.Running or ActPlayState.Paused))return;
        var previous=state;state=ActPlayState.Reward;input.Clear(engine);accumulator=0;
        using(var dialog=new ActFormLibraryDialog(engine))dialog.ShowDialog(this);
        input.Clear(engine);state=previous;canvas.UpdateHud();canvas.Focus();clock.Restart();lastTime=0;
    }

    private void UpdateDebugLabel()
    {
        if (!debugMode) return;
        debugLabel.Text = $"F8 战技  F9 进化  F10 地图选择   地图 {engine.MapIndex}  Seed {engine.Seed}   " +
            string.Join(" · ", engine.Layout.Rooms.Select(room => room.Summary));
        debugTip.SetToolTip(debugLabel, debugLabel.Text);
        Text = $"恐龙行动 · ACT · Seed {engine.Seed}";
    }

    private void OpenMapDebugDialog()
    {
        if (state is ActPlayState.MainMenu or ActPlayState.Entering or ActPlayState.Reward or ActPlayState.Dying) return;
        if (state == ActPlayState.Running) Pause();
        using var dialog = new ActMapDebugDialog(engine.MapIndex, engine.ChapterSeed);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        engine.LoadMapForDebug(dialog.SelectedMap, dialog.SelectedSeed,
            dialog.PreserveRun, dialog.StartAtBoss, roomAdventure:true);
        canvas.ResetFx();
        canvas.UpdateHud(true);
        input.Clear(engine);
        state = ActPlayState.Ready;
        pauseButton.Enabled = false;
        pauseKeyHeld = false;
        BeginRun();
    }

    private void UpdateHints()
    {
        ActBindings b = settings.Bindings;
        ((ActUiButton)pauseButton).KeyHint=SettingsDialog.KeyName((Keys)b.Pause);
        ((ActUiButton)mapDebugButton).KeyHint="F10";
        LayoutInterface();
        hintLabel.Text =
            $"{SettingsDialog.KeyName((Keys)b.Left)} / {SettingsDialog.KeyName((Keys)b.Right)} 移动    " +
            $"{SettingsDialog.KeyName((Keys)b.Jump)} 跳跃    " +
            $"{SettingsDialog.KeyName((Keys)b.Dash)} 冲刺    " +
            $"{SettingsDialog.KeyName((Keys)b.Pause)} 暂停\n" +
            $"{SettingsDialog.KeyName((Keys)b.Attack)} 爪击    " +
            $"{SettingsDialog.KeyName((Keys)b.Tail)} 尾扫    " +
            $"{SettingsDialog.KeyName((Keys)b.Stomp)} 踩踏    " +
            $"{SettingsDialog.KeyName((Keys)b.Ranged)} 骨刺\n" +
            $"{SettingsDialog.KeyName((Keys)b.BattleArt1)} / " +
            $"{SettingsDialog.KeyName((Keys)b.BattleArt2)} 使用战技";
    }

    private void OpenSettings()
    {
        if (state == ActPlayState.Running) Pause();
        using var dialog = new SettingsDialog(settings.Bindings);
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            settings.Bindings = dialog.Bindings;
            canvas.Bindings = settings.Bindings;
            pauseKeyHeld = false;
            UpdateHints();
            if (!SettingsStore.Save(settings))
                MessageBox.Show(this, "按键已生效，但无法保存到设置文件。", "按键设置");
        }
        if (state == ActPlayState.Paused)
            ShowOverlay("已暂停", "按继续游戏恢复，或打开按键设置。", "继续游戏");
    }

    public bool PreFilterMessage(ref Message message)
    {
        if (!Enabled || !ContainsFocus) return false;
        if (!GameKeyboard.TryRead(message, out int key, out bool down)) return false;
        if (state == ActPlayState.MainMenu) return false;
        if (state == ActPlayState.Entering) return true;
        if (key == (int)Keys.F10)
        {
            if (down && ((long)message.LParam & (1L << 30)) == 0)
                OpenMapDebugDialog();
            return true;
        }
        if (state is ActPlayState.Ready or ActPlayState.Dying or ActPlayState.Dead or ActPlayState.Complete)
            return false;
        // A held choice key must not immediately consume the newly obtained art.
        if (down && ((long)message.LParam & (1L << 30)) != 0 &&
            (ActBindings.Actions.Any(action => settings.Bindings.Get(action) == key) ||
                debugMode && (key == (int)Keys.F8 || key == (int)Keys.F9))) return true;
        if (debugMode && state == ActPlayState.Running &&
            (key == (int)Keys.F8 || key == (int)Keys.F9))
        {
            if (down)
            {
                bool offered = key == (int)Keys.F8
                    ? engine.OfferBattleArts() : engine.OfferEvolutions();
                if (offered) OpenPowerReward();
            }
            return true;
        }
        if (key == settings.Bindings.Pause)
        {
            if (!down) { pauseKeyHeld = false; return true; }
            if (pauseKeyHeld) return true;
            pauseKeyHeld = true;
            if (state == ActPlayState.Running) Pause();
            else BeginRun();
            return true;
        }
        if (state == ActPlayState.Paused)
            return false;
        bool handled = input.Handle(engine, settings.Bindings, key, down);
        return handled;
    }
}
