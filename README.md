# THE PRISONERS OF OMAR

A low-poly **PS1 + VHS** multiplayer horror game in the style of Puppet Combo games
(*Stay Out of the House*, *Murder House*). Up to **4 prisoners** wake up in the cages of the
**Base of the Second Class** of Omar Alibutaev and try to escape. A **5th player is Omar**, the masked butcher
who hunts them. If nobody plays Omar, an AI controls him.

* Engine: **Unity 2022.3 LTS or Unity 6**, **Built-in Render Pipeline**, no third-party packages.
* Everything is generated at runtime by code: the map, characters, items, animations, UI and menus.
  The project contains no binary scenes or prefabs, so it diffs and merges cleanly as text.
* Custom UDP networking (host + 4 clients), LAN discovery, AI Omar, single-player practice.

---

## Opening the project

1. Install **Unity 2022.3 LTS** (or Unity 6) with Unity Hub, then use *Add project from disk* and pick this folder.
   If Hub complains about the exact editor version, pick any 2022.3.x or 6000.x.
2. On first open, `Assets/PrisonersOfOmar/Scripts/Editor/ProjectSetup.cs` runs once. It:
   * creates the (empty) scene `Assets/PrisonersOfOmar/Scenes/Main.unity`,
   * adds that scene to the build settings,
   * sets the player settings (gamma colour space, run in background, windowed/fullscreen),
   * enables the legacy Input Manager if the project template turned it off.
   To run it again, use **Prisoners of Omar ▸ Run Project Setup**.
3. Press **Play**. `GameBootstrap` starts the game from any scene.
4. To build, use **Prisoners of Omar ▸ Build Windows / Linux / macOS**, or the normal Build Settings window.

> Unity 6 creates new projects with URP and the new Input System. This project uses the **Built-in pipeline**,
> because its shaders are built-in CG shaders, and the **legacy Input Manager**. Don't assign a URP asset in
> Graphics settings. If input does nothing after opening in Unity 6, set *Project Settings ▸ Player ▸
> Active Input Handling* to **Both** and restart the editor.

## Playing together

| | |
|---|---|
| **Host** | Main menu ▸ *HOST GAME*. The lobby shows your local IP addresses and the port (default **27015/UDP**). |
| **Join on the same network** | *JOIN GAME*. Games on your LAN appear in the list automatically (UDP broadcast on 27016). |
| **Join over the internet** | Type the host's address and port. The host must forward **UDP 27015**, or everyone can use a virtual LAN such as Radmin VPN, ZeroTier or Hamachi. |
| **Alone** | *PLAY ALONE (VS AI OMAR)*. |

In the lobby, each player picks one of the 4 prisoners or Omar (**◀ ▶**) and marks *READY*. The host sets the
night length (10–40 min) and whether the AI plays Omar when no human does, then presses **START**.
Only one person can be Omar. You can't join a match already in progress. If Omar's player disconnects, the AI
takes over his body.

### Controls

| Prisoner | | Omar | |
|---|---|---|---|
| WASD | move | WASD | move |
| Shift | sprint (stamina) | Shift | run (stamina) |
| C / Ctrl | crouch (quiet, harder to see) | LMB | cleaver |
| E | interact (hold for long actions) | RMB | scream (one of 4 screams) |
| F / LMB | use item (lighter / flashlight on-off, bandage, throw bottle...) | E | open / unlock doors, **search** hiding spots, smash boards |
| 1 2 3 / wheel | select slot | T | string a tripwire across a doorway |
| G | drop item | G | set a bear trap |
| Tab | inventory | Q | sense nearby prisoners |
| Esc | menu (the game keeps running) | Esc | menu |

---

## How it plays

**The night.** A match lasts one night, from 1:00 to 6:00 AM, shown on the VCR timecode. The prisoners start
locked in the four cages upstairs. After a few seconds the locks fail. Omar spends the first ~20 seconds waking
up in the furnace room in the basement.

**Inventory.** Each prisoner carries **3 items**. Slot 1 always starts with a **lighter**: it is your light,
it burns fuel, and Omar can see the flame from far away. Items lie around the whole map, and their positions are
**randomised every match**:

| Item | Use |
|---|---|
| Lighter fuel | Refill the lighter, or pour it on the fuel drums |
| Band-Aids | Stop the bleeding (heal one injury) |
| Flashlight + batteries | Bright beam, very visible |
| Sound meter | Shows how much noise you make |
| Bolt cutters | Cut the main gate padlock or a cage lock (loud) |
| Car keys, gas can, car battery | The car |
| Fuse | Powers the radio room |
| Cage key, lockpick | Free caged friends; a lockpick also opens a locked door |
| Crowbar | Pry boarded doors; breaks Omar's grip once |
| Bottle | Throw it to make noise somewhere else |
| Painkillers | Stamina, and ignore the limp for a while |

**Stealth.** Running, doors, dropping or throwing things make **noise**. Omar's player sees it as ripples
through walls, and the AI comes to investigate. Darkness and crouching hide you; your own lighter or flashlight
gives you away. Hide in **wardrobes, lockers and under beds**, but Omar can tear them open.

