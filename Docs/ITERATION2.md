# Iteration 2 — work packages, contracts and requirements

This is the build spec for the second big pass on **The Prisoners of Omar**, written from the owner's
feedback after playing the first version. Nine work packages run **in parallel**, each in its own git
worktree. Packages must stay inside the files they own; the scaffold commit already contains every
cross-package contract (enums, stubs, reserved message ids, partial-class splits) so each package
compiles on its own and the pieces fit when merged.

Owner's feedback, in short (all of it must be addressed by some package below):

* Doors open by **holding LMB and moving the mouse** (physical doors), not with E. Omar **pushes doors open
  by walking through them** with the same physics.
* Omar: **find always plays** when he spots someone, sometimes followed by a random **scream_1..4**. Omar is
  **louder**. He **screams when the trap siren goes off**. Much **stronger wind-up** on his cleaver swing and a
  **better cleaver** (like the reference). **Bigger, wider, scarier** but faithful to the reference. **Slower
  while patrolling**. **Muffled, heavy footsteps**. His run is **too human**: just accelerate the walk, stiff /
  slightly plastic, **no forward lean, little arm swing**.
* The proximity **interference is far too strong** (visual and audio): the player must always see what is in
  front of them, even Omar up close. The picture should just look cheap and degraded. Quieter, reworked static.
* Omar **sometimes chops meat in the kitchen** (optional interaction for players, e.g. stunning him), with a
  **vent** players can watch him from.
