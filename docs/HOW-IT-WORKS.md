# How RRACF works, and what it cannot do

Notes from working the conversion out by hand and then automating it. The short version lives in
the [README](../README.md); this is the reasoning, the things that turned out to be wrong, and the
evidence for each.

---

## 1. Why an ACF slot can be built at all

ACF adds camo slots 61–64. A slot is backed by one tiny asset,
`/Game/Maps/AssetCamouflage/Camouf_<slot>_asset`, which does nothing but name the meshes and
materials that camo should use.

A replacer mod already ships art at some known paths. So a slot only needs a `Camouf_<slot>_asset`
pointing at those same paths — the replacer supplies the art, and the slot supplies the entry in
the camo list. Nothing has to be rebuilt or re-textured.

That is the whole trick. Everything below is detail about getting the bytes right.

---

## 2. The two shapes of replacer mod

This was the biggest thing missed on the first pass. "Replacer camo mod" is not one thing.

### 2a. The mod ships its own `Camouf_<id>_asset`

Most "X over Y camo" mods on Nexus. Two paks:

```
pakchunk188-BigBoss_P     10 packages under Camouflage/Sna_Suit/   <- the art, in a NEW folder
Big_Boss_Suit_P            1 package: /Game/Maps/AssetCamouflage/Camouf_2_asset
```

The art does **not** overwrite anything. The second pak replaces Leaf's camo asset so that Leaf
points at `Sna_Suit` instead of `Leaf`.

Converting this is the easy case: the mod's own `Camouf_2_asset` already references the right art,
so RRACF renames **that** onto the slot. It also deliberately does **not** ship the mod's override
pak — keeping it would leave Leaf hijacked as well as filling the slot, which defeats the point.

### 2b. The mod overwrites a vanilla camo's art in place

Zero's Jacket Replacer. It replaces the files under `Camouflage/Tuxedo/` directly; there is no camo
asset in the mod. Here RRACF pulls the **vanilla** `Camouf_16_asset` out of the game and renames
that. It points at `Camouflage/Tuxedo/...` as it always did, and the mod has changed what lives
there.

All files from the mod ship in this case, since none of them is an override to drop.

---

## 3. Mapping a camo name to its ID

The obvious approach — read `GM_CAMOUF_*` out of `MGS3_enums.hpp` and match on name — **does not
work**. The enum names and the art folder names disagree far too often:

| ID | art folder | enum name |
|----|------------|-----------|
| 6  | `Rain_Drop` | `RAIN_STROKE` |
| 23 | `Snake` | `HEBI` |
| 24 | `Ga_Ko` | `GARCO` |
| 29 | `Animals` | `ANIMAL` |
| 35 | `ST_V` | `VALEN` |
| 36 | `Egermny` | `EASTG` |
| 37 | `Wgermny` | `WESTG` |
| 54 | `Tuxedo_White` | `WHITE_TUXEDO` |
| 60 | `Gavs_Suit` | `ADDITIONAL_UNIFORM_1` |

Instead RRACF extracts every vanilla `Camouf_*_asset` once (58 of them, about a second) and reads
which `Camouflage/<Folder>/` each one references. That is derived from the game itself, so it
cannot drift. It is cached in `rracf-camomap.txt`; **Rebuild camo list** regenerates it after a
game update. The enum is still read, but only to put a friendly label next to each ID.

Some camos share a folder — IDs 11, 57, 58 and 59 all reference `Naked` — so ties break toward the
camo referencing the fewest folders. `Naked` resolves to 11 (plain Naked), while `Naked_Woodland`
only ever matches 57.

**IDs 0, 52 and 53 have no asset in the game at all.** There is no `Camouf_0_asset`, so Olive Drab
/ Normal cannot be used as a template. It is still valid as a `BaseCamo=` value, which is just a
number ACF reads.

---

## 4. Renaming the asset

`Camouf_<a>_asset` appears in three places inside the `.uasset`:

1. the package name in the summary,
2. a name-table entry for the short name,
3. a name-table entry for the full package path.

The `.uexp` does not contain it. All three must change.

### Renaming the files on disk is not enough

Worth stating because it looks like it should work. Rename only the files and run `to-zen`, and the
manifest reads back:

```
packagename : /Game/Maps/AssetCamouflage/Camouf_5_asset      <- the embedded name
filename    : .../Camouf_61_asset.uasset                     <- the file name
```

