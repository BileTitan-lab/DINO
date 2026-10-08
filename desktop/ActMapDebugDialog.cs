namespace DinoDash.Desktop;

internal sealed class ActMapDebugDialog : ActUiDialog
{
    private readonly RadioButton tutorial = new();
    private readonly RadioButton canyon = new();
    private readonly RadioButton forest = new();
    private readonly RadioButton volcano = new();
    private readonly TextBox seedBox = new();
    private readonly CheckBox randomSeed = new();
    private readonly CheckBox preserve = new();
    private readonly CheckBox bossStart = new();
    private readonly Label error = new();
    private readonly Button enter = new ActUiButton();

    public int SelectedMap { get; private set; }
    public int? SelectedSeed { get; private set; }
    public bool PreserveRun { get; private set; }
    public bool StartAtBoss { get; private set; }

    public ActMapDebugDialog(int currentMap, int currentSeed)
    {
        Text = "地图调试";
        ClientSize = new Size(540, 519);
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        MinimizeBox = MaximizeBox = false;
        AutoScaleMode = AutoScaleMode.None;
        ImeMode = ImeMode.Disable;
        Font=new Font("Microsoft YaHei UI",14,FontStyle.Regular,GraphicsUnit.Pixel);
        foreach(var check in new[]{randomSeed,preserve,bossStart})check.BackColor=Color.Transparent;
        BackColor = Color.FromArgb(30, 34, 34);
        ForeColor = Color.FromArgb(239, 229, 207);
        Font = new Font("Microsoft YaHei UI", 10f);

        Controls.Add(TextLabel("地图调试", 30, 17, 480, 37, 19f, true));
        Controls.Add(TextLabel($"当前：第 {currentMap} 章  ·  Seed {currentSeed}",
            32, 57, 470, 25, 9f));
        Controls.Add(TextLabel("选择要直接进入的地图", 32, 91, 470, 25, 11f, true));
        AddMapCard(tutorial, 1, "晨雾湿地", "教学房间 · 平缓路线", 121,
            currentMap == 1);
        AddMapCard(canyon, 2, "风蚀峡谷", "峡谷房间 · 普通 / 高难", 121,
            currentMap == 2);
        AddMapCard(forest, 3, "巨木秘境", "多层房间 · 移动 / 坠落平台", 207, currentMap == 3);
        AddMapCard(volcano, 4, "熔火遗迹", "熔岩房间 · 预警 / 崩塌", 207, currentMap == 4);
        RadioButton[] maps = [tutorial, canyon, forest, volcano];
        foreach (var radio in maps)
            radio.CheckedChanged += (_, _) => { if (radio.Checked) foreach (var other in maps) if (other != radio) other.Checked = false; };

        Controls.Add(TextLabel("地图 Seed", 32, 299, 180, 25, 11f, true));
        seedBox.SetBounds(32, 329, 205, 31);
        seedBox.Text = currentSeed.ToString();
        seedBox.ImeMode = ImeMode.Disable;
        seedBox.BackColor = Color.FromArgb(49, 54, 53);
        seedBox.ForeColor = ForeColor;
        seedBox.BorderStyle = BorderStyle.FixedSingle;
        Controls.Add(seedBox);
        randomSeed.Text = "随机生成新布局";
        randomSeed.SetBounds(263, 327, 238, 33);
        randomSeed.ForeColor = ForeColor;
        randomSeed.CheckedChanged += (_, _) => seedBox.Enabled = !randomSeed.Checked;
        Controls.Add(randomSeed);

        preserve.Text = "保留本局 HP、战技和进化特质";
        preserve.SetBounds(32, 376, 440, 28);
        preserve.ForeColor = ForeColor;
        Controls.Add(preserve);
        bossStart.Text = "直接进入本章 Boss 场地";
        bossStart.SetBounds(32, 406, 440, 28);
        bossStart.ForeColor = ForeColor;
        Controls.Add(bossStart);
        error.ForeColor = Color.FromArgb(238, 156, 129);
        error.SetBounds(32, 440, 360, 22);
        Controls.Add(error);

        enter.Text = "进入所选地图";
        StyleButton(enter, 349, 462, 160, 42, true);
        enter.Click += (_, _) =>
        {
            if (!TryApplySelection()) return;
            DialogResult = DialogResult.OK;
            Close();
        };
        var cancel = new ActUiButton { Text = "取消", DialogResult = DialogResult.Cancel };
        StyleButton(cancel, 32, 462, 112, 42, false);
        Controls.AddRange([enter, cancel]);
        AcceptButton = enter;
        CancelButton = cancel;
    }

