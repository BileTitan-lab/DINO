using System.Media;
using System.Text;

namespace DinoDash.Desktop;

// Small original PCM chime, embedded in code: no files or external audio dependency.
internal static class ActRareFeedback
{
    private static readonly MemoryStream stream=new(CreateWave());
    private static readonly SoundPlayer player=new(stream);
    internal static bool Enabled { get; set; }=true;
    internal static byte[] CreateWave()
    {
        const int rate=22050,count=11025;
        using var data=new MemoryStream();using var writer=new BinaryWriter(data,Encoding.ASCII,true);
        writer.Write(Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+count*2);writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
        writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(rate);writer.Write(rate*2);
        writer.Write((short)2);writer.Write((short)16);writer.Write(Encoding.ASCII.GetBytes("data"));writer.Write(count*2);
        for(int i=0;i<count;i++)
        {
            double t=i/(double)rate,envelope=Math.Min(1,t*45)*Math.Exp(-t*7);
            double note=Math.Sin(2*Math.PI*523.25*t)+.65*Math.Sin(2*Math.PI*659.25*t)+.4*Math.Sin(2*Math.PI*783.99*t);
            writer.Write((short)(note*envelope*6500));
        }
        writer.Flush();return data.ToArray();
    }
    public static void Play()
    {
        if(!Enabled)return;
        try { player.Play(); }
        catch (Exception e) when(e is InvalidOperationException or System.ComponentModel.Win32Exception or IOException) { }
    }
}
