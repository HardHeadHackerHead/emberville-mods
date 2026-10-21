using System.Collections;
using System.Globalization;
using System.Linq;
using EmbervilleMP.Api;
using UnityEngine;
using T = EmbervilleMP.Api.Multiplayer.Testing;

namespace PartyTricks
{
    // TEST ONLY: Party Tricks' self-tests, run by Ember Multiplayer's harness (scripts\test-all.ps1 reads the
    // [MpScenario] lines; the multiplayer mod finds them in this DLL). Nothing here runs in play.
    //   partytricks-cheer    : a cheer shows over the cheering player on both copies; two players cheering together
    //                          close by both get the boost (decided by the host), counted in the world's and the
    //                          session's stores; apart, no boost
    //   partytricks-campfire : (the guest joins during it) the host lights a fire before the guest comes; it heals; it's
    //                          saved with the world and back after a reload; the late joiner sees it with its time left
    //                          and is greeted; the guest's own fire is made by the runner; the guest puts the first out;
    //                          a short one burns out on both
    internal static class Tests
    {
        private static string F(float f) => f.ToString("R", CultureInfo.InvariantCulture);
        private static float Pf(string s) => float.Parse(s, CultureInfo.InvariantCulture);
        private static string V(Vector2 v) => F(v.x) + ":" + F(v.y);
        private static Vector2 Pv(string s) { var p = s.Split(':'); return new Vector2(Pf(p[0]), Pf(p[1])); }
        private static Campfire Fire(int id) => Campfires.All.FirstOrDefault(f => f != null && f.NetId == id);

        [MpScenario("partytricks-cheer", Timeout = 200, Covers = "Api/*")]
        public static IEnumerator CheerTest()
        {
            yield return T.Ready();
            var me = Multiplayer.LocalPlayer;
            var other = Multiplayer.Players.FirstOrDefault(p => !p.IsLocal);
            if (!T.Check("two players", me != null && other != null)) { yield return T.Finish(""); yield break; }
            // Side by side: the guest goes next to the host.
            if (Multiplayer.IsHost) T.Tell("pos", V(me.Position));
            else
            {
                yield return T.WaitFor(() => T.Heard("pos") != null, 10f);
                if (T.Heard("pos") != null) T.MoveLocalPlayer(Pv(T.Heard("pos")) + new Vector2(1.5f, 0f));
            }
            yield return T.Barrier("near");
            yield return T.WaitFor(() => other.Character != null && (other.Position - me.Position).magnitude < Cheer.ComboDistance, 10f);
            T.Check("side by side, the other one shown", other.Character != null && (other.Position - me.Position).magnitude < Cheer.ComboDistance, $"{(other.Position - me.Position).magnitude:F2} apart");
            var pm = me.Character.GetComponent<PlayerMaster>();
            Boost.Stop();
            float speed = pm.GetCharacterStats().CurrentMoveSpeed;
            int cheers = Cheer.World.Get("cheers", 0), combos = Cheer.Session.Get("combos", 0);
            Cheer.Shown.Clear();
            yield return T.Barrier("one");

            // 1. One cheer: over the cheering player, everywhere; no boost.
            if (Multiplayer.IsHost)
            {
                T.Check("the host cheers", Cheer.Do());
                T.Check("shown over our own character", Cheer.Shown.Contains((me.Id, "Cheer!")) && Cheer.Bubbles.Any(b => b.Player == me));
            }
            else
            {
                yield return T.WaitFor(() => Cheer.Shown.Contains((other.Id, "Cheer!")), 10f);
                T.Check("the host's cheer shows over its puppet here", Cheer.Shown.Contains((other.Id, "Cheer!")) && Cheer.Bubbles.Any(b => b.Player == other) && other.Character != null);
            }
            yield return new WaitForSecondsRealtime(Cheer.ComboWindow + 0.5f);   // (past the window: a lone cheer must give nothing)
            T.Check("a cheer alone: no boost", !Boost.Active);
            yield return T.Barrier("together");

            // 2. Together: the host pairs them, both get the boost.
            Cheer.Do();
            yield return T.WaitFor(() => Boost.Active, 10f);
            T.Check("cheering together: we're faster", Boost.Active && pm.GetCharacterStats().CurrentMoveSpeed > speed * 1.15f,
                $"{speed:F2} -> {pm.GetCharacterStats().CurrentMoveSpeed:F2}");
            yield return T.WaitFor(() => Cheer.Shown.Contains((other.Id, "Together!")), 5f);
            T.Check("\"Together!\" over both", Cheer.Shown.Contains((me.Id, "Together!")) && Cheer.Shown.Contains((other.Id, "Together!")));
            yield return T.Barrier("combo");

            // 3. The host's counts, on both copies: cheers in the world's store, combos in the session's.
            yield return T.WaitFor(() => Cheer.World.Get("cheers", 0) >= cheers + 3 && Cheer.Session.Get("combos", 0) == combos + 1, 10f);
            T.Check("cheers counted in the world store", Cheer.World.Get("cheers", 0) >= cheers + 3, $"{cheers} -> {Cheer.World.Get("cheers", 0)}");
            T.Check("one combo counted in the session store", Cheer.Session.Get("combos", 0) == combos + 1, $"{combos} -> {Cheer.Session.Get("combos", 0)}");
            Boost.Stop();
            T.Check("the boost ends", !Boost.Active && pm.GetCharacterStats().CurrentMoveSpeed <= speed * 1.01f);
            yield return T.Barrier("counted");

            // 4. Apart: no boost.
            if (!Multiplayer.IsHost) T.MoveLocalPlayer(me.Position + new Vector2(7f, 0f));
            yield return T.WaitFor(() => (other.Position - me.Position).magnitude > Cheer.ComboDistance + 1f, 10f);
            yield return T.Barrier("apart");
            Cheer.Do();
            yield return new WaitForSecondsRealtime(3f);   // (a combo would come within a round trip)
            T.Check("cheering apart: no boost", !Boost.Active && Cheer.Session.Get("combos", 0) == combos + 1, $"{(other.Position - me.Position).magnitude:F1} apart");
            yield return T.Barrier("done");
            yield return T.Finish("cheers, alone and together");
        }

