# Audio sources and their licences: research (#301)

Parent: #293 (planning map). Question: which sounds can the game commit to the public repo and ship in the public WebGL build, and how to keep a local-only set out of both.

**Scope note.** No audio files were downloaded. Every licence claim below was read from the licence text or the asset/product page (URL given). Items marked **(snippet)** come only from a search-result summary and were not opened. Items marked **(derived)** are my inference from the cited text.

**Not legal advice.** This is a licence reading for a hobby project. Re-check the live page for any asset before it ships.

## Answers in brief

1. **Diablo II rips: no.** Blizzard's EULA claims "all sounds, musical compositions, recordings, and sound effects originating in the Platform" and bars copying except as allowed. Its fan-content terms allow personal, non-commercial use only. Rips must not be committed to the public repo or shipped in the public WebGL build. Local personal testing is the only defensible use, and only through the local-only path in section 3.
2. **Kenney packs are CC0 and cover most of the list.** UI: Interface Sounds (100 files) and UI Audio (50). Hits: Impact Sounds (130). Item drops by material: RPG Audio (50) and its OGA twin "50 RPG sound effects" (50, tags metal, cloth, book, coin, knife). Level-up: Music Jingles (85), contents not verified. Attribution is optional.
3. **Music loops are CC0 on OpenGameArt.** Town: "Town Theme RPG" (CC0, MP3 only, no loop stated). Field: "Overworld (BGM)" (CC0, loops, MP3), "Exploration Theme" (CC0, loops, WAV/OGG/FLAC). Combat: "Battle Theme A" (CC0, MP3, loop not stated). Menu: none verified; reuse Exploration Theme or audition more.
4. **Freesound: filter by licence per sound.** The filter offers "CC-BY or CC0" together, so each sound's licence must be checked. Avoid CC-BY-NC for a game.
5. **Sonniss GDC bundles: do not commit the raw files.** The EULA forbids redistributing the sounds "on their own or inside a sound library, sample pack, asset pack or anything similar". Shipping them inside a finished game is allowed. A public repo or WebGL build exposes the raw files, so this is a no.
6. **Local-only sounds:** keep them in an `Assets/` subfolder that no scene, prefab or `Resources` folder references, add the folder and its `.meta` to `.gitignore`, and load them through an Editor-only path. Anything in `StreamingAssets` or `Resources`, or in an Addressables group that is built, ships in the player.
7. **Missing files:** the game must treat every sound as optional. A missing clip is a silent no-op with one logged warning per key, never an exception or a broken scene.

Recommendation: build the first pass from Kenney (UI, impact, RPG) and OGA CC0 loops. No local-only set is needed for that. Use no Blizzard audio in the repo or the build.

## 1. Diablo II sound rips

### What the sources say

**Blizzard End User License Agreement** (LAST REVISED March 21, 2024, per the page). <https://www.blizzard.com/en-us/legal/fba4d00f-c7e4-4883-b8b9-1b4500a402ea/blizzard-end-user-license-agreement>

- Section 2, "Blizzard's Ownership": "All sounds, musical compositions, recordings, and sound effects originating in the Platform;" (quoted, the list also includes artwork and audio-visual effects).
- Section 1.C, "License Limitations": "Copy or reproduce (except as provided in Section 1.B.)" and "create derivative works based on or related to the Platform."
- Section 2, item 9: "you agree that you will not create any work based on the Platform".

Caveat: the EULA defines its scope by the "Platform" (Battle.net). I did not find a Diablo II-specific clause on sounds, and I did not open a Diablo II Resurrected EULA. The conclusion below does not depend on that gap, because the copyright in the sounds is Blizzard's either way.

**Blizzard Legal FAQ** <https://www.blizzard.com/en-us/legal/c1ae32ac-7ff9-4ac3-a03b-fc04b8697010/blizzard-legal-faq>

