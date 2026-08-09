# RRACF — Replacer to ACF Slot Converter

A companion tool for **[ACF](https://github.com/peepoNuggie/ACF-Additive-Camo-Framework-MGS-UE4SS)**
for *Metal Gear Solid Delta: Snake Eater*. It converts a **replacer** camo mod into an **ACF slot**
mod, so the outfit gets its own place in the camo list instead of taking over one of the game's.

Point it at a mod, give it a name, pick a slot. It works out the rest.

> ### Download
>
> **[Releases](../../releases)** · **[Nexus Mods](https://www.nexusmods.com/metalgearsoliddeltasnakeeater/mods/236)**
>
> Two packages, same program:
>
> | | |
> |---|---|
> | **Bundled** | Everything included. Unzip and run. |
> | **Unbundled** | RRACF only — you supply `retoc` and `repak` yourself. ~90 KB. |
>
> The unbundled build exists because `retoc` and `repak` ship a compression library that some
> antivirus scanners flag on reputation. Nothing is wrong with them — they are unmodified official
> releases — but the smaller package lets you fetch them from source yourself.
>
> Take the **Releases** zips, not GitHub's automatic "Source code (zip)" — that is the project, not
> a runnable tool.

---

## Status

**v2.0** — writes ACF 2.0's config format.

| | |
|---|---|
| Slots | 5 (camo IDs 61–65) |
| Interface | window, plus a command line for scripting |
| Install | unzip and run — no .NET SDK, no setup |
| Mod patterns handled | both (see below) |
| Camo detection | automatic, read from the game itself |
| Tested against | 18 real replacer mods, all confirmed in game |

---

## Using it

Full instructions are in **[readme.txt](readme.txt)**, which ships with the download. It is written
for anyone converting a mod — you do not need to be a mod author, or know anything about how any of
this works. The short version:

1. Put the mod in the `Input` folder
2. **Analyse mod** — RRACF works out which camo it replaces
3. Type an in-game name, pick a slot
4. **Build ACF slot mod**

The result is one folder in `Output` that drops straight into `Content\Paks\mods`, containing the
generated slot files, an `ACF_Slot<n>.txt` you can edit in Notepad, and the original mod's own art.

Everything you type can be saved and reloaded from the **File** menu — saves live in
`Resources\Saves`. Folder paths are deliberately left out, so a save shared with someone else does
not point at folders they do not have.

There is also a command line — `RRACF.exe --help` — used for the batch testing behind the table
above.

### What goes in ACF_Slot&lt;n&gt;.txt

RRACF writes the whole file in ACF's own format, comments included, so it can be edited in Notepad
afterwards. Beyond the name it covers:

- **Four description lines** — `PlainDesc`, `AbilityDescOrange`, `WarningDesc`, `SpecialDesc`, each
  a different color in game. A blank one is omitted rather than written empty, and the legacy
  single `Description` key is never written.
- **All five abilities** — silent steps, steady aim, infinite suppressor, infinite ammo on every
  weapon (`INFAmmoFlag`), and infinite ammo restricted to chosen weapons or whole categories
  (`INFAmmoWeapon`). The last two are separate keys, so either one alone turns it on.
- **Concealment** — `BaseCamo`, plus the 25-surface × 5-stance grid the game's own camos use.
  They are alternatives: ACF **adds** them, so a grass value of 35 with `BaseCamo=30` gives 65.
  RRACF warns before building if both are set.

The camo-values drop-down offers the game's real grids, read out of the running game with ACF's
`camotable` command: **Tiger Stripe** (ID 1), **Squares** (ID 7), **Sneaking Suit** (ID 12) and
**Tuxedo** (ID 16).

One rule the generator follows strictly: **no trailing comments on value lines.** ACF only ignores
lines that *start* with `;` or `#`, so `INFAmmoFlag=0  ; set to 1` would read as enabled.

### Slot 5 (camo ID 65)

New in ACF 2.0, and different in two ways that RRACF surfaces on screen rather than letting you find
out later:

- **The name is capped at 15 characters.** A longer one is ignored outright, not truncated, and the
  row falls back to "ACF Mod 5".
- **Concealment values do not apply.** ACF reads them correctly and the game overrides them — slot 5
  conceals as Tiger Stripe regardless. Slots 1–4 are unaffected.

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

**Some mods cover more than one camo**, in either pattern. Zero's Jacket overwrites the art of both
`Tuxedo` (ID 16) and `Tuxedo_White` (ID 54); EVA's suit ships two camo assets of its own, for Choco
Chip (ID 4) and Rock (ID 46). RRACF lists every camo it finds and you pick which one becomes the
slot — one slot holds one outfit. To get both, convert the mod twice onto different slots; the art
is shared, so each output carries its own copy.

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

- **Slots 61–65 only.** ACF's limit, not this tool's.
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

`retoc` and `repak` are included under `Resources/`, unmodified from their upstream releases.

---

## Building from source

Run `build.bat`. It uses the C# compiler that ships with Windows
(`C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`) — no SDK or Visual Studio needed.

That produces `RRACF.exe`. To run it, the two tools it drives must be reachable — they are already
in this repository at `Resources\retoc\retoc.exe` and `Resources\repak\repak.exe`, and RRACF finds
them anywhere under its own folder.

Both are unmodified copies of the official upstream releases, each with its own LICENSE file:

- [retoc](https://github.com/trumank/retoc) by Truman Kilen and Archengius — converts assets between
  the game's Zen format and the legacy format. MIT.
- [repak](https://github.com/trumank/repak) by Truman Kilen and spuds — reads and writes `.pak`
  archives. MIT / Apache-2.0.

Each ships `oo2core_9_win64.dll`, the Oodle compression library Unreal Engine uses. The game
installs the same library; the tools need it to read the game's compressed archives. It is a common
false-positive trigger for antivirus scanners.

RRACF itself makes no network connections. It reads the game's `.pak`/`.utoc` files, runs `retoc`
and `repak` as child processes, and writes only into its own `Output` folder plus two small `.txt`
files beside itself. No installer, no registry writes, no admin rights.

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
