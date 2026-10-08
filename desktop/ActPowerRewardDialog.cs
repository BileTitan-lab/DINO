using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace DinoDash.Desktop;

// Composites translucent UI over a frozen GameCanvas render, never a desktop capture.
internal sealed class ActPowerRewardDialog : Form
{
    private readonly ActEngine engine;
    private readonly RewardSurface surface;
    private readonly System.Windows.Forms.Timer animation = new() { Interval = 16 };
    private readonly Stopwatch clock = new();
    private double previousTime;
    private bool closing, completed;
    private int selected = -1, focused;
    private readonly bool isArt, full;
    private readonly ActRewardChoice kind;
    private readonly int ownedForms;
    private string Heading => kind switch { ActRewardChoice.Mutation=>"异变龙蛋 · 风险与馈赠",ActRewardChoice.Form=>"唤醒原始形态",ActRewardChoice.BattleArt=>"唤醒原始之力",_=>"聆听生命回响" };
    private string Subtitle => kind switch { ActRewardChoice.Mutation=>"选择一项异变，同时接受代价与收益",ActRewardChoice.Form=>$"选择一个形态，加入本局形态库 · 已携带 {ownedForms} / {ActPrimalCatalog.FormCapacity}",ActRewardChoice.BattleArt=>"战技 / 选择一项，留待关键时刻",_=>"进化 / 选择一项，本局永久生效" };
    private RewardEmblem Emblem => kind==ActRewardChoice.Form?RewardEmblem.Hunter:kind==ActRewardChoice.Mutation?RewardEmblem.Tissue:isArt?RewardEmblem.BattleArt:RewardEmblem.Evolution;
    private readonly RewardOption[] options;
    private bool HasRare=>options.Any(o=>o.Rarity==EvolutionRarity.Rare);
    private float particleTime;
    internal float Reveal => surface.Reveal;
    internal bool IsFadingOut => closing;

    public ActPowerRewardDialog(ActEngine engine, Bitmap? backdrop = null, Bitmap? scene = null)
    {
        this.engine = engine;
        ownedForms=engine.Run.Forms.Count;
        kind=engine.PendingPowerReward;
        isArt = engine.PendingPowerReward == ActRewardChoice.BattleArt;
        if (kind==ActRewardChoice.None)
            throw new ArgumentException("No ACT reward is pending.", nameof(engine));
        full = isArt && engine.Run.BattleArtCount == ActPowerCatalog.BattleArtCapacity;
        options = isArt
            ? engine.ArtOffers.Select(id => { var d = ActPowerCatalog.Art(id);
                return new RewardOption(d.Name, d.Description, ActRewardIcons.For(id)); }).ToArray()
            : engine.EvolutionOffers.Select(id => { var d = ActPowerCatalog.Evolution(id);
                return new RewardOption(d.Name, d.Description, ActRewardIcons.For(id),Rarity:d.Rarity); }).ToArray();
        if(kind==ActRewardChoice.Form)options=engine.FormOffers.Select(id=>
            {var d=ActPrimalCatalog.Form(id);return new RewardOption(d.Name,d.Description,ActFormVisual.Emblem(id),Form:id);}).ToArray();
        if(kind==ActRewardChoice.Mutation)options=engine.MutationOffers.Select(id=>
        {
            var d=ActPrimalCatalog.Mutation(id);
            var emblem=id switch
            {
                MutationId.BloodGene=>RewardEmblem.Spore,MutationId.HollowFrame=>RewardEmblem.Wings,
                MutationId.Brutality=>RewardEmblem.Attack,MutationId.SpikeGrowth=>RewardEmblem.TwinSpikes,
                MutationId.BloodTissue=>RewardEmblem.Tissue,MutationId.BrittleShell=>RewardEmblem.Scales,
                _=>RewardEmblem.Evolution
            };
            return new RewardOption(d.Name,$"代价：{d.Cost}\n收益：{d.Benefit}",emblem,Cost:d.Cost,Benefit:d.Benefit);
        }).ToArray();
        Text = Heading;
        AutoScaleMode = AutoScaleMode.None;
        ClientSize = new Size(1280, 720);
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false; KeyPreview = true; ImeMode = ImeMode.Disable;
        surface = new RewardSurface(this, backdrop, scene) { Dock = DockStyle.Fill };
        Controls.Add(surface);
        animation.Tick += (_, _) =>
        {
            double now = clock.Elapsed.TotalSeconds;
            AdvanceTransition((float)Math.Min(.06, now - previousTime));
            previousTime = now;
        };
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        surface.Reveal = 0f;
        clock.Restart(); previousTime = 0;
        animation.Start(); surface.Focus();
    }

