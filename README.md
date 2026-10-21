<div align="center">

<img src="mods/PartyTricks/cover.png" alt="Party Tricks: friends cheering around a healing campfire" width="760">

### Mods for Emberville co-op, and how to make your own.

</div>

This repo holds mods built on **Emberville Multiplayer**, the online co-op mod for
[Emberville](https://store.steampowered.com/app/5355880/), and the guide to its public API for anyone who wants their
own mod to work in multiplayer.

> **Unofficial fan mods.** Not affiliated with or endorsed by Cygnus Cross. Built for the free **Emberville Demo**;
> Early Access support follows its release.

## The mods

| Mod | What it does | Who needs it |
|---|---|---|
| [**Party Tricks**](mods/PartyTricks) | Press **G** to cheer: teammates see it, and cheering together makes you both run faster for a moment. Press **B** to light a campfire that slowly heals everyone standing by it, and stays in the host's world. | Every player in the session |
| [**Damage Colors**](mods/DamageColors) | Recolours the damage numbers on your own screen. A 30-line example of a mod that works with or without multiplayer. | Only you |

Both need [BepInEx 5](https://github.com/BepInEx/BepInEx). Party Tricks also needs Emberville Multiplayer; Damage
Colors works without it.

### Install

1. Install Emberville Multiplayer first (it comes with BepInEx).
2. Download a mod's zip and unzip it into the game's `BepInEx\plugins\` folder, so you get
   `BepInEx\plugins\PartyTricks\PartyTricks.dll`.
3. For Party Tricks, everyone you play with needs the same version. If someone doesn't have it, the host's game tells
   them to install it before they can join.

Keys and settings are in `BepInEx\config\` after the first start (for Party Tricks:
`quadslab.emberville.partytricks.cfg`).

## Making your own mod

Most mods work in multiplayer without any changes, as long as everyone installs them: Emberville Multiplayer shares
the game's own doors, chests, quests and story flags, so a mod that changes those the normal way is shared too.

A mod with mechanics of its own uses the API: who's in the session and where, your own messages between players,
shared data saved with the host's world, world objects and spawned objects every player sees the same, and a check
when someone joins that everyone has the mods they need.

**[Read the API guide](docs/API.md)**: setup, every call with an example, the guarantees, testing and releasing.

The two mods here are meant to be copied from:

| You want to... | Look at |
|---|---|
| Say "everyone needs my mod", or "only I need it" | `[MultiplayerMod(...)]` in [Party Tricks' Plugin.cs](mods/PartyTricks/Plugin.cs) and [DamageColors.cs](mods/DamageColors/DamageColors.cs) |
| Work with or without the multiplayer mod | [DamageColors.cs](mods/DamageColors/DamageColors.cs) (a soft dependency) |
| Send your own messages, let the host decide | [Cheer.cs](mods/PartyTricks/Cheer.cs) |
| Add a shared object that late joiners see and the host's world saves | [Campfires.cs](mods/PartyTricks/Campfires.cs) |
| Test it with two copies of the game | [Tests.cs](mods/PartyTricks/Tests.cs) |

## Building

You need the .NET SDK, the game, and Emberville Multiplayer installed in it (the mods compile against its DLL).

```powershell
.\scripts\build.ps1                                  # builds every mod in mods\
.\scripts\build.ps1 -Deploy                          # ...and copies each into the game's BepInEx\plugins\<Name>\
.\scripts\package.ps1 -MultiplayerVersion 0.2.0      # release zips in build\, mod-site packages in build\store\
```

The game folder defaults to `D:\SteamLibrary\steamapps\common\Emberville Demo`; pass `-GameDir` (or set
`EMB_GAMEDIR`) for yours. `-MultiplayerDir` points at a different `EmbervilleMP.dll`.

## Credits

- **Emberville** by [Cygnus Cross](https://store.steampowered.com/app/5355880/). All game content, names and artwork
  belong to them.
- BepInEx and HarmonyX by their respective authors.
- Made with the help of Claude (Anthropic). Party Tricks' cover and icon were made with AI image generation.
