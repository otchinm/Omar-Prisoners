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
night length (10–40 min), the **difficulty** (Easy / Normal / Hard / Nightmare: Omar's speed and senses, supplies,
how dark it is — Hard and Nightmare also switch to the sickly olive tape lighting of the reference video) and whether
the AI plays Omar when no human does, then presses **START**. The VHS filter has presets in the settings.
Only one person can be Omar. You can't join a match already in progress. If Omar's player disconnects, the AI
takes over his body.

### Controls

| Prisoner | | Omar | |
|---|---|---|---|
| WASD | move | WASD | move |
| Shift | sprint (stamina) | Shift | run (stamina) |
| C / Ctrl | crouch (quiet, harder to see) | LMB | cleaver |
| E | interact (hold for long actions), hide in wardrobes / lockers / **under beds** | RMB | scream (one of 4 screams) |
| hold LMB on a door + move the mouse | swing the door open / shut (physical doors) | walk into a door | shove it open |
| F / LMB | use item (Zippo open + strike / close, flashlight, bandage, throw bottle, fire the revolver...) | E | unlock doors, **search** hiding spots (lifts beds), smash boards |
| 1 2 3 / wheel | select slot | T | string a tripwire across a doorway |
| G | drop item | G | set a bear trap |
| Tab | inventory | Q | sense nearby prisoners |
| Esc | menu (the game keeps running) | Esc | menu |

---

## How it plays

**The night.** A match lasts one night, from 1:00 to 6:00 AM, shown on the VCR timecode. Each prisoner starts
locked in **their own cage in a random cell room** (the cage room upstairs, the barn stalls, the restroom by the
parking lot); playing alone there is exactly one cage. After a few seconds the locks fail. Omar spends the first
~20 seconds waking up in the furnace room in the basement.

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

**Omar.** A 2.3 m giant who has to stoop through door frames, shoves doors open with his body and butchers meat
in the kitchen now and then (you can watch him from the dining room vent — or hit him from behind while he is busy
to stun him). When he sees you long enough, the **"find"** sound plays (sometimes followed by a scream) and the
chase begins: a chase drone, his screams and a pounding heartbeat. When you've been **out of his line of sight for
4 seconds**, all of those sounds stop. Walking around is quiet; the moment Omar **comes into view** the picture
picks up some VHS interference and quiet tape static that grow as he gets closer.

**The grandmother.** An old woman watches TV in her wheelchair in the living room. If she sees you she shrieks and
calls Omar; later in the night she starts rolling through the ground floor. The **revolver** (2 rounds, one per
match at a random spot) can kill her — or stun Omar.

**Getting hurt.**
* The first cleaver hit makes you **bleed**: you limp and leave a blood trail.
* A second hit means Omar **drags you back to a cage** (your own if it is free, else the nearest one) and takes
  everything except your lighter.
* Friends can free you with the cage key, a lockpick or bolt cutters. You can also mash **E** to break the
  rusty lock.
* The **third capture is final**.

**Traps.**
* **Tripwires** stretched across passages trigger Omar's **alarm siren** when crossed (the tin cans on the wire crash
  down) and he comes running; whoever tripped staggers for a couple of seconds and can't sprint.
* **Bear traps** hold and injure you until you tear free (lots of E mashing, the chain clanks) or a friend pries the
  trap open. Omar carries 4 wires and 3 traps; spent ones come back side by side (a wire every 35 s, a trap every 50 s).
* Some traps are placed randomly at the start of each match, and Omar can set more. Crouch to disarm them.

### The map: the Base of the Second Class

One large map, built by code from every reference image in `SourceAssets/map`. A chain-link and barbed-wire
fence (about 125 × 130 m) surrounds it, with forest beyond.

* **The farmhouse.** Three floors joined by a central hall and two staircases.
  * *Upstairs:* the **cage room** (one of the cell rooms prisoners can wake up in), a storage room, Omar's locked **radio room**,
    a study and a supply closet.
  * *Ground floor:* the dining room (with a crawl vent looking into the kitchen), the kitchen / butchery with Omar's
    butcher block, the **grandmother's TV room**, a boarded-up bathroom,
    the bedroom with the grandfather clock, and Omar's portrait in the hall.
  * *Basement:* the **furnace room** where Omar wakes up, the generator room with the fuse box, the exhibit of
    burnt mannequins, and the antechamber with the **fallout shelter** keypad. Behind the shelter door, a tunnel
    leads under the yard to a ladder past the west fence.
* **The yard.** Gravel driveway, plastic flamingos, a dead tree, a floodlight, a pickup wreck, the **main gate**
  (padlocked) and the road beyond it.
* **West side.** The **parking lot** with **the car** and three wrecks, the chained vehicle gate, and an outdoor restroom
  (with a cell cage).
* **North side.** The **corn field** with its clearing (the helicopter landing zone), the **barn** (two stalls are cells), the
  **fuel drums** against the north fence, the silo, the windmill and the water tower.
* **East side.** A tool shed.

You can hide in wardrobes, lockers and under beds, and the tall corn hides you from a distance. Items,
notes, the shelter code, some traps and the car's dead battery change every match. The map and item layout come
from a seed that the host shares, so every player builds the same map.

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
thunder and distant screams — and the mannequins in the basement are not always where you left them.

**Admin panel (owner only).** F10 opens the owner's admin panel (password protected; the host verifies it for
other peers). It is not needed to play.

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
