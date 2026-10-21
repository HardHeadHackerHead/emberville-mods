using BepInEx;
using BepInEx.Configuration;
using EmbervilleMP.Api;
using UnityEngine;

namespace PartyTricks
{
    // Party Tricks: small things to do together, made with Emberville Multiplayer's public API (an example mod: see
    // docs/API.md in the multiplayer mod). Multiplayer only (hard dependency); every player needs it, same version.
    //   Cheer (G): a spark and "Cheer!" over your character, seen by your teammates; two players cheering together
    //              (within 2 s, a few steps apart) both run faster for a while. The host decides.
    //   Campfire (B): a campfire at your feet, for everyone; it slowly heals whoever stands by it and burns out after a
    //              few minutes. B next to one puts it out. It stays in the host's world.
    [BepInPlugin(Guid, "Party Tricks", Version)]
    [BepInDependency(MultiplayerGuid, BepInDependency.DependencyFlags.HardDependency)]
    [MultiplayerMod(Requirement.Everyone)]
    public class PartyTricksPlugin : BaseUnityPlugin
    {
        public const string Guid = "quadslab.emberville.partytricks";
        public const string Version = "1.0.0";
        public const string MultiplayerGuid = "dev.emberville.mp";

        internal static PartyTricksPlugin Instance;
        internal static BepInEx.Logging.ManualLogSource Log;
        internal static ConfigEntry<KeyCode> CheerKey, CampfireKey;
        internal static ConfigEntry<float> BuffSeconds, BuffPercent, CampfireMinutes, HealRadius;

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            // (C and V are the game's character panel and the multiplayer mod's push-to-talk)
            CheerKey = Config.Bind("Keys", "Cheer", KeyCode.G, "Cheer: your teammates see it; cheer together for a speed boost.");
            CampfireKey = Config.Bind("Keys", "Campfire", KeyCode.B, "Light a campfire at your feet (next to one: put it out).");
            BuffSeconds = Config.Bind("Cheer", "BoostSeconds", 8f, "How long a cheer together makes you faster.");
            BuffPercent = Config.Bind("Cheer", "BoostPercent", 25f, "How much faster (percent of your move speed).");
            CampfireMinutes = Config.Bind("Campfire", "BurnMinutes", 4f, "How long a campfire burns.");
            HealRadius = Config.Bind("Campfire", "HealRadius", 2.5f, "How close to a campfire you must stand to heal.");

            if (Multiplayer.ApiVersion < 1) { Log.LogError("Emberville Multiplayer is too old for Party Tricks"); return; }
            Cheer.Init();
            Campfires.Init();
            Multiplayer.PlayerJoined += Greet;
            Log.LogInfo($"Party Tricks {Version} loaded (cheer: {CheerKey.Value}, campfire: {CampfireKey.Value})");
        }

        // The host greets each newcomer; the newcomer words it with its own keys.
        private static readonly MpChannel hello = Multiplayer.RegisterChannel("partytricks.hello", new ChannelOptions { MaxSize = 8, Rate = 1f, Burst = 3f, WhoMaySend = WhoMaySend.HostOnly });

        static PartyTricksPlugin()
        {
            hello.Received += (from, r) =>
                Multiplayer.ChatMessage($"Party Tricks is on: press {CheerKey.Value} to cheer, {CampfireKey.Value} to light a campfire.");
        }

        private static void Greet(MpPlayer p)
        {
            if (!Multiplayer.IsHost) return;
            // (self-tests read the chat: only Party Tricks' own scenarios get the greeting)
            if (Multiplayer.Testing.Active && !Multiplayer.Testing.Current.StartsWith("partytricks")) return;
            hello.SendTo(p, null);
        }

        private void Update()
        {
            if (!Multiplayer.InSession || Multiplayer.TextFieldOpen) return;
            if (Input.GetKeyDown(CheerKey.Value)) Cheer.Do();
            if (Input.GetKeyDown(CampfireKey.Value)) Campfires.KeyPressed();
        }

        private void OnGUI() => Cheer.OnGUI();
    }
}
