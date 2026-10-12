namespace BotPlusAutocadTool.Models
{
    public sealed class CustomerOrderOptions
    {
        public bool CreateNewFile { get; set; }
        public bool ExplodeBlocks { get; set; }
        public bool ExplodePolylines { get; set; }
        public bool ExplodeText { get; set; }
        public bool PurgeUnused { get; set; }
        public bool BlockCopy { get; set; }
    }
}
