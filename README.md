# RRACF — Replacer to ACF Slot Converter

A companion tool for **ACF** (Additional Camouflage Framework) for *Metal Gear Solid Delta: Snake
Eater*. It turns a **replacer** camo mod into an **ACF slot** mod, so the mod fills one of ACF's
extra camo slots instead of overwriting a vanilla camo.

Download it from Nexus. This repository is the source.

Double-click `RRACF.exe` for the window, or run it from a terminal for the command line.

## How to use it (window)

1. Drop the replacer mod into the **Input** folder. Subfolders are fine — it searches
   recursively for the `.utoc`, so an unzipped Nexus download can go straight in.
2. Press **1. Analyse mod**. It reads the mod, works out which vanilla camo it replaces,
   and fills in the **Replaces** and **Base camo** boxes.
3. Pick an **ACF slot** and type an **in-game name**. That name drives everything: the folder,
   the file names, and the `Name=` line in the `.txt`.
4. Press **2. Build ACF slot mod**.

You get one self-contained folder, named from the in-game name with spaces stripped —
"Fox Suit" on slot 61 becomes:

```
Output\ACF_FoxSuit61\
    ACF_FoxSuit61_P.pak
    ACF_FoxSuit61_P.ucas
    ACF_FoxSuit61_P.utoc
    ACF_Slot61.txt
    FoxSuitMedalion_P.pak     <- copied from the replacer mod
    FoxSuitMedalion_P.ucas
    FoxSuitMedalion_P.utoc
```

Drop that whole folder into `MGSDelta\Content\Paks\mods`. The replacer supplies the actual art;
the ACF files just point the slot at it, which is why both have to ship together.

### Replaces vs Base camo

These look similar but do different jobs:

- **Replaces** is the vanilla camo whose asset is used as the *template*. It must be the camo the
  mod actually replaces — get it wrong and the slot points at art that nothing supplies, so it
  shows up empty. Analyse fills this in; only change it if the detection picked wrong.
- **Base camo** is unrelated to that. It is the `BaseCamo=` line in `ACF_Slot<slot>.txt`, and it is
  a **camouflage index, not a camo ID** — ACF adds it to the concealment the game calculates, so the
  slot simply hides you better or worse than bare skin. For scale: Naked is 0, Olive Drab 10, Tiger
  Stripe 30, Gold −100. Sensible range is −100 to +100, and ACF stores it as a signed byte, so
  −128..127. Leave the box blank for 0, which is what ACF defaults to. It affects stealth only,
  never appearance, and the `.txt` is plain text you can edit afterwards.

## Command line

Same tool. With no arguments it opens the window; with arguments it runs on the command line.

```
RRACF.exe [--mod <folder>] [--slot 61|62|63|64] [options]

  --mod <path>      folder holding the replacer mod (or a .utoc); defaults to Input
  --slot <61-64>    ACF slot to fill; omit to just inspect the mod
  --source <id>     override the detected camo used as the template
  --base <n>        BaseCamo= concealment value, -128..127 (default 0)
  --name <text>     fallback name if --display is not given
  --display <text>  in-game name; drives the folder and file names
  --desc <text>     in-game description
  --paks <dir>      the game's Content\Paks folder
  --out <dir>       output folder
  --rebuild-map 1   re-derive the camo list from the game
```

## Folders

`Input` and `Output` are created next to `RRACF.exe` on first run. The game's `Content\Paks`
folder is auto-detected by checking the usual Steam library locations on every drive. All three
paths can be changed with the Browse buttons, and whatever you pick is remembered in
`rracf-settings.txt`.

## What it actually does

1. `retoc manifest` on every `.utoc` in the Input folder, to find which `camouflage/<Name>` art
   folder is replaced.
2. Looks `<Name>` up to get the vanilla camo ID.
3. `retoc to-legacy` to pull that vanilla `Camouf_<id>_asset` out of the game.
4. Renames it to `Camouf_<slot>_asset` — on disk, and in three places inside the `.uasset`
   (two name-table entries plus the package name in the header).
5. `repak pack`, then `retoc to-zen`.
6. Verifies the result, writes `ACF_Slot<slot>.txt`, and copies the replacer's own
   `.pak`/`.ucas`/`.utoc` in alongside it.

For the full reasoning — the dead ends, the asset internals, and the evidence behind each decision —
see [docs/HOW-IT-WORKS.md](docs/HOW-IT-WORKS.md).

### The two kinds of replacer mod

RRACF handles both, and picks automatically:

- **The mod ships its own `Camouf_<id>_asset`.** Most "X over Y camo" mods work this way: the art
  goes in a new folder such as `Camouflage/Sna_Suit/`, and a tiny pak replaces a vanilla camo's
  asset to point at it. RRACF renames *that* asset onto the slot, and deliberately does **not** ship
  the mod's own override pak — so the vanilla camo it used to sit on is left untouched.
- **The mod overwrites a vanilla camo's art in place.** Zero's Jacket works this way: it replaces
  the files under `Camouflage/Tuxedo/`. Here there is no asset to borrow, so RRACF pulls the vanilla
  `Camouf_<id>_asset` out of the game and renames that.

Reading the first kind needs the game's `global.utoc` **and** all of the mod's own paks staged
together. Anything retoc cannot resolve is written out as `/Engine/UnknownPackage`, which silently
strips the slot's link to the art — it builds cleanly and shows up empty in game.