* A **revolver** that can kill the **grandmother** — an old woman in a **wheelchair watching a TV** full of
  interference in her own room. She **detects players and screams** (loud, disgusting, degraded sounds like
  Omar's), stays in her room at first and **starts roaming the house after some progress**.
* **Item sounds are too clean / cartoonish** (bear trap interactions especially): rework every sound that sounds
  like Minecraft / Terraria; hissy, rough, degraded, like Puppet Combo. Keep the sounds that are good.
* **Admin panel** with lots of features, usable **only by the owner**.
* Map: **see-through gaps in the floor at doorways**, a **jittering rug** at the spawn (z-fighting), **repeated
  "help" blood writings** → remove / vary; new textures in the same style; **more narrow, claustrophobic
  spaces**; expand and improve the map; **no colourful / reddish lighting**: grey, simple, bleak. Highest
  difficulty = as dark as now.
* Solo: **only one cage**. Prisoners **spawn in random rooms** (all together, all apart, 2+1+1...). If nobody
  picks Omar, the **AI plays Omar automatically**.
* Characters: women **prettier with feminine shapes**, **more characters** (the reference with the woman in a
  plaid dress, the boy in the colourful shirt, the man in a white shirt), the lobby preview shows their
  **backs** (must show the front), third-person movement must look right (Omar's player watches them),
  **different degraded voices** per character.
* First person like the reference video: **Zippo lighter** look and behaviour, **STAMINA** bar, effects.
  Items highlighted with a **pixelated glow around them** (not the item itself). Crosshair: **solid white
  square**, easy to see. Better first-person **hand** (stylised, not realistic).
* **Hide under beds** with an animation; when Omar finds you there he **lifts the bed**. Wardrobe hiding
  smoother, **no screen overlay**.
* Menu is liked. The **controls / tutorial** pages are too detailed and repeat themselves → short.
  **Graphics settings with VHS filter presets** (like Stay Out Of The House). Improve the UI, keep it minimal.
* **Difficulty settings** (designed below).

Reference media (all in the repo):

| What | Where |
|---|---|
| First-person reference (lighter, STAMINA bar, item label near the crosshair, VHS look) | `SourceAssets/firstperson/OtKY3U.mp4`, frames `SourceAssets/_frames/firstperson_sheet0.jpg`, `..._sheet1.jpg` |
| Third-person movement of prisoners + killer | `SourceAssets/players/thirdperson/Emmaambush.mp4`, frames `SourceAssets/_frames/thirdperson_sheet*.jpg` |
| New characters (woman in plaid dress, boy in colourful shirt, man in white shirt) | `SourceAssets/players/more/EyAqyzjWQAUE23f.webp` |
| Existing four prisoners | `SourceAssets/players/CzRaIQcVIAAElQD.jpg` |
| Grandmother + wheelchair + her room | `SourceAssets/granny/*.webp`, sheet `SourceAssets/_frames/granny_sheet.jpg` |
| Omar (look, cleaver) | `SourceAssets/omar/*` |
| Omar chopping meat on a table (kitchen routine / vent view) | `SourceAssets/omar/RX5dWgUPxC7k.mp4`, sheet `SourceAssets/_frames/omar_chopping_meat_sheet.jpg` |
| Map references | `SourceAssets/map/*` |
| Owner-supplied sounds (quality reference: dirty, loud, VHS) | `SourceAssets/sound/**`, `Assets/PrisonersOfOmar/Resources/Audio/Omar/{find,alarm,scream_1-4}` |

Use `ffmpeg` (installed) to pull more frames from the videos if you need them.

---

## 1. Work packages and file ownership

| Package | Owns (may edit) |
|---|---|
| **AUDIO** | `Tools/AssetPipeline/audio/**`, `Assets/PrisonersOfOmar/Resources/Audio/**`, `Scripts/Audio/**`, the audio section of `Docs/ASSETS.md` |
| **TEXTURES** | `Tools/AssetPipeline/textures/**`, `Resources/Textures/{Env,Props,Decals,FX,Foliage,Sky,UI}/**`, the texture section of `Docs/ASSETS.md` |
| **CHARACTERS** | `Scripts/Characters/**` (incl. `GrandmaRig.cs`), `Tools/AssetPipeline/characters/**`, `Resources/Textures/Characters/**`, `Resources/Textures/Items/**` |
| **MAP** | `Scripts/Map/**`, `Tools/AssetPipeline/map/**` |
| **RENDERING** | `Scripts/Rendering/**` (incl. `PsxItemGlow.cs`), `Resources/Shaders/**` |
| **UI** | `Scripts/UI/**`, `Scripts/Gameplay/Settings.cs` |
| **PLAYER** | `Gameplay/Match/{PrisonerController, OmarController, Entities, Interaction, LocalInventory, CharacterMotor, SpectatorCamera}.cs`, `Gameplay/GameInput.cs`, `Gameplay/Match/MatchHost.Player.cs`, `Gameplay/Match/MatchWorld.Player.cs`, new files you add for your features; `Msg` ids **100–119** |
| **WORLD** | `Gameplay/Match/{OmarAI, MatchHost, MatchWorld, MatchWorld.Handlers, MatchWorld.Events, MatchWorld.Gun, Avatar, DetectionSystem, ChaseAudio, ProximityFx, AmbienceController, ItemSpawner, Endings, MatchTypes, ItemDefs}.cs`, new files (`GrandmaEntity.cs`, `MatchHost.Grandma.cs`, `MatchHost.Kitchen.cs`, `MatchHost.Gun.cs`, `MatchWorld.Grandma.cs`, `KitchenEntity.cs` ...); `Msg` ids **120–139** |
| **SESSION** | `Gameplay/Session/{NetSession, PlayerInfo, NetLogHook}.cs`, `Gameplay/GameRoot.cs`, `Gameplay/Admin/**`, `Gameplay/Match/{MatchHost.Spawn, MatchHost.Admin, MatchWorld.Spawn}.cs`, new files (`MatchWorld.Admin.cs` ...); `Msg` ids **140–159** |

`Gameplay/Session/Msg.cs` is shared: **only add enum values inside your own id range** (one line each, at the end
of your block). Never edit `README.md`, `Docs/ARCHITECTURE.md` or files of another package — if you need a
change elsewhere, write it under "needs elsewhere" in your report and code against the contract.

Paths above are relative to `Assets/PrisonersOfOmar/Scripts/` unless they start with `Tools/`, `Assets/` or `Docs/`.

## 2. Rules for every package

* Unity 2022.3 / Unity 6, **Built-in render pipeline**, legacy Input Manager, no packages. Everything is built
  from code at runtime (no scenes, prefabs or binary assets except textures / audio files under `Resources`).
* There is **no Unity editor** here. C# must compile: `Tools/CompileCheck/check.sh` (runtime) and
  `Tools/CompileCheck/check.sh --editor`. Shaders cannot be compiled: proofread them like a compiler.
* Keep public APIs that other code uses unless the spec says otherwise; add, don't break.
* **Networking**: the host is authoritative. Every change to shared state goes host → all through messages; the
  host applies its own requests through the loopback (never mutate shared state directly on the host). Owner-
  authoritative movement. Deterministic generation only with `DeterministicRandom` (never `UnityEngine.Random`
  or per-peer state for anything that must match on every peer). New wire data → keep `NetWriter` /
  `NetReader` order symmetric. `GameInfo.ProtocolVersion` is already bumped to 2.
* Style: match the surrounding code (naming, comment density, idioms). No model names / AI attributions in code
  or commits.
* **Commit your work in your worktree branch** (several commits are fine) with clear messages ending with:

      Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
      Claude-Session: https://claude.ai/code/session_018gGCy4z6Kqc9iDE5b8U5nc

  Do not push, do not merge other branches, do not touch the main checkout.
* Final report: what you did per requirement, what you could not do, risks, "needs elsewhere", and numbers
  other packages need (sizes, timings, names).

## 3. Contracts already in the code (scaffold commit)

| Contract | Where | Implemented by |
|---|---|---|
| `CharacterSkin.Prisoner5..7`, `GameInfo.PrisonerSkins`, `GameInfo.IsPrisonerSkin` | `Core/Enums.cs`, `Core/GameInfo.cs` | CHARACTERS (looks), UI (lobby), SESSION (assignment) |
| `ItemType.Revolver` (+ `ItemDefs` entry, charge 1 = 2 rounds, icon `Textures/UI/icon_revolver`) | `Core/Enums.cs`, `Gameplay/Match/ItemDefs.cs` | CHARACTERS (model), TEXTURES (icon), PLAYER (shooting), WORLD (spawn + hit) |
| `Difficulty` enum, `MatchSettings.Difficulty` (serialized), `Settings.Difficulty` | `Core/Enums.cs`, `Session/PlayerInfo.cs`, `Gameplay/Settings.cs` | UI (picker), SESSION (plumbing), WORLD (effects) |
| `VhsPreset` enum, `VhsEffect.Preset`, `Settings.VhsPreset` (applied in `Settings.ApplyDisplay`) | `Core/Enums.cs`, `Rendering/VhsEffect.cs` | RENDERING (looks), UI (setting) |
| `HoldPose.Pistol`, `CharacterAction.{BedLift, ChopMeat, CrawlUnder, CrawlOut, Shoot, Cower, Push}` | `Characters/CharacterTypes.cs` | CHARACTERS (animations), PLAYER / WORLD (triggers) |
| `GrandmaRig` (`Create`, `SetMode(GrandmaMode)`, `SetMoveSpeed`, `LookAt`, `EyePosition`) | `Characters/GrandmaRig.cs` | CHARACTERS (visuals), WORLD (behaviour) |
| `PsxEnvironment.Brightness` | `Rendering/PsxLight.cs` | RENDERING (effect), WORLD (sets per difficulty) |
| `PsxItemGlow.Attach / Highlighted / GlobalStrength` | `Rendering/PsxItemGlow.cs` | RENDERING (visuals), PLAYER (attach), WORLD (difficulty) |
| `PsxFx.ClearBloodDecals()`, `PsxFx.MuzzleFlash(pos, dir)` | `Rendering/PsxFx.cs` | RENDERING (look), WORLD / PLAYER (calls) |
| `PsxMaterials.TvScreen(variant)` | `Rendering/PsxMaterials.cs` | RENDERING (animated static shader), MAP (TVs) |
| New sound paths (doors, hiding, Omar, grandmother, revolver, voices) | `Audio/Snd.cs` (`VoiceLine`, `Snd.Voice(skin, line)`) | AUDIO (files), everyone (calls) |
| `MapData.CellRooms`, `CageInfo.RoomIndex`, `MapData.Grandma` (`GrandmaInfo`), `MapData.Kitchen` (`KitchenInfo`), `MapData.GunSpots`, `MapData.CrawlSpaces`, `HidingSpotInfo.{LiftPivot, LiftAxis, LiftAngle, LifterPose, CrawlStart}` | `Map/MapData.cs` | MAP (data), gameplay packages (use) |
| `MatchHost` is `partial`: `MatchHost.Player.cs` (doors / hiding host code moved there, hooks `TickPlayer`, `OnPlayerLeftPlayer`), `MatchHost.Spawn.cs` (`PickCageFor`), `MatchHost.Admin.cs` (`OnAdminCmdReq`, partial hooks `AdminDoors`, `AdminOmar`, `AdminGrandma`) | `Gameplay/Match/` | PLAYER / SESSION / WORLD |
| `MatchWorld.Player.cs` (door / hiding handlers, `RegisterPlayerHandlers`, `UnregisterPlayerHandlers`, `TickPlayer`), `MatchWorld.Spawn.cs` (`BuildCages`, `SpawnAvatars`), `MatchWorld.Gun.cs` (`SendShoot`) | `Gameplay/Match/` | PLAYER / SESSION / WORLD |
| `CageEntity.Active / SetActive` | `Gameplay/Match/Entities.cs` | SESSION (uses) |
| Reserved `Msg` ids 100–159 (names listed in `Msg.cs`) | `Gameplay/Session/Msg.cs` | per range |
| Admin API: `Admin` (`IsAdmin`, `Pending`, `LastError`, `Log`, `TryLogin`, `Logout`, `Run`, `IsOn`, `Locations`, `DrawOverlay`), `AdminCmd`, `AdminArg`, `AdminCategory`, `AdminCmds.All` table, `AdminState` flags | `Gameplay/Admin/` | SESSION (implementation), UI (panel), WORLD / PLAYER (honour `AdminState`) |

A partial method that nobody implemented compiles to nothing, so call sites are safe before the merge.

## 4. Difficulty design (implemented by WORLD unless noted)

| | EASY | NORMAL | HARD | NIGHTMARE |
|---|---|---|---|---|
| Scene brightness `PsxEnvironment.Brightness` | 2.2 | 1.7 | 1.3 | **1.0** (= the first version's darkness) |
| Omar patrol walk speed (m/s) | 1.25 | 1.45 | 1.6 | 1.75 |
| Omar chase speed (× base run speed) | 0.88 | 1.0 | 1.06 | 1.12 |
| Omar sight range × / hearing range × | 0.8 / 0.7 | 1 / 1 | 1.15 / 1.25 | 1.3 / 1.5 |
| Detection meter fill speed × | 0.6 | 1 | 1.3 | 1.6 |
| Captures before death | 4 | 3 | 2 | 2 |
| Common supplies spawned × | 1.5 | 1 | 0.75 | 0.5 |
| Lighter / flashlight drain × | 0.6 | 1 | 1.3 | 1.6 |
| Pre-armed traps (tripwires / bear traps) | 3 / 1 | 5 / 3 | 6 / 4 | 7 / 5 |
| Item glow `PsxItemGlow.GlobalStrength` | 1 | 1 | 0.6 | 0 |
| Grandmother starts roaming at progress ≥ / night ≥ | 4 / 70% | 2 / 50% | 1 / 35% | 1 / 20% |

UI shows one line per level: EASY "MORE LIGHT, SLOWER OMAR" · NORMAL "THE NIGHT AS INTENDED" · HARD "DARKER. HE HEARS
MORE." · NIGHTMARE "PITCH BLACK. NO SECOND CHANCES."

---

## 5. AUDIO

Goal: every sound fits next to the owner's Omar sounds and menu theme: **dirty, hissy, low-fi VHS, loud where it
matters**. Nothing may sound like a clean game "blip" or a cartoon (Minecraft / Terraria style).

1. **Audit** every generated sound (`manifest.csv`). Rework everything clean or cartoonish. Mandatory: all item
   sounds (`Audio/Items/*`: lighter open / flick / close → a real **Zippo**: metallic clink, flint wheel rasp,
   whoosh of the flame, lid snap; fuel pour, bandage rip, flashlight click, battery, pickup / drop / equip, bottle
   throw, glass break, key unlock, locked rattle, lockpick, crowbar pry, wood break, bolt cut, chain drop, sound
   meter tick, pills), the **bear trap** (place, snap, pry open, struggle), tripwire snap, cages, doors, keypad,
   anything "beepy". Keep the owner-supplied files untouched (Omar find / alarm / screams, menu theme,
   screams_long) except loudness.
   Processing recipe (vary per sound): real recorded-style sources or physically modelled synthesis → room
   (small, dirty) → saturation / clipping → band-limit (80 Hz–6 kHz) → resample to 11–16 kHz → 8–10 bit crush →
   tape hiss bed (−34 dB) + wow / flutter → random gain jitter.
2. **Omar louder**: normalise Omar sounds hotter (peaks ~−1 dBFS), raise the Omar category gain in
   `AudioManager` (~+4 dB) and give his sounds longer audible distances (WORLD handles call-site volumes too).
3. **Proximity static** (`Ambience/static_loop`, `static_heavy_loop`, `Stingers/static_burst_*`,
   `Stingers/vhs_glitch_*`): rework into a **quiet**, textured dirty-tape hum + crackle + faint garbled radio,
   ~12 dB lower than now, no harsh white noise, short bursts.
4. **New sounds** — create every path in the iteration-2 block of `Snd.cs` (variants as noted):
   door creak loops (wood ×2, metal ×1, seamless loops whose pitch the code changes), latch ×2, bump ×2, unlock;
   bed crawl in / out, bed lift (wood frame scraping + mattress), wardrobe enter / exit; Omar chop ×3 (cleaver
   into wet meat on a block), meat squelch ×2, wind-up grunt ×2, stunned groan, door push; grandmother scream ×3
   (old woman, shrill, wet, disgusting, as loud and degraded as Omar's), spot (sudden shriek), mutter ×3
   (unintelligible old voice), death (choked gurgle), wheelchair squeak loop, TV loop (static + garbled old
   broadcast); revolver shot (huge, distorted, room tail), empty click, cock.
   **Rework Omar's footsteps** `Audio/Steps/omar_1..4`: muffled, heavy, boomy thuds (no wooden creak, no
   click), so they are recognisable through walls.
5. **Voices**: `Audio/Voices/prisoner1..prisoner7/{hurt_1..3, scream_1..2, breath_1..3, gasp_1..2,
   struggle_1..2, death_1}` (see `VoiceLine`). Seven distinct voices: prisoner1 young athletic man, prisoner2
   young woman, prisoner3 younger woman (higher), prisoner4 lanky young man (higher, nervous), prisoner5 adult
   woman (lower), prisoner6 teenage boy, prisoner7 adult man (deep). Degraded like old VHS dialogue. You may build
   them from formant synthesis and / or by cutting, pitch / formant shifting and mangling human material in
   `Resources/Audio/Stingers/screams_long` (never reuse Omar's screams for prisoners). Heavy degradation must
   hide any synthetic cleanliness.
6. Update `manifest.csv/.md`, `README.md`, `qa.py`; run the QA (levels, clipping, silence, loop seams, lengths).
   File format / import rules as today (`Editor/ImportSettings.cs` handles `Resources/Audio`). The audio section
   of `Docs/ASSETS.md` lists every new file.

## 6. TEXTURES

Same pipeline principles as before (real photos as sources where possible, degraded PS1 / VHS style, point
filtered, power-of-two sizes). Create **exactly these names** (other packages reference them), more if useful:

* **Decals** (`Resources/Textures/Decals/`, alpha): `writing_dontlook` ("DON'T LOOK AT HIM"), `writing_hesees`
  ("HE SEES YOU"), `writing_tally` (prison tally marks), `writing_2ndclass` ("2ND CLASS"), `writing_letmeout`
  ("LET ME OUT"), `writing_sorry` ("SORRY MOM"), `writing_cross` (crude crosses), `writing_eyes` (two crudely
  drawn eyes), `writing_run` ("RUN"), `writing_names` (scratched list of names, crossed out),
  `drag_marks_blood`, `scratches_claw`, `newspaper_scatter`, `mold_patch`. All hand-written looking, different
  hands / tools (blood, marker, scratched), degraded.
* **Env** (`Env/`): `wall_wallpaper_floral_faded` (grandmother's room: pale yellow wallpaper with faded flowers),
  `wall_wood_planks_dark2` (dark stained planks), `floor_carpet_dark_pattern`, `wall_wallpaper_stripe_gray`,
  `floor_wood_worn_gray`, `metal_vent_duct` (grimy galvanized duct interior), `concrete_crawlspace` (damp, dirty),
  `ceiling_stained_dark`.
* **Props** (`Props/`): `vent_grate` (alpha cutout louvres / mesh), `butcher_block` (end-grain, cuts, blood),
  `meat_pile` (glossy raw red meat chunks, like the chopping reference), `tv_static_4..7` (more TV frames:
  rolling snow, bars, garbled picture), `tv_testcard` (degraded test card), `crucifix_wood`, `photo_frames`
  (old family photos), `newspaper`, `window_dim_glow` (neutral, desaturated lit window to replace the red glow),
  `lampshade_dirty`.
* **FX** (`FX/`): `item_glow` (white pixelated / dithered radial glow, transparent edges, 32×32),
  `item_sparkle_0..3` (16×16 pixel star twinkle frames), `muzzle_flash_0..1`.
* **UI** (`UI/`): `icon_revolver` (same style as the other icons).
* Make the lighting-related textures neutral (no red glow textures used by default).

Update `Tools/AssetPipeline/textures/README.md` (sources / licences) and the texture section of `Docs/ASSETS.md`.

## 7. CHARACTERS

1. **Omar**: bigger (≈ 2.05–2.10 m), wider shoulders, heavier chest / belly, thicker arms and neck; same design
   (sack mask with two black eye holes and the rope noose, red / black plaid flannel, long dark bloody apron,
   jeans, boots, gloves). Scarier through mass and dirt, faithful to `SourceAssets/omar`.
2. **Cleaver** (`ItemMeshFactory.BuildCleaver`): a big butcher's cleaver like the references — wide rectangular
   blade (~30 × 12 cm) with a hanging hole, bevelled edge, dark wooden handle with rivets, blood.
3. **Omar animation**: walk = slow, heavy, stiff gait; run = the **same gait accelerated** (faster cadence,
   longer stride), **no forward lean**, **minimal arm swing** (cleaver arm hangs with a small sway), slightly
   plastic but not robotic. Attack: **big wind-up** (cleaver raised high overhead ~0.45 s, short hold, violent
   chop ~0.2 s, follow-through); report the new impact time. `ChopMeat` (overhead slam onto the table, loopable
   ~1.1 s, impact at ~0.55 s), `BedLift` (~1.6 s), `Push` (shoulder shove ~0.5 s), more violent `Scream`.
4. **Prisoners**: women (Prisoner2, Prisoner3, Prisoner5) prettier faces and **clearly feminine shapes** (bust,
   waist, hips) while staying low-poly PS1. New skins **Prisoner5..7** from
   `SourceAssets/players/more/EyAqyzjWQAUE23f.webp`: woman in a grey plaid long-sleeve mini dress (short auburn
   bob, black mary-janes, bare legs); boy in a loud colourful patterned 90s shirt and dark jeans; man with dark
   side-parted hair in a white dress shirt and dark slacks. `DisplayName` for each (e.g. "THE CAMERAWOMAN",
   "THE KID", "THE FATHER" — your call). Third-person animation must look right from outside
   (`SourceAssets/players/thirdperson`): upright walk, run with moderate arm swing, crouch walk, no foot sliding,
   no clipping; `Cower`, `CrawlUnder`, `CrawlOut`, `Shoot`.
5. **Grandmother** — implement `GrandmaRig` (keep the API): per `SourceAssets/granny`: white / grey bob hair
   falling over one eye, sallow yellow skin, sunken dark eyes, faded floral long dress (pale yellow, orange / green
   flowers), white socks / shoes, thin arms, bony hands; black wheelchair with big black wheels and footrests.
   Modes: WatchingTv (slumped, head bob, twitching hand), Roaming (hands push the wheels; wheels spin from
   `SetMoveSpeed`), Screaming (leans forward, head jerks, arms up and shaking, mouth open), Dead (slumped forward,
   bloody). `LookAt` turns the head. No colliders (gameplay adds them).
6. **First person**: a **better hand** (stylised, nicer proportions, readable thumb and fingers, sleeve per skin);
   **Zippo lighter** like the first-person reference (bottom-right, speckled olive / brass case, hinged lid
   open / close animation, `Anchor_Flame` at the chimney; the flame is RENDERING's `PsxFx.CreateFlame`);
   **revolver** model (old, dark metal, wooden grip) with first-person hold + `Shoot` recoil, third-person
   `HoldPose.Pistol`. Item textures / wheelchair textures in your atlases.
7. Characters face **+Z** (front). Verify with the preview harness (`Tools/AssetPipeline/characters/preview`):
   render a lineup of all 8 skins + the grandmother, front and back, and compare with the references.
8. Report: Omar's height, eye height, capsule radius suggestion, attack impact time.

## 8. MAP

1. **Visual bugs**: (a) no see-through gaps in floors at doorways (close every gap under door frames between
   room floors; check all floors along wall cuts); (b) no z-fighting / jitter: rugs and carpets are thin boxes
   (≥ 2 cm), decals ≥ 1.5 cm off their surface, no coplanar overlapping faces anywhere (e.g. the rug at the cage
   room spawn); (c) nothing floating or intersecting.
2. **Lighting: grey, simple, bleak.** All light colours neutral (bulbs at most very slightly warm and desaturated),
   no red windows (use `window_dim_glow`), emergency light dim and neutral, neutral ambient / fog. Keep today's
   luminance: `PsxEnvironment.Brightness` = 1 must look like today (that is NIGHTMARE); higher difficulties'
   brightness is applied globally.
3. **Writings**: remove the repeated "help"; place the new varied writings sparingly and logically (cage rooms:
   tally marks / LET ME OUT; basement: crosses / names; hall: HE SEES YOU; grandmother's room: crosses).
   Nothing should look copy-pasted; logical repetition (chairs around a table) is fine.
4. **Grandmother's room**: a new room reachable from the house interior (annex or converted space): dark wood
   or faded floral walls, crosses on the walls, cluttered floor (papers, dirt), an old CRT TV on a stand with
   `PsxMaterials.TvScreen(...)`, dim light mostly from the TV, wheelchair spot. Fill `MapData.Grandma` (Area,
   Room, ChairPose facing the TV, TvScreen, TvSoundPosition, RoamNodes = nav nodes of the ground floor reachable
   without stairs, including her room). Doorways on her routes ≥ 0.9 m wide.
5. **Kitchen butcher routine**: heavy butcher table with a big raw meat pile, cleaver marks, blood (like the
   chopping reference). Fill `MapData.Kitchen` (ChopPose, BlockTop, Meat, `ChopInteract` trigger on
   `Layers.Interactable`, VentArea, VentView). **Vent**: crouch-only duct (inside ≥ 1.2 m tall, ≥ 0.9 m wide;
   Omar's 1.95 m capsule must not fit) from an adjacent room / closet to a grate overlooking the butcher table
   from the side or above. Floor-level vent entrance. No nav nodes inside. Add it to `CrawlSpaces`.
6. **More narrow, claustrophobic spaces**: 2–4 more (crawlspace under the house / porch, a narrow passage behind
   walls between two rooms, cramped pantry / closets, attic crawl if feasible); some crouch-only (safe from Omar),
   some just tight corridors. Register crawlspaces; keep nav consistent.
7. **Cell rooms**: at least 4 rooms spread over the map (the upstairs cage room, a basement holding room, barn
   stalls, an outbuilding), each with up to 4 cage slots (`CageInfo` with `RoomIndex`, listed in
   `CellRooms[i].CageIndices`). **Every cage slot root starts inactive** (gameplay activates the used ones).
   `MapData.Cages` holds every slot (index = network id). Keep `PrisonerSpawns` = the first 4 cage `Inside` poses
   (legacy). Active cages must not overlap props; inactive ones have no colliders.
8. **Beds**: every under-bed hiding spot has the frame + mattress under a `LiftPivot` (on the long edge at floor
   level; `LiftAxis` local; `LiftAngle` ≈ 65°) so it can be tipped up, `HiddenView` under the bed (eye ≈ 0.25 m
   above the floor, looking out the long side), `CrawlStart` (feet beside the bed, facing it), `LifterPose`
   (Omar beside the bed, facing it). At least 4 under-bed spots.
9. **Wardrobes / lockers**: slatted doors with real gaps; `HiddenView` behind the slats sees the room through
   them (no screen overlay will be used any more).
10. **Revolver spots**: 3–5 `GunSpots` in plausible places (bedroom dresser, study desk, attic, shed shelf; not in
    the grandmother's room).
11. **Doors for physics**: every `DoorInfo` has the `Pivot` at the hinge (local Y up), the `Leaf` BoxCollider
    under the pivot (so the leaf width can be derived), `OpenAngle` = maximum signed open angle, a clear swing arc
    (no props), frame stops. Document the convention in the `MapBuilder` header.
12. **Expand** the map where it helps (grandmother's annex, pantry, laundry, attic...) without breaking
    objectives / escape routes; keep draw calls and mesh sizes reasonable. Update the `MapBuilder` header block.
13. **Fix the offline QA harness** (`Tools/AssetPipeline/map/sim`) so it builds against today's code (shims
    inside the harness only) and use it: no floor holes (raycast grid), nav reachability from Omar's spawn with
    gates open, shelter / main gate edge codes intact, new data present, crawlspaces too small for Omar (capsule
    test), coplanar-overlap check, render views of the new areas (grandmother's room, kitchen + vent,
    crawlspaces, cell rooms); report the numbers and save the views under the harness output folder.

## 9. RENDERING

1. **Interference rework**: at `VhsEffect.Interference` = 1 the picture is only **degraded** (chroma bleed, slight
   horizontal line jitter, mild grain, a few thin tracking lines, light desaturation, faint rolling bar) and
   **never hides shapes** — no heavy snow, dropouts or blackouts at this stage; glitch bursts short and subtle;
   damage edges subtle. Target look: `SourceAssets/_frames/firstperson_sheet*.jpg` (readable picture, head-switch
   noise band at the bottom, occasional line glitches, smear when turning).
2. **Ghosting**: subtle frame-blend / motion smear (previous low-res frame blended in), stronger with fast camera
   turns; cheap.
3. **VHS presets** (`VhsEffect.Preset`): Default, Clean, Worn, Camcorder, BlackWhite, Sepia, Off (Off = PS1 look,
   no tape effects). Each is a parameter set; combine with `VhsEffect.UserIntensity`.
4. **Hiding**: no full-screen slat mask over the picture / HUD any more (at most a subtle vignette).
5. **Item glow** (`PsxItemGlow`): Puppet Combo style pixelated glow **around** items (camera-facing dithered
   halo + an occasional pixel sparkle), not tinting / outlining the item; pulses; stronger when `Highlighted`;
   visible ≤ ~7 m; depth tested; `GlobalStrength` 0 = off. Textures `FX/item_glow`, `FX/item_sparkle_0..3`
   (procedural fallback when missing).
6. **Lighter flame** (`PsxFx.CreateFlame`): like the reference: bright, tall, two-tongued flickering flame, white-
   yellow core, orange edges, sways / lags with camera motion, occasional split, soft glow sprite.
7. **Brightness**: implement `PsxEnvironment.Brightness` (scales ambient + light contribution, gentle gamma-like
   curve; 1 = today). Neutral grading (no warm / red tint).
8. **TV screen** (`PsxMaterials.TvScreen(variant)`): animated static / interference shader on an emissive CRT
   material (rolling snow, bars, test-card flicker, garbled picture), variants 0..3, cheap.
9. **Decals / z-fighting**: stronger depth offset in the decal shader; make sure vertex snapping doesn't make
   coplanar rugs / decals jitter relative to floors (e.g. snap decals consistently or not at all).
10. `PsxFx.MuzzleFlash`: flash sprite + light + smoke puff.
11. Review findings: `rendering-ui-5` (pooled sprite keeps a stale `_MainTex`), `rendering-ui-8` (covered by 4).

## 10. UI

1. **Controls / tutorial**: replace the current pages with one compact page for prisoners and one for Omar
   (≤ 8 short lines each: key + 2–4 words) plus a 2–3 line goal summary; nothing repeated between pages.
2. **Graphics settings**: a GRAPHICS section: VHS FILTER (◀ DEFAULT / CLEAN / WORN TAPE / CAMCORDER / BLACK & WHITE /
   SEPIA / OFF ▶, applied live through `Settings.VhsPreset` → `Settings.ApplyDisplay`), VHS intensity,
   resolution (internal height), scanlines, field of view, plus anything RENDERING exposes. Persisted.
3. **Lobby**: host difficulty picker (EASY / NORMAL / HARD / NIGHTMARE + the one-line descriptions of §4) →
   `Session.Settings.Difficulty` + `Settings.Difficulty` + `HostSettingsChanged()`; clients see it. Remove the
   AI-Omar toggle (the AI always plays Omar when nobody picks him). Character list = `GameInfo.PrisonerSkins` + Omar.
   The preview **faces the camera** (`rendering-ui-3`).
4. **HUD**: crosshair = **solid white square** (~3×3 low-res px, centred; hidden in menus); interaction label
   **just below the crosshair** like the reference ("TAKE LIGHTER FUEL", "DRAG (HOLD LMB)"); **STAMINA** label +
   thin outlined bar bottom-left, shown while sprinting / recovering, fades when full; revolver rounds when held;
   keep the minimal OSD. No hiding overlay.
5. **Admin panel**: F10 → password prompt (masked input, REMEMBER toggle, error line) when `!Admin.IsAdmin`,
   else the panel: category tabs (`AdminCategory`), rows from `AdminCmds.All` (lobby / match filtered), toggle
   state from `Admin.IsOn`, argument pickers per `AdminArg` (players from the session, items, world events, endings,
   locations from `Admin.Locations()`, number stepper starting at `Default`, roles, difficulty), ENTER / click runs
   `Admin.Run(...)`, `Admin.Log` at the bottom. Works in lobby and match, keyboard + mouse, blocks gameplay input
   while open (like pause), minimal VHS style, fits 240p. The HUD calls `Admin.DrawOverlay(ui)` each frame when
   `Admin.IsAdmin`.
6. Review findings: `rendering-ui-4` / `rules-audio-ui-runtime-9` (text overflow at 240p), `rendering-ui-6` (HUD
   markers projected with the previous frame's camera), `rendering-ui-3`, `network-flow-8` /
   `rules-audio-ui-runtime-11` (client READY state stale after returning to the lobby).
7. Polish the whole UI, keep the minimalism the owner likes (consistent spacing, alignment, readable at 240p).
   Don't redesign the main menu layout.

## 11. PLAYER

1. **Physical doors** (rewrite `DoorEntity` + host code in `MatchHost.Player.cs` + client code in
   `MatchWorld.Player.cs`):
   * E no longer opens / closes doors. Prisoners aim at a door leaf (≤ ~2.2 m), **hold LMB** to grab, move the
     mouse to swing it (mouse X / Y → tangential push at the grab point relative to the camera, Amnesia /
     Penumbra style). While grabbing, camera look is frozen or strongly reduced. Releasing keeps momentum; a hard
     fling slams the door (slam sound + noise). The door can't pass through the player holding it.
   * Swing from 0 (closed) to |OpenAngle| (stop) in the configured direction, damping, bounce at the stop, latch
     when closed slowly; creak loop with pitch ∝ angular speed (`Snd.DoorCreakLoop` / `MetalDoorCreakLoop`),
     latch / bump / slam sounds; noise: slam 10 m, fast swing 4 m.
   * Locked doors rattle when grabbed; Lockpick (hold E) unlocks; Boarded: crowbar (hold E) as before. Omar: E
     unlocks / smashes as before; `MatchHost.RequestDoor(omarId, door, true)` must unlock a locked door for Omar
     (the AI relies on it), `SmashBoards` stays.
   * **Omar pushes doors open by walking** through them with the same physics: Omar's avatar does not collide with
     unlocked door leaves (e.g. leaf on `Layers.Door`, Omar ignores that layer; locked / boarded leaves switch to a
     layer that blocks him); the host applies a push to any leaf intersecting Omar's capsule along his movement
     (enough to clear him, plus momentum) — for the AI and a human Omar alike; a human Omar's client predicts the
     same push locally.
   * Network: `DoorGrabReq` / `DoorGrab` / `DoorDragReq` / `DoorAngles` / `DoorFx` (100–104). Host simulates
     free doors; the grabber simulates its door and streams angle + velocity (unreliable, 20 Hz); the host relays
     moving doors' angles (quantised, 15–20 Hz); clients interpolate; release hands the momentum to the host.
     `DoorEntity.Open` (angle beyond ~20°) stays for AI / sight logic.
   * The prompt API exposes "DRAG (HOLD LMB)" for the crosshair label; LMB still uses the held item when not
     aiming at a door.
   * Implement `partial void AdminDoors(AdminCmd cmd)` (open / close / unlock all doors).
2. **Hiding**:
   * **Under beds**: entering plays a short first-person move (camera drops at `CrawlStart`, slides under to
     `HiddenView`, ~0.9 s, `Snd.BedCrawlIn`); leaving reverses (`BedCrawlOut`). Other peers see the avatar play
     `CrawlUnder` / `CrawlOut`. Limited look while under (yaw ±70°, small pitch).
   * **Omar finds someone under a bed → he lifts the bed**: Omar plays `BedLift` at `LifterPose`, the bed tips up
     around `LiftPivot` (`BedLift` msg 105, `Snd.BedLift`), the prisoner is exposed and pulled out as today
     (injured / captured); the bed drops back after ~2.5 s. An empty bed is lifted and dropped too.
   * **Wardrobes / lockers**: smooth — the doors swing open while the camera eases inside (~0.6 s) and close;
     **no screen overlay** (don't use `VhsEffect.Hiding` as a mask); easing out on exit. A light muffle is fine.
3. **Stamina** like the reference: ~7 s of sprint, regen after a 1.2 s delay, exhausted state with heavy breathing
   (character voice `Breath`), expose values for the HUD.
4. **Lighter** like the reference: first press opens the lid (`LighterOpen`) and strikes (`LighterFlick`) → flame;
   next press closes it (`LighterClose`). Warm (slightly desaturated) flickering point light with a larger radius
   than today so the room around is lit like in the video; flame sways with mouse look; fuel drain × difficulty
   (`Tuning`, WORLD).
5. **Item highlight**: `PsxItemGlow.Attach` on world items; `Highlighted` while aimed at.
6. **Revolver (player side)**: held + LMB fires when loaded: first-person `Shoot` recoil, local muzzle flash
   (`PsxFx.MuzzleFlash`), local bang (`Snd.GunShot`), `MatchWorld.SendShoot(origin, dir)` (WORLD resolves it).
   Empty → `GunEmpty` click.
7. **Voices**: Hurt / Gasp / Struggle / Breath through `Snd.Voice(skin, line)` (2D for yourself; remote peers hear
   them 3D at the avatar — add a small event in your id range if needed).
8. Crouch fits the map's 1.2 m crawl ducts (crouched capsule ≤ 1.05 m) and standing up is blocked inside.
9. `AdminState`: honour `Noclip` (fly, no collisions; Space up / Ctrl down), `SpeedMultiplier`, `InfiniteStamina`,
   `InfiniteLight` for the local character.
10. Review findings: `rendering-ui-2` (PrisonerController keeps re-applying hiding / damage effects under the
    ending screen), the PrisonerController part of `rules-audio-ui-runtime-7`, `network-flow-11` (owner reports
    light charge with `ChargeReq` 106; the host stores it in `MatchHost.Player.cs`).

## 12. WORLD

1. **Omar behaviour and sound**:
   * Slower patrol, accelerating chase (difficulty table; ramp to full run over ~1.2 s). Used by the AI and by
     `Tuning` for a human Omar.
   * **find always** plays when Omar spots someone; ~55 % of the time a random `scream_1..4` follows 0.4–1.0 s
     later; host decides (synchronised).
   * **Louder**: raise volumes / distances of Omar's sounds (find, screams, cleaver, growl, breath, steps).
   * **Alarm → scream**: when a tripwire siren goes off, Omar screams ~0.6 s later (AI and human, host-driven); the
     AI also investigates.
   * **Footsteps**: Omar always uses `Snd.OmarStep` (muffled, heavy; louder, carry further) via the avatar's
     footstep events; prisoners keep surface steps.
   * **Proximity interference much weaker**: visual `Interference` ≤ ~0.35 near Omar, little extra in a chase,
     static audio ≤ ~0.12 volume, heartbeat fine. Never obscure the view. `rendering-ui-1` (spectating Omar
     must not trigger it) and the ProximityFx part of `rendering-ui-2`.
   * Omar is bigger (CHARACTERS): update `Tuning` (eye height ~1.95–2.0, attack range ~2.1, `AttackWindup` = the
     new impact time; CHARACTERS will report the numbers, use ~0.62 s if unknown).
2. **Kitchen routine**: the AI occasionally (every 3–6 min when calm, ~30 % at a patrol pick) walks to
   `Map.Kitchen.ChopPose` and chops meat for 20–40 s (loop `ChopMeat`, `Snd.OmarChop` / `MeatSquelch`, blood /
   meat FX at `BlockTop`, `KitchenState` 124). While chopping he is **distracted** (sight × 0.35, narrow cone,
   hearing × 0.5); a loud noise within 8 m, an alarm or a spotted prisoner ends it. A human Omar can chop at
   `Kitchen.ChopInteract` ("CHOP MEAT", hold E → `ChopReq` 125).
   **Optional stun**: while he chops, a prisoner behind him (≤ 1.6 m, within ±70° of his back) holding a Bottle or
   Crowbar gets "[E] HIT HIM" (an interactable on Omar's back) → `OmarHitReq` 126 → Omar stunned 8 s (bottle
   shatters / crowbar used up), then he hunts the hitter.
3. **Grandmother** (`GrandmaEntity` + host / client partials):
   * Spawns at `Map.Grandma.ChairPose` (`GrandmaRig`), TV loop at `TvSoundPosition`, occasional mutters.
   * **Stage 1 (in her room)**: sees prisoners in a 120° cone where she faces (range by light like
     `DetectionSystem`, line of sight) or very close (< 2.5 m, any angle) → `GrandmaSpot` then `GrandmaScream`
     variants every 3–4 s while she sees them (mode Screaming) and **alerts Omar** (noise 35 m at her position, AI
     investigates like an alarm, human Omar gets a ping). Noise near her makes her turn towards it.
   * **Stage 2 (roaming)**: starts at the difficulty threshold (progress = milestones done: gate cut, car fuelled,
     battery installed if it started dead, fuse in, radio called, drums fuelled, shelter opened; or night
     progress). Rolls at ~1.0 m/s between `Map.Grandma.RoamNodes` on the nav graph (those nodes only), wheelchair
     loop, pauses, sometimes back to her TV. Same detection; when she sees someone she stops, faces them, screams,
     follows slowly while they stay in view.
   * **Killable only with the revolver** → mode Dead, `GrandmaDeath`, blood burst, silent forever.
   * Network: `GrandmaState` 120 (reliable: mode, pos, yaw, target), `GrandmaSnap` 121 (unreliable ~10 Hz while
     moving). Solid capsule collider (players can't walk through her).
   * Implement `partial void AdminGrandma(AdminCmd cmd)`; honour `AdminState.GrandmaDisabled`.
4. **Revolver (host)**: `ItemSpawner` places exactly one at a random `Map.GunSpots` spot (charge 1 = 2 rounds).
   `ShootReq` 122: validate (sender holds a loaded revolver, origin near their eye), consume 0.5 charge, raycast
   (world + grandmother + Omar): grandmother → dies; Omar → stunned 4 s + growl (never dies); world → sparks.
   `ShotFx` 123 to all: muzzle flash + very loud bang at the shooter (3D, large distance). Noise 45 m.
5. **Difficulty** (§4) applied on every peer at match start from `Settings.Difficulty` of the match:
   `PsxEnvironment.Brightness`, `PsxItemGlow.GlobalStrength`, `Tuning` multipliers (speeds, sight, hearing,
   detection, captures, drain), supplies, starting traps, grandmother threshold. Reset `PsxEnvironment.Brightness`
   to 1 and `PsxItemGlow.GlobalStrength` to 1 when the match is torn down.
6. **AdminState** (host side): GodMode (no hits / captures / traps), Invisible (not detected, noises ignored),
   OmarFrozen / OmarBlind / OmarDeaf / OmarSleepUntil / OmarHuntTarget (AI), ClockPaused (match clock). Implement
   `partial void AdminOmar(AdminCmd cmd, int a, float f, Vector3 adminPos)` (stun / teleports / scream / hunt /
   sleep / chop). Call `AdminState.Reset()` and `PsxFx.ClearBloodDecals()` in `MatchWorld.OnDestroy`.
7. Review findings: `network-flow-7` (accumulate send timers, sequence numbers on avatar state, drop stale /
   duplicate states), `network-flow-10` (remote peers hear 3D versions of others' struggle / lockpick / crowbar /
   pour / bandage / battery / lighter / throw sounds), `map-gameplay-6` (vehicle gate leaves use the map's open
   angles), `map-gameplay-8` (mannequin shuffle identical on every peer), the AmbienceController part of
   `rules-audio-ui-runtime-7` (no gameplay ambience after the ending), `rendering-ui-1`, `rendering-ui-7` (call
   site).

## 13. SESSION

1. **Random spawns** (deterministic from the seed + roster order, identical on all peers): each prisoner gets a
   random cell room (uniform over `Map.CellRooms`; groupings random — all together, all apart, 2+1+1...) and a free
   cage slot in it; only used slots are activated (`CageEntity.SetActive`); **solo = exactly one cage**.
   Prisoners start Caged (existing CagesOpenAt flow). Recapture: `PickCageFor` → the nearest cell room (to the
   capture point) with a free slot (activate one if needed, `CageActive` 140 to all); none → -1 (death). No
   `CellRooms` → fall back to today's behaviour.
2. **AI Omar automatically**: nobody picked Omar → the AI plays him (no "nobody plays Omar" error; the AiOmar
   setting no longer gates it). 5 humans: the fifth must pick Omar or spectate.
3. **Admin backend** (implement `Admin`, keep the API and the `AdminCmds` table; you may add commands):
   * Password check: **PBKDF2-HMAC-SHA256** (implement on `HMACSHA256` for compatibility), iterations **20000**,
     salt (hex) **f1c54b20818ef7029c23df0b0fbb40e0**, dkLen 32; verifier = SHA-256(dk) (hex)
     **c0b4ec3e637dc53a9a5121a5170953f398ac37dd04ba2082684ac661cbab9c9c**. Normalise input: upper case, remove
     spaces and '-'. Test vector (not the real password): "TESTTESTTEST" with the same salt / iterations →
     dk f605b215e97fd4b6e2755a3c1adaec8a240a7d42ddd8ee66568024ece99ab9ef, verifier
     ddd7b8db21cf4a5c3882feb9854a7e854d74b58018b748f0075ed3d6d9bd2073. Add a small dotnet test project under
     `Tools/AdminAuthTest` that checks the algorithm with this vector. The real password is never in the repo.
   * Remember: store the dk (hex) in PlayerPrefs when REMEMBER is on; auto-login at start-up if it verifies.
   * As a client, send `AdminAuthReq` 141 (dk) after joining / logging in; the host checks SHA-256(dk) == verifier
     and answers `AdminAuthResult` 142; the host keeps admin rights per player id; commands from non-admins are
     ignored. The host's own login is local.
   * Execute every command: host-side ones via `AdminCmdReq` 143 (`MatchHost.Admin.cs` in a match; `NetSession`
     in the lobby: ForceStart / SetRole / SetDifficulty / KickPlayer); local ones on the client (noclip / speed /
     stamina / light via `AdminState`, fullbright via `PsxEnvironment`, VHS off via `VhsEffect.Preset`, free camera
     (own MonoBehaviour taking the camera rig in a late LateUpdate), teleports, debug overlays drawn by
     `Admin.DrawOverlay(VhsUI)`). Use the partial hooks `AdminDoors` / `AdminOmar` / `AdminGrandma` for systems
     owned by others; anything else through existing `MatchHost` members. Remote teleports (BringPlayer): host →
     owner message in your range → the owner teleports itself. Feedback through `AdminLog` 144 (e.g. the shelter
     code).
4. **Difficulty plumbing**: the host copies `Settings.Difficulty` into `MatchSettings` and back in
   `HostSettingsChanged()`; it travels in `StartMatch`.
5. Skin assignment for new players uses `GameInfo.PrisonerSkins` (unique per player).
6. Review findings: `network-flow-9` (BuildMatch coroutine after the session ended), `network-flow-12` (OnDestroy
   raising SessionEnded during teardown), `rules-audio-ui-runtime-12` (menu ambience stopped by GoToMainMenu).

---

Review findings referenced above: JSON with descriptions and verified fixes at
`/tmp/claude-0/-home-user-Omar-Prisoners/661f76c0-dc1f-528c-a229-0442a0dae27a/scratchpad/review.json`
(keys `confirmed[].id`, `description`, `failure_scenario`, `verified_fix`).