- On using game sounds: "Use of our music, sound samples, or characters from our game in your original compositions is limited" and "for your own personal use and creative exploration."
- On selling: "You may not sell or distribute for commercial purpose any music containing samples of music, sound or characters."
- On downloaded content: "for home, noncommercial and personal use only."

**Blizzard Video Policy** <https://www.blizzard.com/en-us/legal/dd76b654-f2c4-4aaa-ba49-ca3122de2376/blizzard-video-policy>

- The policy lists "music, sounds, speech, or other assets from Blizzard's copyrighted products" as covered.
- "Blizzard Entertainment requires that the use of Blizzard Content must be limited to non-commercial purposes."

### Conclusion (plain)

- **Diablo II rips may not be committed to the public repo.** The repo is public on GitHub, so committing them is redistribution. The EULA bars copying and distributing without permission, and the FAQ restricts sounds to personal use.
- **Diablo II rips may not be shipped in the public WebGL build** on GitHub Pages (see `docs/agents/github-pages.md`). The build is a public download, and the FAQ's personal-use allowance does not cover distributing a game.
- **Local personal testing** of a game build with rips is the only use that is not clearly prohibited. It must stay outside git and outside any build that is published. Section 3 gives the mechanism. It is still a personal-use grey zone, so the team should not treat it as a licence.
- **Diablo II-style item-drop sounds** can be built from CC0 sources instead (section 2). The per-kind idea is about mapping sound categories to item kinds, not about copying the original files.

## 2. Free alternatives

### 2a. Kenney (CC0)

Kenney's support page: "Yes, all game assets on the asset pages are public domain licensed (CC0)." Attribution: "Attribution is not required, but if you choose to give credit you can do so by mentioning "Kenney"." Also: do not use the Kenney logo. <https://kenney.nl/support>

| Pack | Licence | Files | Tags on page | Wanted category | Source |
|---|---|---|---|---|---|
| Interface Sounds | CC0 | 100 | interface, click, button | UI interaction | <https://kenney.nl/assets/interface-sounds> |
| UI Audio | CC0 | 50 | button, switch, click | UI interaction | <https://kenney.nl/assets/ui-audio> |
| Impact Sounds | CC0 | 130 | impact, foley | Combat hits, possibly drops | <https://kenney.nl/assets/impact-sounds> |
| RPG Audio | CC0 | 50 | foley, rpg, footstep, weapon | Drops, potion (to audition) | <https://kenney.nl/assets/rpg-audio> |
| 50 RPG sound effects (OGA mirror of Kenney) | CC0 | 50 OGG | metal, cloth, book, footstep, creak, coin, knife | Item drops by material | <https://opengameart.org/content/50-rpg-sound-effects> |
| Music Jingles | CC0 | 85 | music, jingle | Level-up (to audition) | <https://kenney.nl/assets/music-jingles> |

