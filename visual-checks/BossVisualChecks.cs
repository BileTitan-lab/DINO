using System.Drawing.Drawing2D;
using DinoDash.Desktop;

internal static class BossVisualChecks
{
    public static void Run()
    {
        using var lineup=new Bitmap(1440,370);
        using var row=Graphics.FromImage(lineup);
        row.Clear(Color.FromArgb(32,38,35));
        using var title=new Font("Microsoft YaHei UI",17,FontStyle.Bold);
        using var label=new Font("Microsoft YaHei UI",10);
        for(int chapter=1;chapter<=4;chapter++)
        {
            var id=(ActBossId)(chapter-1);var def=ActBossCatalog.Definitions[chapter-1];
            float left=(chapter-1)*360;
            using var accent=new SolidBrush(ActBossVisual.Accent(id));
            using var background=new SolidBrush(Color.FromArgb(210,211,193));
            row.FillRectangle(background,left+12,16,336,274);
            var state=row.Save();row.TranslateTransform(left+112,108);row.ScaleTransform(1.5f,1.5f);
            ActBossVisual.DrawBody(row,id,0,0,ActEnemyState.Patrol,.4f,def.Moves[0],def.Health,def.Health,Brushes.Black,Brushes.White);
            row.Restore(state);
            row.DrawString($"第 {chapter} 章 · {def.Name}",title,accent,left+28,298);
            row.DrawString(def.Title,label,Brushes.White,left+30,342);

            using var sheet=new Bitmap(1600,720);using var g=Graphics.FromImage(sheet);
            g.Clear(Color.FromArgb(224,222,205));
            ActEnemyState[] states=[ActEnemyState.Patrol,ActEnemyState.Windup,ActEnemyState.Attack,ActEnemyState.Recover];
            for(int move=0;move<3;move++) for(int frame=0;frame<4;frame++)
            {
                float x=frame*400+115,y=move*240+82;
                float t=frame switch{1=>def.Windup*.85f,2=>.24f,3=>.18f,_=>.25f};
                g.DrawString($"{def.Moves[move]} / {states[frame]}",label,Brushes.Black,frame*400+18,move*240+14);
                ActBossVisual.DrawBody(g,id,x,y,states[frame],t,def.Moves[move],def.Health,def.Health,Brushes.Black,Brushes.White);
                g.DrawLine(Pens.Gray,frame*400+8,y+101,frame*400+392,y+101);
            }
            sheet.Save(Path.Combine(AppContext.BaseDirectory,$"boss-{chapter}-poses.png"));
            // Sample continuous windup/recovery rather than only the contact frame.
            if(chapter>1) foreach(var move in def.Moves)
            foreach(var pose in new[]{ActEnemyState.Patrol,ActEnemyState.Windup,ActEnemyState.Recover})
            for(int sample=0;sample<18;sample++)
            {
                using var frame=new Bitmap(450,190);using var fg=Graphics.FromImage(frame);fg.Clear(Color.White);
                ActBossVisual.DrawBody(fg,id,120,45,pose,sample/18f,move,def.Health,def.Health,Brushes.Black,Brushes.White);
                for(int py=146;py<frame.Height;py++) for(int px=0;px<frame.Width;px++)
                    if(frame.GetPixel(px,py).ToArgb()!=Color.White.ToArgb())
                        throw new InvalidOperationException($"Boss {id}/{move}/{pose}/{sample} crossed floor at {px},{py}");
            }
        }
        lineup.Save(Path.Combine(AppContext.BaseDirectory,"boss-lineup.png"));
        Console.WriteLine("PASS four boss silhouettes, twelve move storyboards and ground-contact animation samples.");
    }
}
