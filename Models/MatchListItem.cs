namespace STSAnaliza
{
    public class MatchListItem
    {
        public string Tournament { get; set; } = "";
        public string PlayerA { get; set; } = "";
        public string PlayerB { get; set; } = "";
        public string Day { get; set; } = "";
        public string Hour { get; set; } = "";
        public decimal? OddA { get; set; }
        public decimal? OddB { get; set; }

        public int SourceIndex { get; set; }

        public string? Surface { get; set; }   // <<FILL_3>>
        //public string? Round { get; set; }     // <<FILL_4>>
        public string? FormatMeczu { get; set; }    // <<FILL_5>>

    }
}
