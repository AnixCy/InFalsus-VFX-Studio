namespace InFalsus.Presentation.Game;

// Counts only resources owned by this backend, not Unity's global resource totals.
public readonly record struct RenderResourceCounts(int Effects, int Preparations, int Materials, int Bundles,
    int BundleLeases, int PendingLoads, int RetiredGroups, int RenderRegistrations, int CleanupFailures, int Images);
public readonly record struct RenderDiagnostics(int Effects, int Preparations, int Materials, int Bundles,
    int BundleLeases, int PendingLoads, int RetiredGroups, int RenderRegistrations,
    long PostProcessCallbacks, long PassesRecorded, long BackbufferSkips, long RenderErrors, int CleanupFailures, int Images)
{
    public RenderResourceCounts AllocationState => new(Effects, Preparations, Materials, Bundles,
        BundleLeases, PendingLoads, RetiredGroups, RenderRegistrations, CleanupFailures, Images);
}
