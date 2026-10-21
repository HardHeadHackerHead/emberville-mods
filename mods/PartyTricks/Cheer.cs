using System.Collections.Generic;
using System.Linq;
using EmbervilleMP.Api;
using UnityEngine;

namespace PartyTricks
{
    // Cheering. Everyone hears a cheer (partytricks.cheer: the sender's position) and shows it over that player's
    // character or puppet. The host also keeps the last cheer of each player: two players cheering within ComboWindow
    // of each other, in one scene and a few steps apart, are told (partytricks.combo, host only) to run faster for a
    // while; each copy speeds up its own character. The host counts cheers in the world's store and combos in the
    // session's.
    internal static class Cheer
    {
        public const float ComboWindow = 2f, ComboDistance = 3f, Cooldown = 0.6f;

        private static MpChannel cheer, combo;
        private static float lastCheer = -100f;
        private static readonly Dictionary<byte, (float time, string scene, Vector2 pos)> recent = new Dictionary<byte, (float, string, Vector2)>();

        // What's on screen: a bubble per cheer (tests read it).
        internal class Bubble { public MpPlayer Player; public string Text; public float Until, Started; public Color Colour; }
        internal static readonly List<Bubble> Bubbles = new List<Bubble>();
        internal static readonly List<(byte player, string text)> Shown = new List<(byte, string)>();
        internal static int CombosDecided;

        internal static MpStore World => Multiplayer.WorldStore(PartyTricksPlugin.Guid);
        internal static MpStore Session => Multiplayer.SessionStore(PartyTricksPlugin.Guid);

        public static void Init()
        {
            cheer = Multiplayer.RegisterChannel("partytricks.cheer", new ChannelOptions { MaxSize = 96, Rate = 3f, Burst = 6f });
            combo = Multiplayer.RegisterChannel("partytricks.combo", new ChannelOptions { MaxSize = 32, Rate = 2f, Burst = 4f, WhoMaySend = WhoMaySend.HostOnly });
            cheer.Received += (from, r) =>
            {
                string scene = r.GetString();
                var pos = new Vector2(r.GetFloat(), r.GetFloat());
                Show(from, "Cheer!", new Color(1f, 0.85f, 0.35f));
                if (Multiplayer.IsHost) OnCheer(from, scene, pos);
            };
            combo.Received += (from, r) =>
            {
                int n = r.GetByte();
                var ids = new List<byte>();
                for (int i = 0; i < n && i < 4; i++) ids.Add(r.GetByte());
                float seconds = r.GetFloat(), percent = r.GetFloat();
                Combo(ids, seconds, percent);
            };
            Multiplayer.SessionEnded += () => { recent.Clear(); Bubbles.Clear(); Boost.Stop(); };
        }

        // The key (or a test): cheer here, for everyone.
        public static bool Do()
        {
            var me = Multiplayer.LocalPlayer;
            if (me == null || me.IsDead || Time.realtimeSinceStartup - lastCheer < Cooldown) return false;
            lastCheer = Time.realtimeSinceStartup;
            string scene = me.Scene;
            Vector2 pos = me.Position;
            Show(me, "Cheer!", new Color(1f, 0.85f, 0.35f));
            cheer.SendToAll(w => { w.Put(scene); w.Put(pos.x); w.Put(pos.y); });
            if (Multiplayer.IsHost) OnCheer(me, scene, pos);
            return true;
        }

        // Host: count it, and look for a partner.
        private static void OnCheer(MpPlayer p, string scene, Vector2 pos)
        {
            World.Set("cheers", World.Get("cheers", 0) + 1);
            float now = Time.realtimeSinceStartup;
            recent[p.Id] = (now, scene, pos);
            var partner = recent.Where(kv => kv.Key != p.Id && now - kv.Value.time <= ComboWindow && kv.Value.scene == scene && (kv.Value.pos - pos).magnitude <= ComboDistance)
                .Select(kv => kv.Key).Cast<byte?>().FirstOrDefault();
            if (partner == null) return;
            recent.Remove(p.Id);
            recent.Remove(partner.Value);
            var ids = new List<byte> { partner.Value, p.Id };
            float seconds = PartyTricksPlugin.BuffSeconds.Value, percent = PartyTricksPlugin.BuffPercent.Value;   // (the host's settings)
            CombosDecided++;
            Session.Set("combos", Session.Get("combos", 0) + 1);
            combo.SendToAll(w => { w.Put((byte)ids.Count); foreach (var id in ids) w.Put(id); w.Put(seconds); w.Put(percent); });
            Combo(ids, seconds, percent);
        }

