namespace DinoDash.Desktop;

internal static class ActChapterRoomScenery
{
    public static void DrawWetlandProps(Graphics g,ActEngine game)
    {
        using var stem=new Pen(Color.FromArgb(126,140,131),2);
        using var leaf=new SolidBrush(Color.FromArgb(151,161,147));
        for(int cell=(int)(game.CameraX/135)-1;cell<=(int)((game.CameraX+1060)/135);cell++)
        {
            float x=cell*135+28;
            if(game.TerrainYAt(x) is not float floor)continue;
            for(int n=-1;n<=1;n++)
            {
                float root=x+n*9,ground=game.TerrainYAt(root)??floor;
                g.DrawLine(stem,root,ground,root+n*7,ground-20-(cell+n+30)%3*6);
                g.FillEllipse(leaf,root+n*7-3,ground-25-(cell+n+30)%3*6,6,9);
            }
        }
    }
}