    internal void AdvanceTransition(float dt)
    {
        particleTime+=dt;
        surface.Reveal = Math.Clamp(surface.Reveal + dt * (closing ? -1f / .24f : 1f / (HasRare?.65f:.32f)), 0, 1);
        surface.Invalidate();
        if (!closing && surface.Reveal >= 1 && !HasRare) animation.Stop();
        if (!closing || surface.Reveal > 0) return;
        completed = true; animation.Stop(); DialogResult = DialogResult.OK; Close();
    }

    private void Choose(int index)
    {
        if (closing || surface.Reveal < .5f || index < 0 || index >= options.Length) return;
        if(kind==ActRewardChoice.Form){if(engine.ChooseForm(index))Finish();return;}
        if(kind==ActRewardChoice.Mutation){if(engine.ChooseMutation(index))Finish();return;}
        if (!isArt) { if (engine.ChooseEvolution(index)) { if(options[index].Rarity==EvolutionRarity.Rare)ActRareFeedback.Play(); Finish(); } return; }
        if (!full) { if (engine.ChooseBattleArt(index)) Finish(); return; }
        selected = focused = index; surface.Invalidate();
    }

    private void Replace(int slot)
    { if (!closing && selected >= 0 && engine.ChooseBattleArt(selected, slot)) Finish(); }
    private void Decline()
    { if (!closing && isArt && engine.DeclineBattleArt()) Finish(); }
    private void Finish()
    {
        closing = true; clock.Restart(); previousTime = 0;
        animation.Start(); surface.Invalidate();
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (closing || surface.Reveal < .5f) return true;
        if ((msg.Msg is 0x100 or 0x104) && ((long)msg.LParam & (1L << 30)) != 0) return true;
        if ((keyData & Keys.Modifiers) != Keys.None) return base.ProcessCmdKey(ref msg, keyData);
        int index = keyData switch { Keys.D1 or Keys.NumPad1 => 0,
            Keys.D2 or Keys.NumPad2 => 1, Keys.D3 or Keys.NumPad3 => 2, _ => -1 };
        if (index >= 0)
        {
            if (full && selected >= 0 && index < 2) Replace(index);
            else Choose(index);
            return true;
        }
        switch (keyData)
        {
            case Keys.Left: focused = (focused + options.Length - 1) % options.Length; break;
            case Keys.Right: case Keys.Tab: focused = (focused + 1) % options.Length; break;
            case Keys.Enter: Choose(focused); break;
            case Keys.Escape: if (isArt) Decline(); break;
            default: return base.ProcessCmdKey(ref msg, keyData);
        }
        surface.Invalidate(); return true;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!completed && e.CloseReason == CloseReason.UserClosing) e.Cancel = true;
        base.OnFormClosing(e);
    }
    protected override void Dispose(bool disposing)
    { if (disposing) animation.Dispose(); base.Dispose(disposing); }

    // Selection clears the engine's offer lists immediately. Keep all paint
    // data here so the outgoing animation can still render the same cards.
    private sealed record RewardOption(string Name, string Description, RewardEmblem Emblem,
        PrimalForm? Form=null,string? Cost=null,string? Benefit=null,EvolutionRarity Rarity=EvolutionRarity.Normal);
    private sealed class RewardSurface : Control
    {
        private readonly ActPowerRewardDialog owner;
        private readonly Bitmap? backdrop;
        private readonly Bitmap? scene;
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public float Reveal { get; set; }
        private float UiScale => Math.Min(Width / 960f, Height / 600f);
        private PointF Offset => new((Width - 960 * UiScale) / 2, (Height - 600 * UiScale) / 2);
        private RectangleF Card(int i) { float width=(832-16*(owner.options.Length-1))/owner.options.Length; return new(64+i*(width+16),149,width,313); }
        private static RectangleF Action(int i) => new(64 + 282 * i, 501, 268, 39);

        public RewardSurface(ActPowerRewardDialog owner, Bitmap? backdrop, Bitmap? scene)
        {
            this.owner = owner; this.backdrop = backdrop; this.scene = scene;
            DoubleBuffered = true; ResizeRedraw = true; ImeMode = ImeMode.Disable;
            SetStyle(ControlStyles.Selectable, true); TabStop = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            if (backdrop is not null) g.DrawImage(backdrop, ClientRectangle);
            else g.Clear(Color.FromArgb(77, 84, 85));
            if (Width <= 0 || Height <= 0) return;
            float fade = Reveal * Reveal * (3 - 2 * Reveal);
            if (scene is not null)
            {
                using var blend = new ImageAttributes();
                blend.SetColorMatrix(new ColorMatrix { Matrix33 = fade });
                g.DrawImage(scene, ClientRectangle, 0, 0, scene.Width, scene.Height, GraphicsUnit.Pixel, blend);
            }
            using (var veil = new SolidBrush(Color.FromArgb((int)(95 * fade), 9, 15, 19)))
                g.FillRectangle(veil, ClientRectangle);
            using var layer = new Bitmap(Width, Height);
            using (Graphics ui = Graphics.FromImage(layer))
            {
                ui.SmoothingMode = SmoothingMode.AntiAlias;
                ui.TranslateTransform(Offset.X, Offset.Y + 18f * (1 - fade));
                ui.ScaleTransform(UiScale, UiScale); DrawInterface(ui);
            }
            using var attributes = new ImageAttributes();
            attributes.SetColorMatrix(new ColorMatrix { Matrix33 = fade });
            g.DrawImage(layer, ClientRectangle, 0, 0, Width, Height, GraphicsUnit.Pixel, attributes);
        }

        private void DrawInterface(Graphics g)
        {
            Color accent=ActUiTheme.Gold;
            ActUiTheme.Frame(g,new RectangleF(30,38,900,524),alpha:217);
            ActUiTheme.Footer(g,new RectangleF(34,478,892,79),owner.kind==ActRewardChoice.Mutation?2:0);
            string heading=owner.kind switch {ActRewardChoice.Mutation=>"异变龙蛋",ActRewardChoice.Form=>"获得新形态",ActRewardChoice.BattleArt=>"选择战技",_=>owner.engine.PendingEvolutionSource==EvolutionSource.BossReward?"Boss 进化奖励":"选择进化特质"};
            Label(g,heading,28,ActUiTheme.Paper,new RectangleF(64,57,620,44),true);
            Label(g,$"{owner.focused+1} / {owner.options.Length}",17,ActUiTheme.Paper,new RectangleF(808,65,85,28),center:true);
            Label(g,owner.Subtitle,13,ActUiTheme.Muted,new RectangleF(65,108,780,24));
            using var rule=new Pen(Color.FromArgb(100,ActUiTheme.Edge));g.DrawLine(rule,64,137,896,137);
            for(int i=0;i<owner.options.Length;i++)
            {
                var r=Card(i);var option=owner.options[i];bool active=owner.selected==i||(owner.selected<0&&owner.focused==i);
                ActUiTheme.Frame(g,r,active,220);
                bool rare=option.Rarity==EvolutionRarity.Rare;
                if(rare)
                {
                    using var wash=new SolidBrush(Color.FromArgb(45,ActUiTheme.Gold));g.FillRectangle(wash,r);
                    using var edge=new Pen(ActUiTheme.Gold,2);g.DrawRectangle(edge,r.X+2,r.Y+2,r.Width-4,r.Height-4);
                    using var inner=new Pen(Color.FromArgb(115,ActUiTheme.Gold),1);g.DrawRectangle(inner,r.X+6,r.Y+6,r.Width-12,r.Height-12);
                    Label(g,"稀有进化",12,ActUiTheme.Gold,new RectangleF(r.Right-94,r.Y+10,80,20),true,true);
                    for(int p=0;p<8;p++)
                    {
                        float phase=(owner.particleTime*.35f+p/8f)%1;
                        float px=r.X+16+(p*53%(int)Math.Max(1,r.Width-32));
                        using var dust=new SolidBrush(Color.FromArgb((int)(90+70*MathF.Sin(phase*MathF.PI)),ActUiTheme.Gold));
                        g.FillRectangle(dust,px,r.Bottom-18-phase*110,2,3);
                    }
                }
                Label(g,$"{i+1}",12,ActUiTheme.Muted,new RectangleF(r.X+15,r.Y+11,32,20));
                if(option.Form is PrimalForm form)
                    ActUiTheme.FormIcon(g,new RectangleF(r.X+r.Width/2-51,r.Y+24,102,78),form);
                else
                {
                    float unfold=rare?Math.Clamp(Reveal*1.8f-.2f,.1f,1):1;
                    float side=72*unfold;
                    ActUiTheme.Icon(g,new RectangleF(r.X+r.Width/2-side/2,r.Y+61-side/2,side,side),option.Emblem,rare?ActUiTheme.Gold:null);
                }
                Label(g,option.Name,21,rare?ActUiTheme.Gold:ActUiTheme.Paper,new RectangleF(r.X+16,r.Y+113,r.Width-32,33),true,true);
                if(owner.kind==ActRewardChoice.Mutation)
                {
                    Label(g,"代价 · "+option.Cost,14,ActUiTheme.Bad,new RectangleF(r.X+18,r.Y+153,r.Width-36,66));
                    Label(g,"收益 · "+option.Benefit,14,ActUiTheme.Good,new RectangleF(r.X+18,r.Y+224,r.Width-36,82));
                }
                else Label(g,option.Description,16,ActUiTheme.Paper,new RectangleF(r.X+22,r.Y+158,r.Width-44,122),center:true);
            }
            string hint=owner.full?owner.selected>=0?"选择替换槽位 · 1 / 2 替换，Esc 放弃":"战技槽已满 · 选择新战技后决定替换槽位":"1 / 2 / 3 选择 · 方向键浏览 · Enter 确认";
            Label(g,hint,12,ActUiTheme.Muted,new RectangleF(64,469,832,23),center:true);
            if(owner.isArt)
            {
                if(owner.full)for(int i=0;i<2;i++)
                {
                    string name=owner.engine.Run.BattleArts[i] is BattleArtId id?ActPowerCatalog.Art(id).Name:"空槽";
                    Button(g,Action(i),$"替换 {i+1} · {name}",accent,owner.selected>=0);
                }
                Button(g,Action(2),"放弃本次战技",accent,true);
            }
            else Label(g,owner.kind==ActRewardChoice.Mutation?"单地图效果：下一张地图生效，进入再下一处休息处移除":owner.kind==ActRewardChoice.Form?$"最多携带 {ActPrimalCatalog.FormCapacity} 个形态（含基础形态） · 库满后形态奖励转为进化奖励":"本局永久生效 · 同一种进化特质不可重复获得",12,ActUiTheme.Muted,new RectangleF(64,505,832,30),center:true);
        }
        private static GraphicsPath Chamfer(RectangleF r, float cut)
        {
            var p = new GraphicsPath();
            p.AddPolygon(new PointF[] {new(r.Left + cut, r.Top), new(r.Right, r.Top), new(r.Right, r.Bottom - cut),
                new(r.Right - cut, r.Bottom), new(r.Left, r.Bottom), new(r.Left, r.Top + cut)});
            return p;
        }
        private static void Label(Graphics g, string text, float size, Color color, RectangleF r,
            bool bold = false, bool center = false)
        {
            using var font = new Font("Microsoft YaHei UI", size, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);
            using var brush = new SolidBrush(color);
            using var format = new StringFormat { Alignment = center ? StringAlignment.Center : StringAlignment.Near,
                LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter };
            g.DrawString(text, font, brush, r, format);
        }
        private static void Button(Graphics g, RectangleF r, string text, Color accent, bool enabled)
        {
            using var fill = new SolidBrush(Color.FromArgb(enabled ? 100 : 45, 18, 24, 29));
            using var pen = new Pen(Color.FromArgb(enabled ? 155 : 55, accent));
            using var path = Chamfer(r, 6);
            g.FillPath(fill, path); g.DrawPath(pen, path);
            Label(g, text, 14, enabled ? ActRewardIcons.Bone : Color.FromArgb(117, 130, 132), r, center: true);
        }
        private PointF Logical(Point p) => new((p.X - Offset.X) / UiScale, (p.Y - Offset.Y) / UiScale);
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            PointF p = Logical(e.Location); bool hit = false;
            for (int i = 0; i < owner.options.Length; i++)
                if (Card(i).Contains(p)) { owner.focused = i; hit = true; }
            if (owner.isArt && (Action(2).Contains(p) || owner.full && owner.selected >= 0 &&
                (Action(0).Contains(p) || Action(1).Contains(p)))) hit = true;
            Cursor = hit ? Cursors.Hand : Cursors.Default; Invalidate();
        }
        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button != MouseButtons.Left || owner.closing || Reveal < .5f) return;
            PointF p = Logical(e.Location);
            for (int i = 0; i < owner.options.Length; i++)
                if (Card(i).Contains(p)) { owner.Choose(i); return; }
            if (!owner.isArt) return;
            if (Action(2).Contains(p)) owner.Decline();
            else if (owner.full)
                for (int i = 0; i < 2; i++) if (Action(i).Contains(p)) owner.Replace(i);
        }
    }
}

