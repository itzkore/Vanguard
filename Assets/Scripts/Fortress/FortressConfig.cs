using UnityEngine;

namespace BulletHeavenFortressDefense.Fortress
{
    [CreateAssetMenu(fileName = "FortressConfig", menuName = "BHFD/Fortress/Config")]
    public class FortressConfig : ScriptableObject
    {
        [SerializeField, Min(1)] private int rows = 3;
        [SerializeField, Min(1)] private int columns = 2;
        [SerializeField] private Vector2 cellSpacing = new Vector2(1.5f, 1.5f);
        [SerializeField] private Vector2 originOffset = new Vector2(-6f, 0f);
        [SerializeField, Min(0)] private int coreRow = 1;
        [SerializeField, Min(0)] private int coreColumn = 0;
        [SerializeField] private GameObject corePrefab;
        [SerializeField] private FortressWall wallPrefab;

        public int Rows => Mathf.Max(1, rows);
        public int Columns => Mathf.Max(1, columns);
        public Vector2 CellSpacing => cellSpacing;
        public Vector2 OriginOffset => originOffset;
        public int CoreRow => Mathf.Clamp(coreRow, 0, Rows - 1);
        public int CoreColumn => Mathf.Clamp(coreColumn, 0, Columns - 1);
        public GameObject CorePrefab => corePrefab;
        public FortressWall WallPrefab => wallPrefab;
    }
}