        private static void Combo(List<byte> ids, float seconds, float percent)
        {
            foreach (var id in ids)
            {
                var p = Multiplayer.GetPlayer(id);
                if (p == null) continue;
                Show(p, "Together!", new Color(0.55f, 0.95f, 0.75f));
                if (p.IsLocal) Boost.Start(Mathf.Clamp(seconds, 0f, 60f), Mathf.Clamp(percent, 0f, 100f));
            }
        }

        // ---- on screen ----

        private static void Show(MpPlayer p, string text, Color colour)
        {
            if (p == null) return;
            float now = Time.realtimeSinceStartup;
            Bubbles.RemoveAll(b => b.Player.Id == p.Id);
            Bubbles.Add(new Bubble { Player = p, Text = text, Started = now, Until = now + 1.8f, Colour = colour });
            Shown.Add((p.Id, text));
            if (Shown.Count > 50) Shown.RemoveAt(0);
            var character = p.Character;
            if (character != null) Sparks.Burst(character.transform.position + Vector3.up * 1.2f, colour);
        }

        private static GUIStyle style;

        public static void OnGUI()
        {
            if (Bubbles.Count == 0) return;
            var cam = Camera.main;
            float now = Time.realtimeSinceStartup;
            Bubbles.RemoveAll(b => now > b.Until);
            if (cam == null) return;
            if (style == null) style = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            foreach (var b in Bubbles)
            {
                var character = b.Player.Character;
                if (character == null) continue;
                float t = (now - b.Started) / (b.Until - b.Started);
                Vector3 sp = cam.WorldToScreenPoint(character.transform.position + Vector3.up * (1.9f + t * 0.4f));
                if (sp.z < 0) continue;
                var rect = new Rect(sp.x - 80, Screen.height - sp.y - 15, 160, 30);
                float alpha = t < 0.8f ? 1f : 1f - (t - 0.8f) / 0.2f;
                style.normal.textColor = new Color(0f, 0f, 0f, 0.6f * alpha);
                GUI.Label(new Rect(rect.x + 2, rect.y + 2, rect.width, rect.height), b.Text, style);
                style.normal.textColor = new Color(b.Colour.r, b.Colour.g, b.Colour.b, alpha);
                GUI.Label(rect, b.Text, style);
            }
        }
    }

    // A cheer together: faster for a while (the game's own move speed stat, as its speed potions do it).
    internal static class Boost
    {
        private static CharacterStats stats;
        private static Modifier modifier;
        private static float until;
        private static BoostTimer timer;

        public static bool Active => modifier != null;
        public static float Left => Active ? Mathf.Max(0f, until - Time.realtimeSinceStartup) : 0f;

        public static void Start(float seconds, float percent)
        {
            var me = Object.FindAnyObjectByType<PlayerMaster>();
            var local = Multiplayer.LocalPlayer?.Character;
            var pm = local != null ? local.GetComponent<PlayerMaster>() : me;
            if (pm == null) return;
            Stop();
            stats = pm.GetCharacterStats();
            modifier = new Modifier(percent, ModifierType.Multiplier);
            stats.GetSecondaryStat(SecondaryStat.MoveSpeed).AddModifier(modifier);
            until = Time.realtimeSinceStartup + seconds;
            if (timer == null) timer = new GameObject("PartyTricks_Boost").AddComponent<BoostTimer>();
            Multiplayer.ChatMessage($"Cheered together: you're faster for {seconds:0} s!");
            PartyTricksPlugin.Log.LogInfo($"[cheer] Boost: +{percent:0}% speed for {seconds:0} s");
        }

        public static void Stop()
        {
            if (modifier != null && stats != null) stats.GetSecondaryStat(SecondaryStat.MoveSpeed).RemoveModifier(modifier);
            modifier = null;
            stats = null;
        }

        public static void Tick()
        {
            if (Active && (Time.realtimeSinceStartup >= until || stats == null)) Stop();
        }

        private class BoostTimer : MonoBehaviour
        {
            private void Awake() => DontDestroyOnLoad(gameObject);
            private void Update() => Tick();
        }
    }
}
