using BulletHeavenFortressDefense.Data;

namespace BulletHeavenFortressDefense.Entities
{
    public interface IDamageable
    {
        void TakeDamage(float amount, DamageType damageType);
        bool IsAlive { get; }
    }
}
