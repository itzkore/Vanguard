using UnityEngine;
using BulletHeavenFortressDefense.Utilities;

namespace BulletHeavenFortressDefense.Systems
{
    public class DamageSystem : Singleton<DamageSystem>
    {
        public void ApplyDamage(Entities.IDamageable target, float amount, Data.DamageType damageType)
        {
            if (target == null)
            {
                return;
            }

            target.TakeDamage(amount, damageType);
        }
    }
}
