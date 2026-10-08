namespace DinoDash.Desktop;

internal sealed class SettingsDialog : ActUiDialog, IMessageFilter
{
    private readonly Dictionary<ActAction, Button> buttons = [];
    private readonly Label status = new();
    private ActAction? waiting;

    public SettingsDialog(ActBindings bindings)
    {
        Bindings = bindings.Clone();
        Text = "按键设置";
        AutoScaleMode = AutoScaleMode.None;
        int rows=(ActBindings.Actions.Length+1)/2;
        float panelHeight = 252 + rows * 46;
        var work=Screen.FromPoint(Cursor.Position).WorkingArea;
        float scale = Math.Min(1.2f,Math.Min((work.Width-60f)/860f,(work.Height-80f)/panelHeight));
        int U(float value) => (int)Math.Round(value * scale);
        ClientSize = new Size(U(860), U(panelHeight));
        Font = new Font("Microsoft YaHei UI", 13f * scale, FontStyle.Regular, GraphicsUnit.Pixel);
        FormBorderStyle = FormBorderStyle.None;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = ActHudRenderer.Panel;
        ForeColor = ActVisualTheme.Paper;
        KeyPreview = true;
        ImeMode = ImeMode.Disable;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, Padding = new Padding(U(24)), ColumnCount = 2,
            BackColor = Color.Transparent, ForeColor = ActUiTheme.Paper, Font = Font,
            RowCount = rows + 4
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, U(42)));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, U(32)));
        for (int i = 0; i < rows; i++)
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, U(46)));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, U(52)));
        Controls.Add(root);
        root.Controls.Add(new Label
        {
            Text = "按键设置", Dock = DockStyle.Fill,
            Font = new Font("Microsoft YaHei UI", 25f * scale, FontStyle.Bold, GraphicsUnit.Pixel),
            ForeColor = ActUiTheme.Paper
        });
        root.SetColumnSpan(root.Controls[0],2);
        root.Controls.Add(new Label
        {
            Text = "点击动作，再按新的键；每项只能使用一个独立按键。",
            Dock = DockStyle.Fill, ForeColor = ActHudRenderer.Muted
        });
        root.SetColumnSpan(root.Controls[1],2);
        foreach (ActAction action in ActBindings.Actions)
        {
            var button = new ActUiButton
            {
                Dock = DockStyle.Fill, BackColor = Color.FromArgb(38, 41, 47),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft YaHei UI", 13f * scale, FontStyle.Bold, GraphicsUnit.Pixel),
                Padding = new Padding(U(12), 0, U(12), 0),
                Margin = new Padding(U(4), U(3), U(4), U(3)),
                TextAlign = ContentAlignment.MiddleLeft
            };
            button.FlatAppearance.BorderColor = Color.FromArgb(86, 85, 82);
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(64, 60, 51);
            button.Click += (_, _) =>
            {
                waiting = action;
                RefreshLabels();
                status.Text = $"设置“{ActionName(action)}”：请按新按键。";
            };
            buttons.Add(action, button);
            root.Controls.Add(button);
        }
        status.Dock = DockStyle.Fill;
        status.Text = "可绑定 Shift 和 Esc；组合键暂不支持。";
        status.TextAlign = ContentAlignment.MiddleLeft;
        status.ForeColor = ActHudRenderer.Gold;
        root.Controls.Add(status);
        root.SetCellPosition(status,new TableLayoutPanelCellPosition(0,rows+2));
        root.SetColumnSpan(status,2);
        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };
        Button save = new ActUiButton() { Text = "保存" };
        Button cancel = new ActUiButton() { Text = "取消" };
        Button defaults = new ActUiButton() { Text = "恢复默认" };
        foreach (Button button in new[] { save, cancel, defaults })
        {
            button.Size = new Size(U(100), U(38));
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = Color.FromArgb(86, 85, 82);
            button.BackColor = ActHudRenderer.Panel;
            button.ForeColor = ActVisualTheme.Paper;
        }
        save.BackColor = ActHudRenderer.Gold;
        save.ForeColor = ActHudRenderer.Panel;
        save.Click += (_, _) => { DialogResult = DialogResult.OK; Close(); };
        cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        defaults.Click += (_, _) =>
        {
            Bindings = new ActBindings();
            waiting = null;
            status.Text = "已恢复默认按键。";
            RefreshLabels();
        };
        actions.Controls.Add(save);
        actions.Controls.Add(cancel);
        actions.Controls.Add(defaults);
        root.Controls.Add(actions);
        root.SetCellPosition(actions,new TableLayoutPanelCellPosition(0,rows+3));
        root.SetColumnSpan(actions,2);
        AcceptButton = save;
        CancelButton = cancel;
        RefreshLabels();
        Application.AddMessageFilter(this);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) Application.RemoveMessageFilter(this);
        base.Dispose(disposing);
    }

    public bool PreFilterMessage(ref Message message)
    {
        if (waiting is null || !Enabled || !ContainsFocus ||
            !GameKeyboard.TryRead(message, out int key, out bool down) || !down)
            return false;
        return CaptureKey((Keys)key | ModifierKeys);
    }

    public ActBindings Bindings { get; private set; }

    protected override bool ProcessCmdKey(ref Message message, Keys keyData)
    {
        if (waiting is null) return base.ProcessCmdKey(ref message, keyData);
        return CaptureKey(keyData);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (waiting is not null)
        {
            e.Handled = CaptureKey(e.KeyData);
            e.SuppressKeyPress = true;
            return;
        }
        base.OnKeyDown(e);
    }

    private bool CaptureKey(Keys keyData)
    {
        if (waiting is not ActAction chosen) return false;
        Keys key = keyData & Keys.KeyCode;
        Keys modifiers = keyData & Keys.Modifiers;
        if (!IsBindable(key) ||
            (modifiers != Keys.None && !(key == Keys.ShiftKey && modifiers == Keys.Shift)))
        {
            status.Text = "请选择单独的有效按键。";
            return true;
        }
        if (ActBindings.Actions.Any(action => action != chosen &&
            Bindings.Get(action) == (int)key))
        {
            status.Text = $"{KeyName(key)} 已用于其他动作，请选择不同的按键。";
            return true;
        }
        Bindings.Set(chosen, (int)key);
        waiting = null;
        status.Text = $"“{ActionName(chosen)}”已设置为 {KeyName(key)}。";
        RefreshLabels();
        return true;
    }

    private void RefreshLabels()
    {
        foreach (ActAction action in ActBindings.Actions)
        {
            buttons[action].Text = $"{ActionName(action),-12}   {KeyName((Keys)Bindings.Get(action))}";
            buttons[action].ForeColor = waiting == action ? ActHudRenderer.Gold : ActVisualTheme.Paper;
        }
    }

    internal static bool IsBindable(Keys key) =>
        key is >= Keys.A and <= Keys.Z or >= Keys.D0 and <= Keys.D9 or
            >= Keys.NumPad0 and <= Keys.NumPad9 or
            Keys.Space or Keys.Left or Keys.Right or Keys.Up or Keys.Down or
            Keys.ShiftKey or Keys.Escape or Keys.OemMinus or Keys.Oemplus or
            Keys.Oemcomma or Keys.OemPeriod or Keys.OemQuestion or
            Keys.OemOpenBrackets or Keys.OemCloseBrackets or Keys.OemSemicolon;

    internal static string KeyName(Keys key) => key switch
    {
        >= Keys.D0 and <= Keys.D9 => ((int)key - (int)Keys.D0).ToString(),
        >= Keys.NumPad0 and <= Keys.NumPad9 => "小键盘" + ((int)key - (int)Keys.NumPad0),
        Keys.Space => "空格", Keys.ShiftKey => "Shift", Keys.Escape => "Esc",
        Keys.Left => "←", Keys.Right => "→", Keys.Up => "↑", Keys.Down => "↓",
        Keys.OemMinus => "-", Keys.Oemplus => "+", Keys.Oemcomma => ",",
        Keys.OemPeriod => ".", Keys.OemQuestion => "/", Keys.OemOpenBrackets => "[",
        Keys.OemCloseBrackets => "]", Keys.OemSemicolon => ";",
        _ => key.ToString()
    };

    internal static string ActionName(ActAction action) => action switch
    {
        ActAction.Left => "向左", ActAction.Right => "向右",
        ActAction.Jump => "跳跃", ActAction.Dash => "冲刺",
        ActAction.Attack => "爪击", ActAction.Tail => "尾扫",
        ActAction.Stomp => "空中踩踏", ActAction.Ranged => "骨刺",
        ActAction.BattleArt1 => "战技槽 1", ActAction.BattleArt2 => "战技槽 2", ActAction.Interact => "互动",
        ActAction.PreviousForm => "上个形态", ActAction.NextForm => "下个形态", ActAction.FormLibrary => "形态库",
        _ => "暂停 / 继续"
    };
}
