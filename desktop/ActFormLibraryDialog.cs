namespace DinoDash.Desktop;

internal sealed class ActFormLibraryDialog:ActUiDialog
{
    private readonly ActEngine engine;
    private readonly LibrarySurface surface;
    private readonly Button equip=new ActUiButton(),close=new ActUiButton();
    private int selected;
    public ActFormLibraryDialog(ActEngine engine)
    {
        this.engine=engine;selected=(int)engine.Run.CurrentForm;
        var work=Screen.FromPoint(Cursor.Position).WorkingArea;
        ClientSize=new Size(Math.Min(920,work.Width-60),Math.Min(660,work.Height-80));
        Text="原始形态";StartPosition=FormStartPosition.CenterParent;ShowInTaskbar=false;
        FormBorderStyle=FormBorderStyle.None;
        AutoScaleMode=AutoScaleMode.None;ImeMode=ImeMode.Disable;KeyPreview=true;
        surface=new LibrarySurface(this){Dock=DockStyle.Fill};Controls.Add(surface);
        equip.Text="使用此形态";equip.BackColor=ActUiTheme.Gold;
        close.Text="返回游戏";close.DialogResult=DialogResult.Cancel;
        surface.Controls.AddRange([equip,close]);CancelButton=close;
        equip.Click+=(_,_)=>{if(engine.SwitchForm((PrimalForm)selected)){DialogResult=DialogResult.OK;Close();}};
        surface.Resize+=(_,_)=>LayoutActions();LayoutActions();RefreshChoice();
    }
    private void LayoutActions()
    {
        float s=Math.Min(ClientSize.Width/920f,ClientSize.Height/660f);
        equip.SetBounds((int)(ClientSize.Width-204*s),(int)(ClientSize.Height-65*s),(int)(170*s),(int)(39*s));
        close.SetBounds((int)(34*s),(int)(ClientSize.Height-65*s),(int)(150*s),(int)(39*s));
    }
    private void RefreshChoice()
    {
        var id=(PrimalForm)selected;bool owned=engine.Run.Forms.Contains(id);
        equip.Enabled=owned&&engine.Run.CurrentForm!=id&&engine.CanSwitchForm;
        equip.Text=!owned?engine.Run.Forms.Count>=ActPrimalCatalog.FormCapacity?"形态库已满":"尚未获得":engine.Run.CurrentForm==id?"当前使用中":engine.Run.FormCooldown>0?$"冷却 {engine.Run.FormCooldown:0.0} 秒":!engine.CanSwitchForm?"动作结束后切换":"使用此形态";
        surface.Invalidate();
    }
    protected override bool ProcessCmdKey(ref Message msg,Keys keyData)
    {
        if(keyData is Keys.Left or Keys.Right){selected=(selected+(keyData==Keys.Left?4:1))%5;RefreshChoice();return true;}
        return base.ProcessCmdKey(ref msg,keyData);
    }
    private sealed class LibrarySurface:Control
    {
        private readonly ActFormLibraryDialog owner;
        public LibrarySurface(ActFormLibraryDialog owner){this.owner=owner;DoubleBuffered=true;ResizeRedraw=true;}
        private float UiScale=>Math.Min(Width/920f,Height/660f);
        private RectangleF Card(int i)=>new(34+i*172,101,158,145);
        protected override void OnPaint(PaintEventArgs e)
        {
            var g=e.Graphics;ActUiTheme.Landscape(g,ClientRectangle,true);
            var save=g.Save();g.ScaleTransform(UiScale,UiScale);
            ActUiTheme.Frame(g,new RectangleF(5,5,910,650),alpha:225);
            ActUiTheme.Text(g,"原始形态",new(34,23,650,44),28,bold:true);
            ActUiTheme.Text(g,$"携带 {owner.engine.Run.Forms.Count} / {ActPrimalCatalog.FormCapacity}",new(736,30,150,30),17,ActUiTheme.Muted,center:true);
            for(int i=0;i<5;i++)
            {
                var d=ActPrimalCatalog.Forms[i];bool owned=owner.engine.Run.Forms.Contains(d.Id);var r=Card(i);
                ActUiTheme.Frame(g,r,owner.selected==i);
                ActUiTheme.FormIcon(g,new(r.X+33,r.Y+16,92,73),d.Id,owned?ActUiTheme.Paper:ActUiTheme.Muted);
                ActUiTheme.Text(g,d.Name.Replace("形态",""),new(r.X+5,r.Y+98,r.Width-10,26),18,owned?ActUiTheme.Paper:ActUiTheme.Muted,true,true);
                ActUiTheme.Text(g,owner.engine.Run.CurrentForm==d.Id?"使用中":owned?"已获得":"尚未获得",new(r.X+5,r.Bottom-22,r.Width-10,18),11,ActUiTheme.Muted,center:true);
            }
            var selected=ActPrimalCatalog.Forms[owner.selected];
            ActUiTheme.Frame(g,new(34,268,852,113));
            ActUiTheme.Text(g,selected.Name,new(55,280,800,30),19,bold:true);
            ActUiTheme.Text(g,selected.Description,new(55,318,796,48),16);
            ActUiTheme.Text(g,"异变记录",new(34,398,820,30),19,bold:true);
            var ids=owner.engine.Run.Mutations.Select(id=>(id,"本局"))
                .Concat(owner.engine.Run.PendingMapMutations.Select(id=>(id,"等待下一张地图")))
                .Concat(owner.engine.Run.ActiveMapMutations.Select(id=>(id,"生效中 · 下一处休息处移除"))).ToArray();
            if(ids.Length==0)ActUiTheme.Text(g,"尚未接受异变",new(34,437,810,30),15,ActUiTheme.Muted);
            for(int i=0;i<ids.Length;i++)ActUiTheme.Text(g,$"{ActPrimalCatalog.Mutation(ids[i].id).Name} · {ids[i].Item2}",new(34+i%2*428,435+i/2*28,414,27),13,ActUiTheme.Muted);
            ActUiTheme.Text(g,"← / → 查看形态  ·  切换冷却在返回游戏后继续计时",new(34,559,840,25),12,ActUiTheme.Muted);
            ActUiTheme.Footer(g,new(8,566,903,83));g.Restore(save);
        }
        protected override void OnMouseClick(MouseEventArgs e)
        {base.OnMouseClick(e);if(e.Button!=MouseButtons.Left)return;var p=new PointF(e.X/UiScale,e.Y/UiScale);for(int i=0;i<5;i++)if(Card(i).Contains(p)){owner.selected=i;owner.RefreshChoice();break;}}
    }
}

