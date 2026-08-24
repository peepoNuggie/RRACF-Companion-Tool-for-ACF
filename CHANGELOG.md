# Changelog

## v2.0.1

A maintenance release. No new features — RRACF just finds your game in places it used to give up
on, and says something useful when it can't.

### Finding the game

RRACF used to look in exactly four Steam-shaped folders and nowhere else. If your copy was anywhere
other than those, it found nothing and left you to type the path in by hand.

It now tries three things in order and stops at the first that works:

1. The same four Steam locations as before
2. Steam's own record of where its libraries are
3. A look for the game's own folder on your drives

🏴‍☠️ **Fixed issues for those sailing the high seas** 🏴‍☠️ — step 3 looks for the folder the *game*
creates, not the one whoever packaged your copy chose to put it in. That name comes from the game's
own build, so it is identical on every copy in existence. Wherever your install ended up, and
however it got there, it gets found.

External drives are searched too. Network drives are not — they are slow enough to spend the whole
search getting nowhere.

A normal Steam copy is still answered by step 1, instantly, exactly as before.

### Errors that name the actual problem

Pointing RRACF at `Content\Paks\mods` — the folder a *finished* mod gets installed into — used to
get all the way to the build before failing with:

> The game does not contain Camouf_11_asset. Camo ID 11 may not exist.

Camo 11 was fine. The folder simply had no game data in it, and there was no way to tell that from
the message.

That is now caught before anything runs, and the error names the folder to use instead:

> This is not the game's Paks folder — there is no global.utoc in it:
> `...\MGSDelta\Content\Paks\mods`
>
> It looks like you have picked the mods folder inside it. That is where a finished mod gets
> installed — the game's own files are in the folder above:
> `...\MGSDelta\Content\Paks`

### Also

- The command-line help now offers slots **61–65**. Slot 65 has worked since 2.0; the help just
  never mentioned it.
- RRACF opens slightly faster. It was working out where the game was on every single launch, even
  when it already had the answer saved.

---

Earlier releases are on the [Releases page](../../releases).
