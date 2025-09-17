using UnityEngine;

namespace BulletHeavenFortressDefense.Visuals
{
    public class LevelVisualInitializer : MonoBehaviour
    {
        [SerializeField] private Vector2Int gridSize = new Vector2Int(32, 12);
        [SerializeField] private float tileSize = 1f;
        [SerializeField] private int walkwayHeight = 4;
        [SerializeField] private int baseZoneWidth = 6;
        [SerializeField] private Color backgroundColor = new Color(0.09f, 0.11f, 0.18f);
        [SerializeField] private Color walkwayColor = new Color(0.14f, 0.18f, 0.25f);
        [SerializeField] private Color baseZoneColor = new Color(0.18f, 0.22f, 0.32f);
        [SerializeField] private Transform container;

        private Sprite _backgroundSprite;
        private Sprite _walkwaySprite;
        private Sprite _baseSprite;

        private void Awake()
        {
            if (container == null)
            {
                var go = new GameObject("LevelVisuals")
                {
                    hideFlags = HideFlags.DontSave
                };
                go.transform.SetParent(transform);
                container = go.transform;
                container.localPosition = Vector3.zero;
            }

            _backgroundSprite = CreateSprite(backgroundColor);
            _walkwaySprite = CreateSprite(walkwayColor);
            _baseSprite = CreateSprite(baseZoneColor);

            GenerateGround();
        }

        private void GenerateGround()
        {
            ClearChildren();

            int halfHeight = gridSize.y / 2;
            for (int x = 0; x < gridSize.x; x++)
            {
                for (int y = -halfHeight; y <= halfHeight; y++)
                {
                    var sprite = ChooseSpriteFor(x, y + halfHeight);
                    SpawnTile(sprite, new Vector3((x - gridSize.x / 2f) * tileSize, y * tileSize, 5f));
                }
            }
        }

        private Sprite ChooseSpriteFor(int x, int yIndex)
        {
            if (x < baseZoneWidth)
            {
                return _baseSprite;
            }

            int walkwayStart = Mathf.Max(0, (gridSize.y - walkwayHeight) / 2);
            int walkwayEnd = Mathf.Min(gridSize.y, walkwayStart + walkwayHeight);
            if (yIndex >= walkwayStart && yIndex < walkwayEnd)
            {
                return _walkwaySprite;
            }

            return _backgroundSprite;
        }

        private void SpawnTile(Sprite sprite, Vector3 position)
        {
            var go = new GameObject("Tile")
            {
                hideFlags = HideFlags.DontSave | HideFlags.HideInHierarchy
            };
            go.transform.SetParent(container);
            go.transform.localPosition = position;
            go.transform.localScale = Vector3.one * tileSize;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = -10;
        }

        private void ClearChildren()
        {
            for (int i = container.childCount - 1; i >= 0; i--)
            {
                var child = container.GetChild(i);
                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }

        private Sprite CreateSprite(Color color)
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }
    }
}
