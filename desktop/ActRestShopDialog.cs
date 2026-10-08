namespace DinoDash.Desktop;

internal sealed class ActRestShopDialog : ActUiDialog
{
    private readonly ActEngine game;
    private readonly Label balance = new(), status = new();
    private readonly List<Button> buy = [];
    private readonly FlowLayoutPanel replace = new();
    public ActRestShopDialog(ActEngine engine)
    {
        game=engine; Text="游商 · 骨片交易";
        AutoScaleMode=AutoScaleMode.None;
        ClientSize=new Size(820,510); FormBorderStyle=FormBorderStyle.None;
        MaximizeBox=MinimizeBox=false; ShowInTaskbar=false; StartPosition=FormStartPosition.CenterParent;
        BackColor=ActHudRenderer.Panel; ForeColor=ActVisualTheme.Paper;
        Font=new Font("Microsoft YaHei UI",14,FontStyle.Regular,GraphicsUnit.Pixel); ImeMode=ImeMode.Disable;
        var title=new Label {Text="商人",BackColor=Color.Transparent,Font=new Font(Font.FontFamily,28,FontStyle.Bold,GraphicsUnit.Pixel),Bounds=new Rectangle(27,20,420,45),ForeColor=ActUiTheme.Paper};
        balance.SetBounds(570,35,225,28); Controls.AddRange([title,balance]);
        for(int i=0;i<game.ShopStock.Count;i++)
        {
            int index=i; var item=game.ShopStock[i];
            var card=new ActUiPanel{Bounds=new Rectangle(27+i*260,87,245,258),ForeColor=ActUiTheme.Paper};
            var emblem=item.Art is BattleArtId art?ActRewardIcons.For(art):item.Evolution is EvolutionId evo?ActRewardIcons.For(evo):item.Name.Contains("骨刺")?RewardEmblem.Energy:RewardEmblem.Spore;
            card.Paint+=(_,e)=>ActUiTheme.Icon(e.Graphics,new RectangleF(94,12,56,56),emblem);
            var name=new Label{Text=item.Name,BackColor=Color.Transparent,TextAlign=ContentAlignment.MiddleCenter,Bounds=new Rectangle(15,72,215,30),ForeColor=ActUiTheme.Paper,Font=new Font(Font.FontFamily,18,FontStyle.Bold,GraphicsUnit.Pixel)};
            var description=new Label{Text=item.Description,BackColor=Color.Transparent,TextAlign=ContentAlignment.MiddleCenter,Bounds=new Rectangle(15,109,215,52),ForeColor=ActUiTheme.Paper};
            var price=new Label{Text=$"{item.Price} 骨片",TextAlign=ContentAlignment.MiddleCenter,BackColor=Color.Transparent,Bounds=new Rectangle(15,162,215,30),ForeColor=ActHudRenderer.Gold};
            var button=MakeButton("购买",new Rectangle(15,206,215,36));
            button.Click+=(_,_)=>Choose(index);
            buy.Add(button);card.Controls.AddRange([name,description,price,button]);Controls.Add(card);
        }
        status.SetBounds(28,354,766,28);status.ForeColor=ActHudRenderer.Gold;Controls.Add(status);
        replace.SetBounds(27,389,570,80); replace.WrapContents=false;replace.BackColor=Color.Transparent;Controls.Add(replace);
        Paint+=(_,e)=>ActUiTheme.Footer(e.Graphics,new RectangleF(3,Height-85,Width-6,82),1);
        var close=MakeButton("离开摊位",new Rectangle(632,450,160,38));
        close.Click+=(_,_)=>Close();Controls.Add(close);CancelButton=close;
        RefreshStock();
    }
    private Button MakeButton(string text,Rectangle bounds)=>new ActUiButton(){Text=text,Bounds=bounds,
        FlatStyle=FlatStyle.Flat,BackColor=Color.FromArgb(62,66,51),ForeColor=ActVisualTheme.Paper};
    private void RefreshStock()
    {
        balance.Text=$"持有骨片：{game.Run.BoneShards}";
        for(int i=0;i<buy.Count;i++)
        {
            string problem=game.PurchaseProblem(i);
            buy[i].Text=problem.Length==0?"购买":problem;
            buy[i].ForeColor=problem.Length==0?ActVisualTheme.Paper:ActHudRenderer.Gold;
        }
    }
    private void Choose(int index)
    {
        string problem=game.PurchaseProblem(index);
        if(problem.Length>0){status.Text=problem;return;}
        var item=game.ShopStock[index];
        if(item.Art!=null && game.Run.BattleArtCount==ActPowerCatalog.BattleArtCapacity)
        {
            status.Text="战技已满：选择替换槽位，或取消。确认替换后才扣除骨片。";
            ClearReplacement();
            for(int slot=0;slot<2;slot++)
            {
                int selected=slot;
                var button=MakeButton($"替换 {slot+1}：{ActPowerCatalog.Art(game.Run.BattleArts[slot]!.Value).Name}",new Rectangle(0,0,210,45));
                button.Click+=(_,_)=>Buy(index,selected);replace.Controls.Add(button);
            }
            var cancel=MakeButton("取消",new Rectangle(0,0,90,45));cancel.Click+=(_,_)=>{ClearReplacement();status.Text="已取消，未扣除骨片。";};replace.Controls.Add(cancel);
        }
        else Buy(index,null);
    }
    private void ClearReplacement()
    {
        foreach(Control child in replace.Controls.Cast<Control>().ToArray())child.Dispose();
        replace.Controls.Clear();
    }
    private void Buy(int index,int? slot)
    {
        status.Text=game.Purchase(index,slot)?"交易完成，祝你一路平安。":game.PurchaseProblem(index);
        ClearReplacement();RefreshStock();
    }
}
