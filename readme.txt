================================================================================
  RRACF - Replacer to ACF Slot Converter
  A companion tool for ACF (Additional Camouflage Framework)
  Metal Gear Solid Delta: Snake Eater
================================================================================


WHAT IT DOES
--------------------------------------------------------------------------------

Most camo mods are "replacers" - they take over one of the game's own camos, so
installing them means losing that camo.

RRACF converts a replacer into an ACF slot mod instead. The outfit gets its own
place in the camo list, and the vanilla camo it used to take over is left alone.

You do not need to understand any of it. Drop the mod in, press two buttons.


BEFORE YOU START
--------------------------------------------------------------------------------

You need ACF installed. Get it from the ACF page on Nexus Mods.

Keep this folder together. RRACF needs the Resources folder next to it.


HOW TO USE IT
--------------------------------------------------------------------------------

1. Put the mod you want to convert into the Input folder.

   Unzip it first. A folder inside Input is fine - RRACF will find it.
   Only put ONE mod in Input at a time.

2. Double-click RRACF.exe.

3. Press "1. Analyse mod".

   RRACF reads the mod and works out which camo it replaces. It fills in the
   "Replaces" box for you. You should not need to change it.

4. Type an In-game name.

   This is what players see in the camo list, and it names the files too.
   "Fox Suit" gives you a folder called ACF_FoxSuit61.

   A Description is optional. Base camo is optional - leave it blank.

5. Pick an ACF slot: 1 to 5.

   Use a slot that is not already taken by another mod you have installed.

   SLOT 5 IS DIFFERENT in two ways, and RRACF tells you on screen when you pick
   it. The name is capped at 15 characters - a longer one is ignored completely,
   not shortened. And its concealment values do not work: the game overrides them
   and slot 5 always hides you like Tiger Stripe. Everything else works normally.
   If concealment matters to your mod, use slots 1 to 4.

6. Fill in the three tabs if you want to. All of it is optional.

   Description   up to four lines, each a different colour in game
   Camouflage    how well the outfit hides you
   Abilities     silent steps, infinite ammo, and so on

7. Press "2. Build ACF slot mod".

That's it. Your converted mod is in the Output folder.


INSTALLING WHAT IT MADE
--------------------------------------------------------------------------------

Open the Output folder. Inside is a folder named after your mod, for example:

    Output\ACF_FoxSuit61\

Copy that WHOLE FOLDER into:

    ...\MGSDelta\Content\Paks\mods\

Everything in it is needed, including the original mod's files. RRACF puts them
there on purpose - they are the actual clothing.

Start the game and the outfit will be in the camo list under the name you gave
it.


IF SOMETHING GOES WRONG
--------------------------------------------------------------------------------

RRACF tells you when something is not right, and the message explains what to
do. The usual ones:


"This looks like an ADD-ON rather than a complete mod"

    Some downloads are add-ons. They contain the outfit's settings but none of
    the actual clothing, and they borrow it from a main mod. On Nexus these are
    usually small files listed under "Optional files".

    Put the main mod in the Input folder as well, then press Analyse again.
    Both together is fine - RRACF sorts out which parts to use.


"This mod does not replace any camouflage art"

    The mod changes something other than a camo - Snake's body or face, for
    instance. Those apply to every camo at once, so they cannot be given their
    own slot. Nothing to be done, sorry.


"RRACF could not find retoc.exe or repak.exe"

    The Resources folder has gone missing or was not unzipped with the rest.
    Re-download and keep the folder together.


Nothing was found in the Input folder

    Check the mod is unzipped and that the .pak, .ucas and .utoc files are in
    there somewhere.


THE ACF_Slot txt FILE
--------------------------------------------------------------------------------

Each converted mod comes with a small text file, for example ACF_Slot61.txt.
You can open it in Notepad and edit it any time - no need to convert again.

    Name          what players see in the camo list
    PlainDesc     the blurb underneath it
    AbilityDesc.. three more description lines, in orange, red and yellow
    BaseCamo      how well the outfit hides you, everywhere
    Camo<Surface> how well it hides you on one surface, per stance
    INFAmmoFlag   and the other ability switches

Everything you set in the window is written here, so you can tweak it in
Notepad afterwards rather than converting again.

ONE RULE if you edit it: never put a comment after a value on the same line.
Only lines that START with ; or # are ignored, so this switches the ability ON:

    INFAmmoFlag=0    ; set to 1 to enable

Put explanations on their own line above the key instead.


SAVING YOUR WORK
--------------------------------------------------------------------------------

File > Save slot settings stores everything you typed - name, descriptions,
camo values and abilities - so you can come back to it later. File > Load
brings it back. Saves live in Resources\Saves, and File > Open saves folder
takes you there.

Folder paths are deliberately not saved, so a save file you share with someone
else will not point at folders they do not have.


ABILITIES
--------------------------------------------------------------------------------

    Steady aim            no shaking while aiming (first person only)
    Silent steps          footsteps make no noise
    Infinite suppressor   durability never drops
    Infinite ammo         every weapon costs no ammo

You can instead pick individual weapons or whole categories in the list below
those boxes. The two are independent - either one alone turns infinite ammo on.

Watch out for one thing: "Grenades" is the whole category (frag, WP, stun,
smoke and chaff) while "Grenade" on its own is just the frag grenade. They are
separate entries in the list.


HOW WELL SHOULD IT HIDE YOU?
--------------------------------------------------------------------------------

There are two ways, and you should pick ONE.


The simple way - Base camo

    One number that applies everywhere. 0 behaves like being naked, positive
    hides you better, negative makes you easier to spot. -100 to 100.

    Good enough if you just want "hides a bit better than bare skin".


The realistic way - the Camo values grid

    This is how the game's own camos actually work. Every camo has a value for
    each surface AND each stance, which is why Water works underwater and going
    prone in grass helps.

    Fill in the grid in the window. Each row is a surface, each column a stance.
    With Base camo left at 0, the numbers you type ARE the percentages the game
    shows.

    Use the drop-down above the grid for a starting point. "Tiger Stripe" is the
    game's real values, so you can see the shape of a proper camo. The others are
    suggestions to build on, not values taken from the game.

    A good camo is BAD somewhere. That is what makes choosing one interesting.


Do not use both. ACF adds them together, so Base camo 30 with a grass value of
35 gives 65 in grass. RRACF will warn you if you set both.


A NOTE ON SHARING CONVERTED MODS
--------------------------------------------------------------------------------

The converted folder contains the original mod's files. If you want to upload
your conversion anywhere, ask the original mod author first.


CREDITS
--------------------------------------------------------------------------------

RRACF uses two excellent tools, included in the Resources folder under their
own licences:

    retoc   by Truman Kilen and Archengius
    repak   by Truman Kilen and spuds

RRACF on Nexus:  https://www.nexusmods.com/metalgearsoliddeltasnakeeater/mods/236
ACF on Nexus:    https://www.nexusmods.com/metalgearsoliddeltasnakeeater/mods/235

Source code: https://github.com/peepoNuggie/RRACF-Companion-Tool-for-ACF
Downloads are on Nexus Mods only.
