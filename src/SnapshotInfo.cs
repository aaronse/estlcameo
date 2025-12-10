namespace EstlCameo
{
    public class SnapshotInfo
    {
        public DateTime Timestamp { get; set; }
        public string SnapshotPath { get; set; }
        public string PreviewImagePath { get; set; }

        // Optional: precomputed relative text to avoid recomputing on every paint
        public string RelativeText { get; set; }
    }
}