        [MpScenario("partytricks-campfire", Timeout = 320, Start = "own-world", Covers = "Api/*")]
        public static IEnumerator CampfireTest()
        {
            yield return T.WaitInGame();
            if (Multiplayer.IsHost)
            {
                var me = Multiplayer.LocalPlayer;
                PartyTricksPlugin.Log.LogInfo($"[test] Lighting a fire with {Multiplayer.PlayersInMyScene().Count} player(s) in the scene");
                var fireA = Campfires.Place(me.Position + new Vector2(1f, 0f), 120f)?.GetComponent<Campfire>();
                T.Check("the host (it runs its scene) lights a fire at once", fireA != null && fireA.NetId != 0);
                if (fireA == null) { yield return T.Finish(""); yield break; }
                int idA = fireA.NetId;
                // It heals whoever stands by it (each copy its own player).
                var pm = me.Character.GetComponent<PlayerMaster>();
                pm.currentHealth = Mathf.Max(1, pm.MaxHealthPoints / 2);
                int hp = pm.CurrentHealthPoints, heals = Campfires.Heals;
                yield return T.WaitFor(() => pm.CurrentHealthPoints > hp, 8f);
                T.Check("standing by it heals", pm.CurrentHealthPoints > hp && Campfires.Heals > heals, $"{hp} -> {pm.CurrentHealthPoints}");
                // Saved with the host's world: back after a reload, with its time left.
                yield return new WaitForSecondsRealtime(2f);
                float before = fireA.Remaining;
                T.SaveAndReloadWorld();
                yield return T.WaitFor(() => Fire(idA) != null && Fire(idA) != fireA, 10f);
                fireA = Fire(idA);
                T.Check("saved with the world: back after a reload, same id", fireA != null);
                T.Check("... with its time left", fireA != null && Mathf.Abs(fireA.Remaining - before) < 3f && fireA.Remaining < 118f, $"{before:F1} -> {fireA?.Remaining:F1}");
                if (fireA == null) { yield return T.Finish(""); yield break; }
                yield return T.Ready();   // (the guest has joined and is in our world: told only now, before it'd reach nobody)
                T.Tell("fireA", idA.ToString(CultureInfo.InvariantCulture));
                yield return new WaitForSecondsRealtime(1f);
                T.Tell("remA", F(fireA.Remaining));
                yield return T.Barrier("joined", 60f);

                // The guest lights one: made here (we run the scene).
                var guest = Multiplayer.Players.FirstOrDefault(p => !p.IsLocal);
                yield return T.WaitFor(() => Campfires.All.Any(f => f != null && f.NetId != idA), 15f);
                var fireB = Campfires.All.FirstOrDefault(f => f != null && f.NetId != idA);
                T.Check("the guest's fire was lit here, by the runner", fireB != null && fireB.PlacedBy == guest?.Name, fireB?.PlacedBy);
                T.Tell("fireB", (fireB?.NetId ?? 0).ToString(CultureInfo.InvariantCulture));
                yield return T.Barrier("b", 30f);
                // The guest puts the first one out.
                yield return T.WaitFor(() => fireA == null, 15f);
                T.Check("the guest put the first fire out", fireA == null && Fire(idA) == null);
                T.Check("... and everyone was told", T.ChatHas("put out a campfire"));
                yield return T.Barrier("out", 30f);
                // A short one burns out by itself.
                var fireC = Campfires.Place(me.Position + new Vector2(-1.5f, 0f), 5f)?.GetComponent<Campfire>();
                T.Check("a short fire", fireC != null);
                T.Tell("fireC", (fireC?.NetId ?? 0).ToString(CultureInfo.InvariantCulture));
                yield return T.WaitFor(() => fireC == null, 15f);
                T.Check("it burns out by itself", fireC == null);
                yield return T.Barrier("burnt", 30f);
            }
            else
            {
                yield return T.Ready();
                var me = Multiplayer.LocalPlayer;
                yield return T.WaitFor(() => T.ChatHas("Party Tricks is on"), 15f);
                T.Check("greeted by the host on joining", T.ChatHas("Party Tricks is on"));
                yield return T.WaitFor(() => T.Heard("fireA") != null && T.Heard("remA") != null, 20f);
                int idA = int.Parse(T.Heard("fireA") ?? "0", CultureInfo.InvariantCulture);
                yield return T.WaitFor(() => Fire(idA) != null, 20f);
                var fireA = Fire(idA);
                T.Check("the late joiner sees the host's fire, same id", fireA != null);
                float hostLeft = T.Heard("remA") != null ? Pf(T.Heard("remA")) : -1f;
                T.Check("... with its time left", fireA != null && Mathf.Abs(fireA.Remaining - hostLeft) < 5f, $"{fireA?.Remaining:F1} here, {hostLeft:F1} on the host");
                yield return T.Barrier("joined", 60f);

                var asked = Campfires.Place(me.Position + new Vector2(0f, 2.5f), 120f);
                T.Check("a guest's fire is asked of the runner (nothing here yet)", asked == null);
                yield return T.WaitFor(() => Campfires.All.Any(f => f != null && f.NetId != idA) && T.Heard("fireB") != null, 15f);
                var fireB = Campfires.All.FirstOrDefault(f => f != null && f.NetId != idA);
                T.Check("it came from the runner, same id", fireB != null && fireB.NetId.ToString(CultureInfo.InvariantCulture) == T.Heard("fireB"));
                yield return T.Barrier("b", 30f);
                Campfires.PutOut(fireA);
                yield return T.WaitFor(() => fireA == null, 15f);
                T.Check("put out: gone here too", fireA == null && Fire(idA) == null);
                yield return T.Barrier("out", 30f);
                yield return T.WaitFor(() => T.Heard("fireC") != null, 15f);
                int idC = int.Parse(T.Heard("fireC") ?? "0", CultureInfo.InvariantCulture);
                yield return T.WaitFor(() => Fire(idC) != null, 10f);
                T.Check("the short fire is here", Fire(idC) != null);
                yield return T.WaitFor(() => Fire(idC) == null, 15f);
                T.Check("... and burns out here too", Fire(idC) == null);
                yield return T.Barrier("burnt", 30f);
            }
            yield return T.Finish("campfires");
        }
    }
}
