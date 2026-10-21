using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace PartyTricks
{
    // Everything Party Tricks shows is drawn from code (no image files): soft dots and a flame shape, with the game's
    // own sprite materials and its characters' sorting layer.
    internal static class Art
    {
        private static Sprite dot, flame, log;
        private static Material unlit, lit;

        public static Sprite Dot => dot ? dot : dot = Make(32, (x, y) => Mathf.Clamp01(1f - new Vector2(x - 0.5f, y - 0.5f).magnitude * 2f), 64f);
        // A teardrop: round at the bottom, pointed at the top.
        public static Sprite Flame => flame ? flame : flame = Make(32, (x, y) =>
        {
            float w = Mathf.Lerp(0.45f, 0.02f, Mathf.Pow(y, 1.4f)) * (y < 0.3f ? Mathf.Sqrt(Mathf.Clamp01(y / 0.3f)) : 1f);
            float d = Mathf.Abs(x - 0.5f);
            return d > w ? 0f : Mathf.Clamp01((w - d) / Mathf.Max(0.05f, w) * 2.2f);
        }, 32f);
        public static Sprite Log => log ? log : log = Make(16, (x, y) => y > 0.3f && y < 0.7f ? 1f : 0f, 16f);

        private static Sprite Make(int size, System.Func<float, float, float> alpha, float pixelsPerUnit)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    px[y * size + x] = new Color(1f, 1f, 1f, alpha((x + 0.5f) / size, (y + 0.5f) / size));
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0f), pixelsPerUnit);
        }

        // The game's 2D sprite materials (unlit: glows at night too; lit: like the world around it).
        public static Material Unlit => unlit ? unlit : unlit = FindMaterial("Sprite-Unlit") ?? Lit;
        public static Material Lit => lit ? lit : lit = FindMaterial("Sprite-Lit") ?? PlayerMaterial();

        private static Material FindMaterial(string shaderPart) =>
            Resources.FindObjectsOfTypeAll<Material>().FirstOrDefault(m => m != null && m.shader != null && m.shader.name.Contains(shaderPart));

        private static Material PlayerMaterial()
        {
            var sr = Object.FindAnyObjectByType<PlayerMaster>()?.GetComponentInChildren<SpriteRenderer>();
            return sr != null ? sr.sharedMaterial : null;
        }

        // Sorted like a character (the player's sorting group: in front of what's behind it, behind what's in front).
        public static void SortLikeCharacters(GameObject go, int orderOffset = 0)
        {
            var group = go.AddComponent<SortingGroup>();
            var player = Object.FindAnyObjectByType<PlayerMaster>();
            var theirs = player != null ? player.GetComponentInChildren<SortingGroup>() : null;
            if (theirs != null) { group.sortingLayerID = theirs.sortingLayerID; group.sortingOrder = theirs.sortingOrder + orderOffset; }
        }

        public static SpriteRenderer Renderer(Transform parent, string name, Sprite sprite, Color colour, Material material, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = colour;
            if (material != null) sr.sharedMaterial = material;
            sr.sortingOrder = order;
            return sr;
        }
    }

    // A small burst of glowing dots (a cheer).
    internal class Sparks : MonoBehaviour
    {
        private const float Life = 0.8f;
        private Transform[] dots;
        private SpriteRenderer[] renderers;
        private Vector2[] velocity;
        private float started;
        private Color colour;

        public static void Burst(Vector3 at, Color colour)
        {
            var go = new GameObject("PartyTricks_Sparks");
            go.transform.position = at;
            Art.SortLikeCharacters(go, 50);
            go.AddComponent<Sparks>().Begin(colour);
        }

        private void Begin(Color c)
        {
            colour = c;
            started = Time.time;
            const int n = 10;
            dots = new Transform[n];
            renderers = new SpriteRenderer[n];
            velocity = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                renderers[i] = Art.Renderer(transform, "spark", Art.Dot, c, Art.Unlit, i);
                dots[i] = renderers[i].transform;
                dots[i].localScale = Vector3.one * Random.Range(0.25f, 0.45f);
                float a = (i + Random.value * 0.5f) / n * Mathf.PI * 2f;
                velocity[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.8f + 0.6f) * Random.Range(1.5f, 2.6f);
            }
        }

        private void Update()
        {
            float t = (Time.time - started) / Life;
            if (t >= 1f) { Destroy(gameObject); return; }
            for (int i = 0; i < dots.Length; i++)
            {
                velocity[i] += Vector2.down * 3f * Time.deltaTime;
                dots[i].localPosition += (Vector3)(velocity[i] * Time.deltaTime);
                renderers[i].color = new Color(colour.r, colour.g, colour.b, 1f - t * t);
            }
        }
    }
}
