namespace MultiLineFishing
{
    public class PlayerConfig
    {
        public bool Enabled { get; set; } = false;
        public int ExtraLines { get; set; } = 2; // Number of extra bobbers (default 2, range 1–5)
    }
}