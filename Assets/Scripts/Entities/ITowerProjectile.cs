namespace BulletHeavenFortressDefense.Entities
{
    public interface ITowerProjectile
    {
        void Initialize(Data.TowerData source, UnityEngine.Vector3 direction, string poolId);
    }
}
