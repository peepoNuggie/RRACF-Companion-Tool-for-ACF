# RRACF — Replacer to ACF Slot Converter

A companion tool for **[ACF](https://github.com/peepoNuggie/ACF-Additive-Camo-Framework-MGS-UE4SS)**
for *Metal Gear Solid Delta: Snake Eater*. It converts a **replacer** camo mod into an **ACF slot**
mod, so the outfit gets its own place in the camo list instead of taking over one of the game's.

Point it at a mod, give it a name, pick a slot. It works out the rest.

> ### Downloads are on Nexus, not here
>
> **[Download RRACF on Nexus Mods](https://www.nexusmods.com/metalgearsoliddeltasnakeeater/mods)**
>
> This repository holds **source code only**. There are no ready-to-run files here, and GitHub's
> automatic "Source code (zip)" archives are the project, not the tool.
>
> Nexus carries the packaged build, with `retoc` and `repak` already included.

---

## Status

**v1.0** — feature complete.

| | |
|---|---|
| Slots | 4 (camo IDs 61–64) |
| Interface | window, plus a command line for scripting |
| Install | unzip and run — no .NET SDK, no setup |
| Mod patterns handled | both (see below) |
| Camo detection | automatic, read from the game itself |
| Tested against | 18 real replacer mods, all confirmed in game |

---

## Using it

Full instructions for mod authors are in **[readme.txt](readme.txt)**, which ships with the
download. The short version:

1. Put the mod in the `Input` folder
2. **Analyse mod** — RRACF works out which camo it replaces
3. Type an in-game name, pick a slot
4. **Build ACF slot mod**

The result is one folder in `Output` that drops straight into `Content\Paks\mods`, containing the
generated slot files, an `ACF_Slot<n>.txt` you can edit in Notepad, and the original mod's own art.

There is also a command line — `RRACF.exe --help` — used for the batch testing behind the table
above.

---

## How it works

An ACF slot is backed by one tiny asset, `Camouf_<slot>_asset`, that names the meshes and materials
a camo should use. A replacer mod already ships art at known paths, so a slot only needs an asset
pointing at them. RRACF builds that asset by renaming an existing one, then repacks it.

The interesting part is which asset to rename, because **"replacer camo mod" means two different
things**:

- **The mod ships its own `Camouf_<id>_asset`.** Most "X over Y camo" mods. The art goes in a new
  folder and a small pak repoints a vanilla camo at it. RRACF renames *that* asset onto the slot,
  and does not ship the mod's override — so the camo it used to take over is left alone.
- **The mod overwrites a vanilla camo's art in place.** No asset to borrow, so the vanilla
  `Camouf_<id>_asset` is pulled out of the game and renamed instead.

Both are detected automatically.

Three things that are not obvious, and cost real debugging:

- **The camo name → ID map cannot come from the `GM_CAMOUF_*` enum.** Its names disagree with the
  art folders for a third of the camos — ID 6 is folder `Rain_Drop` but enum `RAIN_STROKE`, 23 is
  `Snake`/`HEBI`, 54 is `Tuxedo_White`/`WHITE_TUXEDO`. RRACF reads the game's own assets instead.
- **A renamed name-table entry needs its two 16-bit hashes rewritten**, and the two use *different*
  CRC tables — so the obvious implementation matches one and not the other.
- **Renaming across a digit boundary resizes the header**, moving every absolute offset in the
  package including each export's `SerialOffset`. Getting that wrong packs and verifies perfectly
  cleanly and crashes the game on load.

Every build is verified before it is written: the package name is read back out of the finished
container, the chunk ID is compared against the untouched source (a match would mean it silently
overrides a vanilla camo), and the export offsets are checked against the header size.

The full write-up — including the dead ends and the evidence for each decision — is in
**[docs/HOW-IT-WORKS.md](docs/HOW-IT-WORKS.md)**.

---

## Known limitations

- **Slots 61–64 only.** ACF's limit, not this tool's.
- **Only camo mods can be converted.** Mods that replace Snake's body or head meshes apply to every
  camo at once, so there is nothing to confine to a slot. RRACF says so and lists what the mod
  actually replaces.
- **Add-ons need their base mod present.** Some downloads carry a camo definition and no art,
  borrowing it from a main mod — on Nexus, the small "Optional files". RRACF detects these and says
  which one is missing rather than building an empty slot.
- **No `Camouf_0_asset` exists** in the game (nor 52 or 53), so Olive Drab cannot be used as a
  template.
- **One mod at a time in `Input`.** Two unrelated mods would be merged into a single slot; RRACF
  warns, but does not stop you.
- **Slot collisions are ACF's problem, not this tool's.** Two mods on the same slot means one
  disappears silently — RRACF has no way to know what you already have installed.

---

## Requirements

- **[ACF](https://github.com/peepoNuggie/ACF-Additive-Camo-Framework-MGS-UE4SS)** installed, for the
  converted mods to have slots to fill
- A copy of the game, to read the vanilla camo assets from
- Windows. The tool runs on .NET Framework 4.x, already present on Windows 10 and 11

`retoc` and `repak` are bundled in the Nexus download and are **not** in this repository.

---

## Building from source

Run `build.bat`. It uses the C# compiler that ships with Windows
(`C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`) — no SDK or Visual Studio needed.

Put the two tools next to the built exe as `Resources\retoc\retoc.exe` and
`Resources\repak\repak.exe` (anywhere under the program's folder works):

- [retoc](https://github.com/trumank/retoc) — Zen ⇄ legacy asset conversion. MIT.
- [repak](https://github.com/trumank/repak) — `.pak` reading and writing. MIT / Apache-2.0.

---

## Validation

Output is byte-identical to hand-built ACF mods for `ACF_Ocelot62`, `ACF_Boss63` and `ACF_Sorrow64`.

All 18 replacer mods tested convert correctly and were confirmed in game, including six add-ons and
several that cross the digit boundary. `ACF_Zero63` is deliberately *not* byte-identical — see
[docs/HOW-IT-WORKS.md](docs/HOW-IT-WORKS.md#5-namesreferencedfromexportdatacount) for why.

---

## Support

Bug reports and pull requests are welcome here on GitHub.

Discord: **peepoNuggie**

---

## Credits

- Tooling: [retoc](https://github.com/trumank/retoc) by Truman Kilen and Archengius,
  [repak](https://github.com/trumank/repak) by Truman Kilen and spuds
- Built for [ACF](https://github.com/peepoNuggie/ACF-Additive-Camo-Framework-MGS-UE4SS)

RRACF is MIT — see [LICENSE](LICENSE). retoc and repak keep their own licences.
