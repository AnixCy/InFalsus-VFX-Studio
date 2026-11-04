namespace InFalsusChartLoader
{
    /// <summary>Optional read-only bridge for recording the actual in-session version.</summary>
    public static class ChartSession
    {
        public static string HashOf(string logicalName) => ChartCatalog.SnapshotHash(logicalName);
    }
}
