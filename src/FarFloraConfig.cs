namespace FarFlora
{
    public class FarFloraConfig
    {
        public bool Enabled = true;
        public int FarDistanceBlocks = 160;
        public int TickMs = 250;
        public int MaxRetessPerTick = 48;
        public string[] ExcludePrefixes = new[] { "crop-", "wildvine", "lichen" };
        public string[] IncludePrefixes = new string[0];
        public bool LogStats = false;
    }
}
