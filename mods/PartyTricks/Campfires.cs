using System.Collections.Generic;
using System.Linq;
using EmbervilleMP.Api;
using LiteNetLib.Utils;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace PartyTricks
{
    // Campfires: spawned objects (Multiplayer.Spawn: the scene's runner makes them, every copy gets one with the same
    // net id, players arriving later too), saved with the host's world. Their state (seconds left) is an object kind
    // (CampfireKind): the runner's copy counts down for real and corrects the others every few seconds; when it's out,
    // the runner removes it. Putting one out is an intent to the runner. Each copy heals its own player near a fire.
    // A campfire burns only while someone is in its scene (nobody runs a scene nobody is in).
    internal static class Campfires
    {
        public const string Key = "partytricks.campfire";
        public const float PutOutReach = 1.5f, PlaceCooldown = 2f, ResyncEvery = 10f, HealEvery = 2f;
        public const int MaxPerScene = 6;

        internal static CampfireKind Kind;
        internal static readonly List<Campfire> All = new List<Campfire>();
        private static float lastPlaced = -100f;
        internal static int Heals;

        public static void Init()
        {
            Kind = new CampfireKind();
            Multiplayer.RegisterSpawnable(Key, Make, Kind, saved: true);
            // Someone arrived in our scene: as its runner, our fires' time left now (not up to ResyncEvery old).
            Multiplayer.PlayerChangedScene += p =>
            {
                if (p.IsLocal || !Multiplayer.RunsThisScene || p.Scene != Multiplayer.LocalPlayer?.Scene) return;
                foreach (var f in All.Where(f => f != null)) Multiplayer.ObjectChanged(Kind, f.gameObject);
            };
        }

        public static void KeyPressed()
        {
            var me = Multiplayer.LocalPlayer;
            if (me == null || me.IsDead || me.Character == null) return;
            var near = Nearest(me.Position);
            if (near != null && (near.Position - me.Position).magnitude <= PutOutReach) PutOut(near);
            else Place(me.Position, PartyTricksPlugin.CampfireMinutes.Value * 60f);
        }

        public static Campfire Nearest(Vector2 at) => All.Where(f => f != null).OrderBy(f => (f.Position - at).sqrMagnitude).FirstOrDefault();

        // A campfire here (the runner makes it; elsewhere it's asked and the fire arrives a moment later).
        public static GameObject Place(Vector2 at, float seconds)
        {
            if (Time.realtimeSinceStartup - lastPlaced < PlaceCooldown) return null;
            if (All.Count(f => f != null) >= MaxPerScene) { Multiplayer.ChatMessage("There are enough campfires here already."); return null; }
            lastPlaced = Time.realtimeSinceStartup;
            string by = Multiplayer.LocalPlayer?.Name ?? "";
            return Multiplayer.Spawn(Key, at, w => { w.Put(seconds); w.Put(by); });
        }

        public static void PutOut(Campfire fire)
        {
            if (fire != null) Multiplayer.SendIntent(Kind, fire.gameObject, w => w.Put((byte)1));
        }

        private static GameObject Make(NetDataReader r)
        {
            float seconds = r.GetFloat();
            string by = r.GetString();
            var go = new GameObject("PartyTricks_Campfire");
            var fire = go.AddComponent<Campfire>();
            fire.Remaining = seconds;
            fire.PlacedBy = by;
            return go;
        }
    }

    // The campfire's state for the multiplayer mod's object framework: seconds left.
    internal class CampfireKind : MpObjectHandler
    {
        public override string Kind => Campfires.Key;
        public override void Capture(GameObject obj, NetDataWriter w) => w.Put(obj.GetComponent<Campfire>().Remaining);
        public override void Apply(GameObject obj, NetDataReader r) => obj.GetComponent<Campfire>().Remaining = r.GetFloat();

        // On the scene's runner: someone wants it out.
        public override void OnIntent(GameObject obj, MpPlayer from, NetDataReader r)
        {
            if (r.GetByte() != 1) return;
            Multiplayer.Despawn(obj);
            Multiplayer.Announce($"{from.Name} put out a campfire.");
        }
    }

    internal class Campfire : MonoBehaviour
    {
        public float Remaining;
        public string PlacedBy = "";
        public Vector2 Position => transform.position;
        public int NetId => Multiplayer.NetIdOf(gameObject);

        private SpriteRenderer outer, inner;
        private Light2D glow;
        private float baseIntensity = 1.2f, nextResync, nextHeal;
        private float seed;

        private void Awake() => Campfires.All.Add(this);

        private void Start()
        {
            seed = Random.value * 10f;
            Art.SortLikeCharacters(gameObject);
            var logColour = new Color(0.36f, 0.22f, 0.13f);
            var log1 = Art.Renderer(transform, "log", Art.Log, logColour, Art.Lit, 0);
            log1.transform.localScale = new Vector3(0.9f, 0.5f, 1f);
            log1.transform.localRotation = Quaternion.Euler(0, 0, 18f);
            var log2 = Art.Renderer(transform, "log", Art.Log, logColour * 0.85f, Art.Lit, 1);
            log2.transform.localScale = new Vector3(0.9f, 0.5f, 1f);
            log2.transform.localRotation = Quaternion.Euler(0, 0, -18f);
            outer = Art.Renderer(transform, "flame", Art.Flame, new Color(1f, 0.45f, 0.12f, 0.95f), Art.Unlit, 2);
            outer.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            inner = Art.Renderer(transform, "flame-core", Art.Flame, new Color(1f, 0.86f, 0.4f, 0.95f), Art.Unlit, 3);
            inner.transform.localPosition = new Vector3(0f, 0.06f, 0f);
            inner.transform.localScale = Vector3.one * 0.55f;
            glow = MakeLight();
            nextResync = Time.realtimeSinceStartup + Campfires.ResyncEvery;
            nextHeal = Time.realtimeSinceStartup + Campfires.HealEvery;
        }

        // The player's own night light, copied (its 2D light setup: the layers it lights), made warm.
        private Light2D MakeLight()
        {
            Light2D light = null;
            var template = FindAnyObjectByType<PlayerMaster>()?.GetComponentsInChildren<Light2D>(true).FirstOrDefault();
            if (template != null)
            {
                var copy = Instantiate(template.gameObject, transform, false);
                copy.name = "glow";
                copy.transform.localPosition = new Vector3(0f, 0.3f, 0f);
                copy.SetActive(true);
                light = copy.GetComponent<Light2D>();
            }
            if (light == null) light = new GameObject("glow").AddComponent<Light2D>();
            if (light.transform.parent != transform) light.transform.SetParent(transform, false);
            light.lightType = Light2D.LightType.Point;
            light.enabled = true;
            light.color = new Color(1f, 0.62f, 0.3f);
            light.pointLightInnerRadius = 0.3f;
            light.pointLightOuterRadius = 3.2f;
            light.intensity = baseIntensity;
            return light;
        }

        private void OnDestroy() => Campfires.All.Remove(this);

        private void Update()
        {
            float now = Time.realtimeSinceStartup;
            Remaining = Mathf.Max(0f, Remaining - Time.deltaTime);
            // A flicker: scale and light breathing out of step.
            float f = Mathf.PerlinNoise(seed, Time.time * 3f);
            if (outer != null) outer.transform.localScale = new Vector3(0.9f + f * 0.15f, 0.95f + f * 0.35f, 1f);
            if (inner != null) inner.transform.localScale = new Vector3(0.5f + f * 0.08f, 0.5f + f * 0.2f, 1f);
            // (burning down: smaller in its last 20 seconds)
            float size = Mathf.Clamp01(Remaining / 20f) * 0.6f + 0.4f;
            transform.localScale = Vector3.one * size;
            if (glow != null) glow.intensity = baseIntensity * (0.85f + f * 0.3f) * size;

            // The runner's copy is the real one: corrects the others now and then, puts it out at the end.
            if (Multiplayer.InSession && Multiplayer.RunsThisScene)
            {
                if (Remaining <= 0f) { Multiplayer.Despawn(gameObject); return; }
                if (now >= nextResync) { nextResync = now + Campfires.ResyncEvery; Multiplayer.ObjectChanged(Campfires.Kind, gameObject); }
            }

            // Each copy warms its own player.
            if (now >= nextHeal)
            {
                nextHeal = now + Campfires.HealEvery;
                var me = Multiplayer.LocalPlayer?.Character;
                var pm = me != null ? me.GetComponent<PlayerMaster>() : null;
                if (pm != null && !pm.IsDead && pm.CurrentHealthPoints < pm.MaxHealthPoints &&
                    ((Vector2)me.transform.position - Position).magnitude <= PartyTricksPlugin.HealRadius.Value)
                {
                    int amount = Mathf.Max(1, Mathf.RoundToInt(pm.MaxHealthPoints * 0.04f));
                    if (pm.RestoreHealth(amount)) Campfires.Heals++;
                }
            }
        }
    }
}
