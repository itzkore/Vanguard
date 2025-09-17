using System.Collections.Generic;
using UnityEngine;

namespace BulletHeavenFortressDefense.Data
{
    [CreateAssetMenu(fileName = "WaveSequence", menuName = "BHFD/Data/Wave Sequence")]
    public class WaveSequence : ScriptableObject
    {
        [SerializeField] private List<WaveData> waves = new();

        public List<WaveData> Waves => waves;
    }
}