`to-zen` uses the **embedded** package name. A pak built that way keeps the vanilla camo's chunk ID
and silently overrides that camo instead of filling a slot, with no error from any tool. This is
the exact failure the chunk-ID check exists to catch.

### Renamed name-table entries need their hash rewritten

Every string in the name table is followed by two 16-bit hashes. Vanilla assets store **zero** for
all of them, but a renamed entry has to carry a real one.

Both algorithms were identified empirically, by testing candidates against known-good values from a
hand-built asset (`Camouf_63_asset` → `0xEE79`, `0xAFDD`; the full path → `0xFB2A`, `0x1109`):

- `CasePreservingHash` = `FCrc::StrCrc32` — standard reflected CRC-32 (poly `0xEDB88320`), four
  table rounds per UTF-16 character.
- `NonCasePreservingHash` = `FCrc::Strihash_DEPRECATED` — uppercased, **one** round per character,
  against Unreal's **legacy MSB-first** table (poly `0x04C11DB7`), starting from 0.

The second one is the trap: it uses a different table from the first, so the obvious implementation
matches one hash and not the other. RRACF rehashes only the entries it changes, leaving every other
entry's zero intact — which is what the vanilla file has.

### Crossing a digit boundary resizes the header

Leaf (ID 2) to slot 61 makes the name one byte longer in each of the three places, so the file grows
by three bytes and everything after each insertion point moves.

RRACF walks the whole package summary and shifts every absolute offset: name table, soft object
paths, gatherable text, exports, imports, depends, soft package references, searchable names,
thumbnails, asset registry, bulk data start, world tile info, preload dependencies, payload TOC,
data resources — plus the `SerialOffset` of every export (at `exportEntry + 36`, an int64).

The walk is self-checking. The summary is immediately followed by the name table, so if the cursor
does not land exactly on `NameOffset`, the layout is not the one the tool understands and it
refuses to touch the file rather than produce something subtly corrupt. That check passes on all 58
vanilla camo assets.

---

## 5. `NamesReferencedFromExportDataCount`

This field sits 16 bytes before the name table, behind an int64 `-1` marker that RRACF verifies
before trusting the position.

**An earlier version of this tool set it to the total name count. That was wrong.**

The field counts the names at the *start* of the table that the export data refers to. A rename does
not change the export data, so the count does not change either. The proof: take a known-good,
shipped, working mod pak, run it through `to-legacy` and back to `to-zen` with **no edits at all**,
and compare.

| | `.ucas` |
|---|---|
| mod's original pak | 2224 |
| round-tripped, field preserved | 2224 |
| round-tripped, field forced to name count | 3667 |

Preserving it is byte-for-byte faithful. Forcing it inflates the package by ~65% with names nothing
reads. RRACF now leaves the field alone.

The reason the mistake survived so long: it was inferred from a single hand-built reference whose
author had rewritten the field, probably via a GUI asset editor that recomputes it on save.

---

## 6. Reading a mod's own asset needs the whole mod present

`to-legacy` cannot read a mod container alone — it needs the engine's script objects from the
game's `global.utoc`:

```
Error: FIoChunkId { ... ScriptObjects } not found in any containers
```

So RRACF stages a folder with `global.utoc`/`global.ucas` plus the mod's paks and points `to-legacy`
at that.

**All** the mod's paks have to be there, not just the one holding the camo asset. That asset's
imports point into the mod's *art* paks, and anything retoc cannot resolve it writes out as
`/Engine/UnknownPackage` / `UnknownExport`. The build then succeeds, verifies, and produces a slot
with no link to any art — an empty camo in game, with nothing anywhere reporting a problem.

The tell is size: with imports lost, the Ocelot slot came out at 1401 bytes against the hand-built
2224.

### Some mods have no art of their own

A few downloads ship *only* a camo asset and take their art from a companion download — usually the
base mod on the same Nexus page. "The Boss' Mantle over Black Camo" is one: a single 1851-byte pak
whose only package is `Camouf_9_asset`, and whose imports resolve to nothing on their own.

Converted alone, that produces a slot referencing no art whatsoever. It packs, verifies and installs
without complaint, and is an empty camo in game. So after extracting the template RRACF counts the
`/Body/Camouflage/<folder>/` references in it, and refuses the build if there are none, pointing at
the likely cause. Putting the companion download in the Input folder alongside it fixes the
resolution — the Mantle then finds 7 art references and builds.

