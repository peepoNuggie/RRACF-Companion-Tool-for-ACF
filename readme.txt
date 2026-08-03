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

5. Pick an ACF slot: 1, 2, 3 or 4.

   Use a slot that is not already taken by another mod you have installed.

6. Press "2. Build ACF slot mod".

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
    Description   the blurb underneath it
    BaseCamo      how well the outfit hides you

BaseCamo is a concealment value, not a camo number. 0 is the same as being
naked, Olive Drab is 10, Tiger Stripe is 30, and Gold is -100. Positive hides
you better, negative makes you easier to spot. Anything from -100 to 100 is
sensible. Leave it at 0 if you are not sure.


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