**Omar.** When he sees you long enough, the **"find"** sound plays and the chase begins: a chase drone, his
screams and a pounding heartbeat. When you've been **out of his line of sight for 4 seconds**, all of those
sounds stop. Whenever Omar is near, the picture falls apart into **VHS static and interference** and you hear
low-fi static.

**Getting hurt.**
* The first cleaver hit makes you **bleed**: you limp and leave a blood trail.
* A second hit means Omar **drags you back to a cage** and takes everything except your lighter.
* Friends can free you with the cage key, a lockpick or bolt cutters. You can also mash **E** to break the
  rusty lock.
* The **third capture is final**.

**Traps.**
* **Tripwires** stretched across passages trigger Omar's **alarm siren** when crossed, and he comes running.
* **Bear traps** hold and injure you until you pull free, or until a friend pries the trap open.
* Some traps are placed randomly at the start of each match, and Omar can set more. Crouch to disarm them.

### Escape routes and endings

| Route | How | Ending |
|---|---|---|
| Main gate | Cut the padlock with **bolt cutters**, then walk down to the road | **THE LONG ROAD** |
| The car | **Gas can** + **car keys**. The battery is dead in some matches: find the **car battery**. Everyone who boarded escapes | **HEADLIGHTS** |
| Fallout shelter | Find the **4 notes** with the randomised code, enter it on the basement keypad, crawl through the tunnel | **UNDERGROUND** |
| The radio | Put the **fuse** in the basement fuse box, call for help from Omar's radio room, then survive until the helicopter lands in the corn field | **SIGNAL** |
| The fuel drums | Pour **lighter fuel** on the drums behind the barn and light them with the **lighter**. The blast opens the fence and stuns Omar if he's close | **ASHES** |
| Everyone caught | — | **THE SECOND CLASS** |
| 6:00 AM | — | **DAWN** |

* Every ending also says whether **everyone** made it out or some of you are **still in the pens**.
* Each ending has a chance of a darker **random twist**.
* The ending screen lists each player's fate.

Random events also happen during the night: power cuts, the phone ringing at 3 AM, a TV switching itself on,
thunder, distant screams, and **anomalies**. During an anomaly the whole environment warps and tears like a
failing PS1 renderer, and the mannequins in the basement are no longer where you left them.

---

## Project layout

```
Assets/PrisonersOfOmar/
  Scripts/Core         shared enums, layers, deterministic RNG, MeshBuilder
  Scripts/Net          UDP transport (reliable + unreliable channels, fragmentation, LAN discovery)
  Scripts/Rendering    PS1 vertex lighting, low-res camera rig, VHS post-process, anomalies, FX
  Scripts/Characters   procedural low-poly prisoners / Omar / mannequins, procedural animation, item models
  Scripts/Map          the Base of the Second Class, built from code
  Scripts/Gameplay     session, match rules (host authoritative), controllers, AI, items, traps, endings
  Scripts/UI           VHS immediate-mode UI and all screens
  Scripts/Audio        audio manager (categories, ducking, menu music)
  Resources/           shaders, textures, audio (loaded by path at runtime)
SourceAssets/          the original reference images and sounds
Tools/AssetPipeline/   Python generators for textures / character textures / audio / map plans
Tools/CompileCheck/    compile the C# against Unity reference assemblies without the editor
Tools/NetTests/        networking tests (dotnet run --project Tools/NetTests)
Docs/                  architecture notes and the asset name contract
```

### Regenerating assets

```
python3 Tools/AssetPipeline/textures/build_textures.py     # environment / props / foliage / sky / FX / UI
python3 Tools/AssetPipeline/characters/build_characters.py # character atlases + item textures
python3 Tools/AssetPipeline/audio/build_audio.py           # generated sound effects and loops
```

Textures are built from **real photographs** (CC0 scikit-image sample photos, and Poly Haven HDRIs shipped in the
`@pmndrs/assets` npm package), then degraded. Each generator is deterministic and documents its sources in its
README.

### Checks without the editor

```
Tools/CompileCheck/check.sh            # runtime scripts
Tools/CompileCheck/check.sh --editor   # + editor scripts
dotnet run --project Tools/NetTests    # 24 transport scenarios (loss, reordering, fragmentation, fuzzing)
```

## Third-party material

* `SourceAssets/` and the copied sounds in `Resources/Audio/Music`, `Resources/Audio/Omar/{alarm,find,scream_1-4}`
  and `Resources/Audio/Stingers/screams_long` were supplied by the project owner. They include material from
  Puppet Combo's *Stay Out of the House* (menu theme, screams) and screenshots used as visual references.
  Make sure you have the rights before distributing a build.
* Fonts: VT323 and Anton, SIL Open Font License (see `Tools/AssetPipeline/textures/fonts/`).
* Photo sources: scikit-image sample data (CC0 / public domain) and Poly Haven HDRIs (CC0).