Note that a nonzero `/Engine/UnknownPackage` count is *not* on its own a failure: several mods that
match their hand-built references byte for byte still carry one unresolved import, because the
staged folder holds only `global.utoc` and the mod, not the whole game. What matters is whether any
camouflage art survived.

---

## 7. Verification

Two checks run on every build, both against the finished container rather than intermediate files.

**Package name readback.** `retoc manifest` on the produced `.utoc` must report
`/Game/Maps/AssetCamouflage/Camouf_<slot>_asset`. The patcher also refuses if any trace of the old
name survives anywhere in the asset.

**Chunk ID comparison.** RRACF packs the *untouched* source asset through the same pipeline and
compares chunk IDs. A chunk ID is derived from the package name, so if the rename had not really
taken, the two would match and the pak would override the vanilla camo instead of filling a slot.
The build fails if they are equal.

**Art reference count.** The template must still name at least one `/Body/Camouflage/<folder>/`
path, or the slot would be empty in game (section 6).

---

## 8. What does not work

- **Mods that do not replace camouflage art.** An ACF slot points at one camo's art under
  `.../Snake_HD/Body/Camouflage/<CamoName>/`. Mods replacing Snake's base body or head meshes
  (`Snake_HD/Mesh/Standard01/sna_def_*`, e.g. Snake's Under Armor) apply to every camo at once,
  so there is nothing to confine to a slot. RRACF says so and lists what the mod does replace.
- **Slots 65 and above.** ACF does not support them yet. `Pipeline.ValidSlots` is the one line to
  change when it does.
- **Camo IDs 0, 52, 53 as a template.** No asset exists in the game.
- **Assets with custom version entries, package-level compression, or UTF-16 name entries.** None
  of the camo assets have these; RRACF refuses rather than guess at the layout.

---

## 9. Validation

Byte-identical (`.pak`, `.ucas`, `.utoc`) to hand-built ACF mods:

| built by RRACF | from | pattern |
|---|---|---|
| `ACF_Ocelot62` | Ocelot's Uniform over Animal (29) | mod-supplied asset |
| `ACF_Boss63` | The Boss' Sneaking Suit over Snake (23) | mod-supplied asset |
| `ACF_Sorrow64` | The Sorrow's Uniform over Spirit (21) | mod-supplied asset |

**`ACF_Zero63` is deliberately not byte-identical.** RRACF produces a 1553-byte `.ucas` where the
hand-built copy is 2535. The whole difference is `NamesReferencedFromExportDataCount`, which that
copy had rewritten (section 5). The three references that agree with RRACF are all mods that have
been shipped and played. The extra names in the larger file are unread padding, so both should
work — but the smaller output for the overwrite pattern (2b) **has not been confirmed in game yet**,
and that is the one claim here resting on reasoning rather than measurement.

Of 13 real replacer mods tested end to end, 11 convert and were confirmed by round-tripping the
finished slot back to legacy and checking the package name and the art folder it points at:

| mod | camo | art folder | refs |
|---|---|---|---|
| Big Boss' suit over Leaf | 2 | `Sna_Suit` | 8 |
| DUKE's jumpsuit over KLMK | 40 | `Duke` | 3 |
| EVA's suit and jacket | 4 | `EVA` | 7 |
| Ocelot's Uniform over Animal | 29 | `Ocelot_Uniform` | 9 |
| Para-Medic's Jacket over DPM | 26 | `Med_Jacket` | 3 |
| The Boss' Sneaking Suit over Snake | 23 | `Boss_Sneaking_Suit` | 5 |
| The End's Fatigues | 19 | `End_Fatigues` | 4 |
| The Fear's Fatigues | 18 | `Fear_Fatigues` | 5 |
| The Fury's Suit over Fire | 20 | `Fury_Suit` | 9 |
| The Pain's Fatigues | 17 | `Pain_Fatigues` | 5 |
| The Sorrow's Uniform over Spirit | 21 | `Sorrow_Uniform` | 9 |

The other two — both variants of The Boss' Mantle — are correctly *refused*, because they carry no
art of their own (section 6). With their companion download in the Input folder they build fine.

Camo IDs 2, 4 and 9 cross the digit boundary, exercising the header resize.
