# Mind Your Purse - Street Mugging

A Mount & Blade II: Bannerlord mod that brings back the town muggings from Warband.

One of the things I missed most from Mount & Blade: Warband was walking through a town at night and suddenly being jumped by a gang of thugs in a back alley. Bannerlord's towns felt a little too safe without it, so I made this mod to bring that danger back. Rough, rich towns are now risky places to carry a full purse, especially after dark.

Built for Bannerlord **v1.4.8** (singleplayer).

## Features
Muggings only happen **at night** by default. Turn on *Allow daytime muggings* in the settings to make Trade and the tavern district risky by day too; town walks are always night-only.

- **Trade** and **Go to the tavern district** (town menu): there's a chance the town center loads instead, with you next to the market stalls (Trade) or at the tavern door (tavern district), and the gang already standing around you. You then choose to **fight** or **hand over part of your purse**. If you pay, you stay in the scene and can walk to the merchant or into the tavern yourself.
- **Take a walk around the town center at night**: at a random moment 30–120 seconds into your walk, once you're at least 20 m from where you came in, the thugs may strike. Once they've surrounded you, you can **pay them off** or **fight**. This chance is rolled right then, in the streets: no guards nearby makes it 1.5x, and each guard within 30 m multiplies it by 0.6 (1 guard 0.9x, 2 guards 0.54x, 3 guards 0.32x).
- **Paying off**: the gang takes the gold, puts their weapons away and runs off together (they stop counting as enemies, so they won't turn on you), then disappears once they're about 35 m away (or after 15 seconds).
- **Guards help**: town guards who can see you being attacked join the fight on your side. Guards within 8 m always notice; farther ones (up to 25 m) must be looking your way with a clear view.
- Muggers wear **hoods**: about 40% get a blackened hood, the rest a hood or head wrap that fits the town's culture (fur hoods in Khuzait towns, keffiyehs in Aserai ones, and so on).
- Muggers carry **blunt weapons** (wooden hammers and clubs in safe towns, up to steel maces and mauls in the worst). This can be turned off in the settings.

## Scaling
- **Chance**: lower security and higher prosperity both raise it. Night raises trade/tavern odds, and night strolls are the riskiest. With 50 security and 4000 prosperity it's about 13% for trading at night (7% by day, if daytime muggings are on) and 20% for a night walk, before the gang-leader and guard modifiers.
- **Gang leaders**: your average relation with the town's gang-leader notables scales every chance. At -100 it's 2x, at 0 it's 1x, and at +100 it's 0.25x, on a smooth curve (-50 is about 1.41x, +50 is 0.5x). Towns without gang leaders are unaffected.
- **Difficulty**: 55% insecurity + 45% prosperity. This sets gang size (2–7) and quality, from looters up to the town culture's gang thugs, then regional bandits. The worst towns send a bandit chief as leader.
- **Lose**: you're knocked out and lose 15–50% of your gold (more in rougher, richer towns). This uses the game's own alley-fight defeat flow.
- **Win**: you loot the thugs' purses (more in richer towns), and the town gains +1 security.
- Each town can mug you at most once every 24 in-game hours. Nobody bothers you while you're disguised, during a siege, or with fewer than 50 denars.

## Requirements
These are all available on Nexus Mods and the Steam Workshop:
- Harmony
- ButterLib
- UIExtenderEx
- Mod Configuration Menu (MCM) v5

In the launcher, load Mind Your Purse - Street Mugging after all of these.

## Building from source
You need the [.NET SDK](https://dotnet.microsoft.com/download) (any recent version) and Bannerlord installed with the mods above.

```
cd src
dotnet build -c Release
```

The build copies `StreetMuggings.dll` and `SubModule.xml` into
`<Bannerlord>\Modules\StreetMuggings`. If Bannerlord isn't in the default Steam folder, run:
`dotnet build -c Release /p:GameFolder="D:\path\to\Mount & Blade II Bannerlord"`

Enable **Mind Your Purse - Street Mugging** in the launcher, below Harmony, ButterLib, UIExtenderEx and MCM.

## Settings (Mod Options > Mind Your Purse - Street Mugging)
- **General**: turn the mod or each trigger (trade, tavern, night walks) on or off, and allow daytime muggings (off by default).
- **Mugging Rate**: an overall rate multiplier, the base and maximum chance, day/night/walk multipliers, the gang-leader relation multipliers (at -100 and +100), the per-town cooldown and the minimum gold you must carry.
- **Night Walks**: the earliest and latest ambush time, the no-guards and per-guard multipliers, and the guard counting radius.
- **Gang and Stakes**: gang size range, gold lost on defeat, the pay-off option and size, blunt weapons and hoods on or off, guards helping and their sight range, and security gained on victory.

Changes apply immediately. They're saved by MCM under `Documents\Mount and Blade II Bannerlord\Configs\ModSettings\StreetMuggings`.

## Testing switches
Two debug switches live at the bottom of `src/MuggingSettings.cs`. They're not in the mod menu.
- `AlwaysMug`: every eligible action triggers a mugging, day or night, ignoring chance, cooldown and minimum gold.
- `DebugMessages`: prints each roll, with town security, prosperity, gang relations, guard count and the final chance.

Set either to `true` and rebuild to test. Set them back to `false` before a release.

## How it works (files in `src/`)
- `Patches.cs`: Harmony prefixes on the Trade and town-walk menu consequences, plus `GameMenuOption.RunConsequence` for the tavern-district option.
- `MuggingManager.cs`: eligibility, the roll, opening the ambush scene, and cooldowns.
- `MuggingRules.cs`: the chance and difficulty formulas, and gang composition.
- `MuggingMissionLogic.cs`: moves you to the market or tavern door, spawns the thugs as SandBox location characters near you, offers the pay-or-fight choice, walks paid-off thugs away, then runs the fight through the game's `MissionFightHandler` (the same system as alley fights), calls in guards who see it, and handles the win and loss results.
- `ThugWeapons.cs`: swaps each thug's weapons for a tier-appropriate blunt weapon.
- `ThugOutfits.cs`: puts a hood on each thug.
- `StreetMuggingsMcmSettings.cs`: the MCM options page. `MuggingSettings.cs` reads from it.
- `SubModule.cs`: applies the patches and attaches the mission logic to the town-center scene.

## Support
If you enjoy the mod, you can support me on [Ko-fi](https://ko-fi.com/darkjeffrey).
