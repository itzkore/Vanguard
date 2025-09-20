namespace BulletHeavenFortressDefense.Phase
{
    /// <summary>
    /// Implement on components that want callbacks when the global WavePhase changes.
    /// Lightweight alternative to UnityEvents – registry dispatch avoids scene-wide searches.
    /// </summary>
    public interface IPhaseAware
    {
        void OnPhaseChanged(BulletHeavenFortressDefense.Managers.WaveManager.WavePhase newPhase);
    }
}