Not used for this list: Casino Audio, Sci-fi Sounds, Digital Audio, Voiceover Pack, Voiceover Pack (Fighter) (listed at <https://kenney.nl/assets/category:Audio>).

Notes:
- The RPG Audio page does not list individual filenames. Its tags (metal, cloth, book, coin, knife) come from the OGA mirror, so the per-kind mapping for item drops must be checked against the actual files after download.
- Music Jingles does not say what the jingles are. Level-up is a guess until auditioned.
- CC0 licence text: "waiving all of his or her rights to the work worldwide" and no attribution requirement on the deed. <https://creativecommons.org/publicdomain/zero/1.0/>

### 2b. Freesound (per-sound licence)

Freesound's FAQ: "freesound lets the user select one of three licenses for their sounds": CC0, CC-BY (attribution) and CC-BY-NC (attribution, noncommercial). Sampling+ is removed. <https://freesound.org/help/faq/>

- CC-BY: "For 'attribution' you should always mention the original creators of the sounds when you use them."
- CC-BY-NC: "you can't earn any money with the piece of work you create!" (the FAQ text is about the work, not games; avoid NC for a game).
- Search filter: "an option has been added to the licenses filter in the search page," which filters for "sounds under CC-BY or CC0." It does not isolate CC0, so check the licence on each sound.

Freesound is not a source for bulk packs. Use it only for individual sounds that are not in Kenney, and record the licence per file.

### 2c. OpenGameArt (per-asset licence, CC0 verified)

Each OGA asset page shows its own licence line. The OGA FAQ page returned 404 on fetch, so the licence is taken from each asset page.

| Track | Licence (page) | Author | Format | Loop | Category | Source |
|---|---|---|---|---|---|---|
| Town Theme RPG | CC0 | cynicmusic | MP3 only (1.3 MB) | Not stated | Town | <https://opengameart.org/content/town-theme-rpg> |
| Battle Theme A | CC0 | cynicmusic | MP3 only (3.3 MB) | Not stated | Combat | <https://opengameart.org/content/battle-theme-a> |
| Music loop variations | CC0 | obscure music | loops.zip, WAV tag, OGG preview | Yes (6 loops, 3 steps x 2 variations) | Generic loops (Town or Field) | <https://opengameart.org/content/music-loop-variations> |
| Overworld (BGM) | CC0 | IntelligentGene | MP3 (275 KB) | Yes ("This is a looping background music.") | Field | <https://opengameart.org/content/overworld-bgm> |
| Exploration Theme | CC0 | CleytonKauffman | WAV, OGG, FLAC | Yes ("Loops seamlessly: Yes.") | Field, menu candidate | <https://opengameart.org/content/exploration-theme> |

Found by search, not opened (**snippet**): Urban Battle (MintoDog, CC0, loopable, OGG/MP3), Boss Battle Theme (CleytonKauffman, CC0, looped), Sunset Plains (Yoiyami, CC0, loop not confirmed). Sources: <https://opengameart.org/content/urban-battle>, <https://opengameart.org/content/boss-battle-theme>, <https://opengameart.org/content/sunset-plains>.

Cautions (**snippet**):
- "The World At Your Door" is CC-BY 3.0 (attribution needed). <https://opengameart.org/content/the-world-at-your-door-rpg-orchestral-essentials-overworld-music>
- "RPG - Overworld - Melodies of the Future" is CC-BY 4.0. <https://opengameart.org/content/rpg-overworld-melodies-of-the-future>
- "Battle" and "Town" by mla are CC-BY 4.0. <https://opengameart.org/content/battle-0>, <https://opengameart.org/content/town-5>
- SNES RPG Overworld: an earlier version used Chrono Trigger instruments, replaced later. Use only the revised file. <https://opengameart.org/content/snes-rpg-overworld>

Mapping to the wanted loops: Town (Town Theme RPG or Music loop variations), Field (Overworld (BGM) or Exploration Theme), Combat (Battle Theme A, loop unverified; Urban Battle from snippet), Main menu (Exploration Theme, audition). The loop status of Battle Theme A must be checked before use.

### 2d. Sonniss GDC game audio bundles (not for commit)

Sonniss bundle page, 2015 to 2024 archive. <https://sonniss.com/gameaudiogdc/>

- Attribution: "No attribution is required and you can use them on an unlimited number of projects for the rest of your lifetime."
- Redistribution: "Not as sound effects. That covers the original files and any edited or re-designed versions," and this applies "on their own or inside a sound library, sample pack, asset pack or anything similar."
- Shipping inside a finished product is allowed: "you can absolutely sell them as part of your finished game, film, app, or creative project."
- Licensed for "media production (games, film, TV, interactive projects) only."
- AI/ML training is "strictly prohibited under our license terms."

Conclusion: a public repo holds the raw files, which is redistribution as sound effects, so they may not be committed. A WebGL build exposes the raw files to any visitor, so they may not be shipped either. The page does not discuss public repos explicitly, so this is the plain reading of "not as sound effects". Re-check the EULA of the year's bundle on Sonniss's site before any use.

Third-party summaries (not primary, not relied on): <https://licenseorg.com/guide/music-audio/sonniss> and a mirrored EULA PDF at <https://scrubby.duckdns.org/Sonniss.com%20-%20GDC%202023%20-%20Game%20Audio%20Bundle/License.pdf>.

## 3. Keeping sounds local-only

Use this only if a sound must stay out of git and out of the public build. For the CC0 set it is not needed.

### 3a. Keep the files out of git

The repo `.gitignore` already has Addressables and `.claude/` rules. Add a rule for the local folder, and ignore both the folder and its `.meta` files. Unity expects every asset to have a `.meta`, so ignoring only the audio files leaves orphan metas in the tree.

```
/[Aa]ssets/LocalAudio/
/[Aa]ssets/LocalAudio.meta
```

Caveats (**derived**):
- A committed scene or prefab that references an ignored clip will show a missing reference on a clone. Load local clips by a key from code, not through a serialized field, so a clone without the files still runs.
- The repo has a committed WebGL build on `main` (`docs/media/InventoryTetris_20220123/Build/`). A local clip that reaches a build output folder would be committed. Keep build output out of git before any publish.

### 3b. Keep the files out of the published build

Unity's rules, as far as the sources cover them:

- **Resources:** "Assets in `Resources` folders are always included in the Player build, even if they're not referenced by anything." Do not put local-only audio in `Resources`. The repo already has `Assets/Resources/GameConfig.asset` and `GameInput.inputactions` there, so this folder is in use. <https://docs.unity3d.com/Manual/LoadingResourcesatRuntime.html>
- **Scenes:** "Unity includes that scene and any assets used in the scene in your game or application's built-in data." Any clip referenced from a scene or prefab is in the build. <https://docs.unity3d.com/Packages/com.unity.addressables@1.20/manual/ManagingAssets.html>
- **StreamingAssets:** "Any files in this directory are copied without modification into the Player output." Local-only audio must not go here. On WebGL, files are read with UnityWebRequest from a URL. <https://docs.unity3d.com/Manual/StreamingAssets.html> (search summary; the 2022.3 copy at <https://docs.unity3d.com/2022.3/Documentation/Manual/StreamingAssets.html> says the same).
- **Editor folders:** Editor scripts are "not available in Player builds at runtime". Assets reachable only from an `Editor` folder are excluded from Player builds. <https://docs.unity3d.com/Manual/SpecialFolders.html> (the Resources-under-Editor exception is from a search summary of the Resources page).
- **Addressables:** "When you add an asset to an Addressables group, that asset is packed into an AssetBundle when you make a content build." "Before building a player, you must make a content build." An asset in no group, and not referenced by a scene, is not packed. <https://docs.unity3d.com/Packages/com.unity.addressables@1.20/manual/ManagingAssets.html>
- **WebGL output:** the build is "all files necessary to run your application in a web browser" (`.data`, `.wasm`, `.loader.js`, `.framework.js`). Anything in the build is public once hosted. <https://docs.unity3d.com/Manual/webgl-building.html>

Options, ranked:

1. **Editor-only load (recommended for local-only sounds).** Put the files in `Assets/LocalAudio/` (gitignored), reference them from no scene, prefab or Resources folder, and load them in a `#if UNITY_EDITOR` path with `AssetDatabase.LoadAssetAtPath`. The Editor and Play Mode get the audio, the Player build gets nothing, and the build stays clean. (**derived** from the Editor and Resources rules above.) The Player gets silence, which section 3c covers.
2. **Addressables group left out of the content build.** Put the local clips in a group that the team does not include in the build profile, and never reference the group from a committed scene. This is more machinery than option 1 and needs a check that the content build skips it. (**derived**.)
3. **Remote URL.** Serving a local clip from a URL that is not in the repo is the only way to hear it on a public WebGL page, and it is still public. It is not a fix for licensing. Not recommended.

Do not use `StreamingAssets` or `Resources` for anything in the local-only set.

### 3c. Missing-file behaviour (design, not sourced)

Design rules for the game code, independent of where the files live:

- All sounds are looked up by a key (for example `ui.click`, `drop.metal`, `music.town`) through one `AudioBank` service. The bank holds a map from key to clip, filled from the build's configured clips, and an optional local set in the Editor.
- A missing key returns null. The caller does nothing: a UI key with no clip is a silent no-op, a music key with no clip keeps the current track or plays silence.
- Log one `Debug.LogWarning` per missing key, not per play, so the console stays readable.
- Never throw, never fail scene load, never block a Player start on audio. The menu and combat must work with every clip missing.
- Drop sounds map by item kind. If a kind has no clip, fall back to its material category, then to a generic drop sound, then to silence.

This needs one code path that is covered by an EditMode test with an empty bank.

## 4. Gaps

- **No Diablo II-specific sound policy found.** The Blizzard FAQ and Video Policy are the closest. A Diablo II Resurrected EULA was not opened.
- **Blizzard EULA page** was fetched once; its date comes from the page ("LAST REVISED March 21, 2024"). A search summary said the EULA copy is roughly 939 days old, so check the current version.
- **Kenney RPG Audio and Music Jingles contents** are not listed on their pages. Per-kind drop mapping and the level-up sound must be checked against the files.
- **No CC0 menu track** was verified. Exploration Theme is the best candidate found.
- **OGA FAQ** returned 404. Licences come from each asset page.
- **Unity manual pages** for "Include additional files in a build", "Build Player included assets" and "Asset Database" returned 404 or no content. The StreamingAssets, Resources and Editor claims use the pages that did load plus search summaries; those summaries are marked.
- **Sonniss** page was fetched; the EULA PDF was not opened from the vendor.

## Sources

- Blizzard EULA: <https://www.blizzard.com/en-us/legal/fba4d00f-c7e4-4883-b8b9-1b4500a402ea/blizzard-end-user-license-agreement>
- Blizzard Legal FAQ: <https://www.blizzard.com/en-us/legal/c1ae32ac-7ff9-4ac3-a03b-fc04b8697010/blizzard-legal-faq>
- Blizzard Video Policy: <https://www.blizzard.com/en-us/legal/dd76b654-f2c4-4aaa-ba49-ca3122de2376/blizzard-video-policy>
- Kenney support: <https://kenney.nl/support>
- Kenney audio list: <https://kenney.nl/assets/category:Audio>
- Kenney packs: <https://kenney.nl/assets/interface-sounds>, <https://kenney.nl/assets/ui-audio>, <https://kenney.nl/assets/impact-sounds>, <https://kenney.nl/assets/rpg-audio>, <https://kenney.nl/assets/music-jingles>
- CC0 deed: <https://creativecommons.org/publicdomain/zero/1.0/>
- Freesound FAQ: <https://freesound.org/help/faq/>
- OpenGameArt pages: <https://opengameart.org/content/50-rpg-sound-effects>, <https://opengameart.org/content/town-theme-rpg>, <https://opengameart.org/content/battle-theme-a>, <https://opengameart.org/content/music-loop-variations>, <https://opengameart.org/content/overworld-bgm>, <https://opengameart.org/content/exploration-theme>
- Sonniss bundle page: <https://sonniss.com/gameaudiogdc/>
- Unity manual, Resources: <https://docs.unity3d.com/Manual/LoadingResourcesatRuntime.html>
- Unity manual, StreamingAssets: <https://docs.unity3d.com/Manual/StreamingAssets.html>
- Unity manual, Special folders: <https://docs.unity3d.com/Manual/SpecialFolders.html>
- Unity manual, WebGL build: <https://docs.unity3d.com/Manual/webgl-building.html>
- Addressables, Managing assets: <https://docs.unity3d.com/Packages/com.unity.addressables@1.20/manual/ManagingAssets.html>
- Repo: `docs/agents/github-pages.md`, `.gitignore`, `Assets/Resources/`, `docs/media/InventoryTetris_20220123/Build/` (on `main`)
