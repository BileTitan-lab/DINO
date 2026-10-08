namespace DinoDash.Desktop;

internal sealed class RewardDialog : Form
{
    private readonly GameEngine engine;
    private readonly Label choiceHint = new();
    private readonly Button replaceFirst = new();
    private readonly Button replaceSecond = new();
    private readonly Button decline = new();
    private int selectedCard = -1;

    public RewardDialog(GameEngine engine)
    {
        this.engine = engine;
        bool isCard = engine.PendingReward == RewardKind.Card;
        Text = isCard ? "选择卡牌" : "选择遗物";
        ClientSize = new Size(640, 480);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ControlBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Color.FromArgb(245, 244, 239);
        Font = new Font("Microsoft YaHei UI", 10f);
        KeyPreview = true;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(22, 18, 22, 18),
            RowCount = 5,
            ColumnCount = 1
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));
        for (int i = 0; i < 3; i++) root.RowStyles.Add(new RowStyle(SizeType.Absolute, 94));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(root);

        var heading = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Text = isCard ? "选择 1 张一次性卡牌" : "选择 1 件永久遗物",
            Font = new Font("Microsoft YaHei UI", 18f, FontStyle.Bold)
        };
        root.Controls.Add(heading, 0, 0);

        int offerCount = isCard ? engine.CardOffers.Count : engine.RelicOffers.Count;
        for (int index = 0; index < offerCount; index++)
        {
            int offer = index;
            string name = isCard ? RewardText.Name(engine.CardOffers[index]) :
                RewardText.Name(engine.RelicOffers[index]);
            string description = isCard ? RewardText.Description(engine.CardOffers[index]) :
                RewardText.Description(engine.RelicOffers[index]);
            Button option = MakeButton($"{index + 1}.  {name}\n{description}");
            option.Dock = DockStyle.Fill;
            option.TextAlign = ContentAlignment.MiddleLeft;
            option.Margin = new Padding(0, 4, 0, 4);
            option.Click += (_, _) => Choose(offer);
            root.Controls.Add(option, 0, index + 1);
        }

        var bottom = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        bottom.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        bottom.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        choiceHint.Dock = DockStyle.Fill;
        choiceHint.TextAlign = ContentAlignment.MiddleLeft;
        choiceHint.Font = new Font("Microsoft YaHei UI", 9f);
        bottom.Controls.Add(choiceHint, 0, 0);

        var actions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
        for (int i = 0; i < 3; i++) actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333f));
        ConfigureAction(replaceFirst);
        ConfigureAction(replaceSecond);
        ConfigureAction(decline);
        replaceFirst.Click += (_, _) => Replace(0);
        replaceSecond.Click += (_, _) => Replace(1);
        decline.Text = "放弃新卡牌";
        decline.Click += (_, _) =>
        {
            if (!engine.DeclineCard()) return;
            DialogResult = DialogResult.OK;
            Close();
        };
        actions.Controls.Add(replaceFirst, 0, 0);
        actions.Controls.Add(replaceSecond, 1, 0);
        actions.Controls.Add(decline, 2, 0);
        bottom.Controls.Add(actions, 0, 1);
        root.Controls.Add(bottom, 0, 4);

        if (isCard && engine.Cards.Count == GameEngine.CardCapacity)
        {
            choiceHint.Text = "卡槽已满：选一张新卡，再替换现有卡牌；也可放弃。";
            replaceFirst.Text = $"替换 1：{RewardText.Name(engine.Cards[0])}";
            replaceSecond.Text = $"替换 2：{RewardText.Name(engine.Cards[1])}";
            replaceFirst.Enabled = false;
            replaceSecond.Enabled = false;
        }
        else
        {
            choiceHint.Text = isCard ? "选中后加入空卡槽；游戏暂停期间不会消耗技能时间。" :
                "遗物获得后本局永久生效，且不占卡牌槽。";
            actions.Visible = false;
        }
    }

    private void Choose(int offer)
    {
        if (engine.PendingReward == RewardKind.Relic)
        {
            if (!engine.ChooseRelic(offer)) return;
            DialogResult = DialogResult.OK;
            Close();
            return;
        }
        if (engine.Cards.Count < GameEngine.CardCapacity)
        {
            if (!engine.ChooseCard(offer)) return;
            DialogResult = DialogResult.OK;
            Close();
            return;
        }
        selectedCard = offer;
        choiceHint.Text = $"已选 {RewardText.Name(engine.CardOffers[offer])}；指定要替换的卡槽。";
        replaceFirst.Enabled = true;
        replaceSecond.Enabled = true;
    }

    private void Replace(int slot)
    {
        if (!engine.ChooseCard(selectedCard, slot)) return;
        DialogResult = DialogResult.OK;
        Close();
    }

    protected override bool ProcessCmdKey(ref Message message, Keys keyData)
    {
        Keys key = keyData & Keys.KeyCode;
        int option = key switch
        {
            Keys.D1 or Keys.NumPad1 => 0,
            Keys.D2 or Keys.NumPad2 => 1,
            Keys.D3 or Keys.NumPad3 => 2,
            _ => -1
        };
        int count = engine.PendingReward == RewardKind.Card ? engine.CardOffers.Count : engine.RelicOffers.Count;
        if ((keyData & Keys.Modifiers) == Keys.None && option >= 0 && option < count)
        {
            Choose(option);
            return true;
        }
        return base.ProcessCmdKey(ref message, keyData);
    }

    protected override void OnFormClosing(FormClosingEventArgs eventArgs)
    {
        if (engine.PendingReward != RewardKind.None) eventArgs.Cancel = true;
        base.OnFormClosing(eventArgs);
    }

    private static Button MakeButton(string text) => new()
    {
        Text = text,
        BackColor = Color.White,
        ForeColor = Color.FromArgb(18, 18, 18),
        FlatStyle = FlatStyle.Flat,
        Font = new Font("Microsoft YaHei UI", 10.5f, FontStyle.Bold),
        Padding = new Padding(14, 4, 8, 4),
        Cursor = Cursors.Hand
    };

    private static void ConfigureAction(Button button)
    {
        button.Dock = DockStyle.Fill;
        button.Margin = new Padding(3);
        button.BackColor = Color.White;
        button.FlatStyle = FlatStyle.Flat;
        button.Font = new Font("Microsoft YaHei UI", 8.5f, FontStyle.Bold);
        button.Cursor = Cursors.Hand;
    }
}