    internal bool TryApplySelection()
    {
        error.Text = "";
        if (!randomSeed.Checked &&
            (!int.TryParse(seedBox.Text.Trim(), out int value) || value <= 0))
        {
            error.Text = "Seed 必须是正整数。";
            return false;
        }
        SelectedMap = volcano.Checked ? 4 : forest.Checked ? 3 : canyon.Checked ? 2 : 1;
        SelectedSeed = randomSeed.Checked ? null : int.Parse(seedBox.Text.Trim());
        PreserveRun = preserve.Checked;
        StartAtBoss = bossStart.Checked;
        return true;
    }

    private void AddMapCard(RadioButton radio, int number, string name,
        string description, int top, bool selected)
    {
        var panel = new ActUiPanel { Bounds = new Rectangle(number % 2 == 1 ? 32 : 277, top, 232, 77),
            BackColor = Color.Transparent, Cursor = Cursors.Hand };
        radio.Text = $"第 {number} 章  {name}";
        radio.Font = new Font("Microsoft YaHei UI", 14f, FontStyle.Bold,GraphicsUnit.Pixel);
        radio.ForeColor = ActUiTheme.Paper;
        radio.BackColor=Color.Transparent;
        radio.SetBounds(10, 11, 216, 29);
        radio.Checked = selected;
        var hint = TextLabel(description, 12, 42, 215, 28, 8f);
        hint.Cursor = Cursors.Hand;
        panel.Controls.AddRange([radio, hint]);
        panel.Click += (_, _) => radio.Checked = true;
        hint.Click += (_, _) => radio.Checked = true;
        panel.Paint += (_, e) =>
        {
            using var border = new Pen(radio.Checked
                ? Color.FromArgb(188, 162, 111) : Color.FromArgb(83, 91, 87),
                radio.Checked ? 2f : 1f);
            e.Graphics.DrawRectangle(border, 1, 1, panel.Width - 3, panel.Height - 3);
        };
        radio.CheckedChanged += (_, _) => panel.Invalidate();
        Controls.Add(panel);
    }

    private static Label TextLabel(string value, int x, int y, int width,
        int height, float fontSize, bool bold = false) => new()
    {
        Text = value, Bounds = new Rectangle(x, y, width, height),
        Font = new Font("Microsoft YaHei UI", fontSize*1.35f,
            bold ? FontStyle.Bold : FontStyle.Regular,GraphicsUnit.Pixel),
        BackColor=Color.Transparent,
        ForeColor = bold ? ActUiTheme.Paper : ActUiTheme.Muted
    };

    private void StyleButton(Button button, int x, int y, int width, int height,
        bool primary)
    {
        button.SetBounds(x, y, width, height);
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = primary
            ? Color.FromArgb(206, 177, 119) : Color.FromArgb(88, 98, 93);
        button.BackColor = primary
            ? Color.FromArgb(178, 149, 98) : Color.FromArgb(49, 56, 53);
        button.ForeColor = primary ? Color.FromArgb(30, 34, 34) : ForeColor;
        button.Font = new Font("Microsoft YaHei UI", 14f, FontStyle.Bold,GraphicsUnit.Pixel);
        button.Cursor = Cursors.Hand;
    }
}