A few mods ship a camo asset with **no art of their own**, taking it from a base mod on the same
Nexus page — on Nexus these are usually small files under "Optional files". Analyse spots them and
says which is missing, and the build refuses rather than produce an empty slot. Put the base mod in
the Input folder alongside the add-on and both convert normally.

### Details that matter

**The camo list is derived from the game, not from the enum.** The `GM_CAMOUF_*` names in
`MGS3_enums.hpp` often do not match the art folder names — ID 6 is folder `Rain_Drop` but enum
`RAIN_STROKE`, 23 is `Snake` but `HEBI`, 24 is `Ga_Ko` but `GARCO`, 35 is `ST_V` but `VALEN`,
54 is `Tuxedo_White` but `WHITE_TUXEDO`. So RRACF reads every vanilla `Camouf_<id>_asset` once
and records which art folders each one points at. That cache is `rracf-camomap.txt`; press
**Rebuild camo list** after a game update. The enum is still used, but only for friendly labels.

**Renamed name-table entries need their hash rewritten.** Every string in the `.uasset` name
table is followed by two 16-bit hashes. The vanilla asset stores zero for all of them, but a
renamed entry must carry a real hash. RRACF recomputes both — `Strihash_DEPRECATED`
(uppercased, Unreal's legacy CRC table) and `StrCrc32` (standard CRC-32) — for the entries it
touches, and leaves the untouched ones alone.

**`NamesReferencedFromExportDataCount` must be left exactly as found.** This field counts the names
at the start of the table that the export data refers to. A rename does not change the export data,
so the count does not change either. Round-tripping a known-good mod pak through `to-legacy` and
back reproduces it byte for byte only when the field is preserved — overwriting it with the total
name count inflates the `.ucas` by roughly 65% with names nothing reads. RRACF locates the field
structurally (16 bytes before the name table, behind a `-1` marker it checks first) so it can be
read and reported, but it is never rewritten.

**Renaming across a digit boundary resizes the header.** Going from a single-digit camo such as
Leaf (ID 2) to slot 61 makes the name one byte longer in each of the three places it appears. That
moves everything after it, so RRACF walks the whole package summary and shifts every absolute
offset — name table, imports, exports, asset registry, bulk data, preload dependencies — plus the
`SerialOffset` of each export. The walk is self-checking: the summary is immediately followed by
the name table, so if the cursor does not land exactly on `NameOffset` the layout is not what the
tool expects and it refuses to touch the file.

### Verification

Before it finishes, RRACF packs the *untouched* vanilla asset the same way and compares chunk
IDs. If the package name had not really changed, the two IDs would match and the mod would
silently override the vanilla camo instead of filling a slot. The build fails if that happens,
and also if any trace of the old name is left anywhere in the asset.

## Limits

- **Only mods that replace camouflage art can be converted.** An ACF slot works by pointing at one
  camo's own art under `.../Snake_HD/Body/Camouflage/<CamoName>/`. Mods that replace Snake's base
  body or head meshes instead (anything under `Snake_HD/Mesh/...`) apply to every camo at once, so
  there is nothing to confine to a single slot. RRACF says so and lists what the mod does replace.
- **Slots 61–64 only.** ACF does not support 65+ yet. Widen `Pipeline.ValidSlots` when it does.
- **There is no `Camouf_0_asset`** (nor 52 or 53) in the game, so Olive Drab / Normal cannot be used
  as a template. It is still fine as a `BaseCamo=` value, since that is just a number ACF reads.

## Building from source

Run `build.bat`. It uses the C# compiler that ships with Windows
(`C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`) — no SDK or Visual Studio needed.
The result runs on .NET Framework 4.x, which is already on every Windows 10/11 machine.

RRACF drives two external tools that are **not** in this repository. Put them next to `RRACF.exe`
as `retoc\retoc.exe` and `repak\repak.exe` — the Nexus download already includes them:

- [retoc](https://github.com/trumank/retoc) — Zen ⇄ legacy asset conversion, by Truman Kilen and
  Archengius. MIT.
- [repak](https://github.com/trumank/repak) — `.pak` reading and writing, by Truman Kilen and spuds.
  MIT / Apache-2.0.

Both are MIT-licensed and redistributable as long as their `LICENSE` files ship with them, which is
why each lives in its own folder with its licence intact.

## Licence

RRACF is MIT — see [LICENSE](LICENSE). retoc and repak keep their own licences.

## Validation

Checked against four hand-built ACF mods, and against 13 real replacer mods end to end.

Byte-identical (`.pak`, `.ucas` and `.utoc`) to the hand-built:

- `ACF_Ocelot62` — Ocelot's Uniform over Animal (ID 29)
- `ACF_Boss63` — The Boss' Sneaking Suit over Snake (ID 23)
- `ACF_Sorrow64` — The Sorrow's Uniform over Spirit (ID 21)

The fourth, `ACF_Zero63`, is **not** byte-identical: RRACF produces a 1553-byte `.ucas` where the
hand-built one is 2535. The difference is `NamesReferencedFromExportDataCount`, which the hand-built
copy had rewritten to the total name count. Preserving it is the faithful behaviour — see above —
and the three references that agree with RRACF are all mods that have been shipped and played. The
extra names in the larger file are unread padding, so both should work, but the smaller output for
this mod pattern has not yet been confirmed in game.

All 13 mods in `Downloads\Testing` convert without error, four of them across the digit boundary
(camo 2, 4 and 9 → slot 61) exercising the header resize.
