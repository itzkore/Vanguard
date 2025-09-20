namespace BulletHeavenFortressDefense.Utilities
{
    /// <summary>
    /// Optional interface for runtime systems that need a deterministic reset before a new run starts.
    /// GameManager will invoke ResetForNewRun() (best-effort) after clearing the playfield but before waves begin.
    /// Keep implementations idempotent and fast (no allocations, no heavy scene searches).
    /// </summary>
    public interface IRunResettable
    {
        void ResetForNewRun();
    }
}
