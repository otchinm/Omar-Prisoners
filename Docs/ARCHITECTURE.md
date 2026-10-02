# The Prisoners of Omar — architecture

Unity **2022.3 LTS or Unity 6**, **Built-in Render Pipeline**, legacy Input Manager, no third-party packages.
The game is *code-first*: the single scene is empty and everything (map, characters, items, UI, menus) is
generated at runtime by C#. Assets are loaded from `Resources/` by path. This keeps the project
mergeable as text and lets it be compile-checked without the editor (`Tools/CompileCheck/check.sh`).

## Folder / module ownership

```
Assets/PrisonersOfOmar/
  Scripts/Core/        shared enums, constants, layers, DeterministicRandom, MeshBuilder, GeoUtil, SurfaceTag
  Scripts/Net/         UDP transport: NetServer / NetClient / LanDiscovery / NetWriter / NetReader
  Scripts/Rendering/   PSX materials + lights, camera rig (low-res + VHS), anomalies, FX, sky, preview renderer
  Scripts/Characters/  procedural humanoids (prisoners, Omar, mannequins, corpses), procedural animation,
                       first-person arms, item models
  Scripts/Map/         MapBuilder (the Base of the Second Class), MenuSceneBuilder, NavGraph, prop builders
  Scripts/Gameplay/    session / match flow, players, Omar, AI, items, interactions, traps, stealth, endings
  Scripts/UI/          VHS immediate-mode UI + all screens (menu, lobby, HUD, inventory, endings)
  Scripts/Audio/       AudioManager (categories, ducking, music, 3D one-shots, loops)
  Scripts/Editor/      import settings, project setup, menu tools
  Resources/Shaders/   all shaders (+ .cginc) — in Resources so Shader.Find works in builds
  Resources/Textures/  see Docs/ASSETS.md
  Resources/Audio/     see Docs/ASSETS.md
Tools/AssetPipeline/   python scripts that generate textures / audio from CC0 photos + synthesis
Tools/CompileCheck/    dotnet compile check against Unity reference assemblies
SourceAssets/          the original files supplied by the user (reference images, sounds)
```

A module may only change files in its own folder. Public types listed in the stub files of
Core / Net / Rendering / Characters / Map are **contracts**: keep every public signature, you may add more.

## Conventions

* Namespaces: `PrisonersOfOmar` (Core), `PrisonersOfOmar.Net`, `.Rendering`, `.Characters`, `.Map`, `.Gameplay`, `.UI`, `.Audio`.
* C# 9 at most, no `record`/`init`, no nullable annotations. Target .NET Standard 2.1 API.
* Never `UnityEngine.Random` / `System.Random` for anything that must match on all peers — use `DeterministicRandom`.
* Units: meters, seconds, degrees. Y up, +Z forward. Character roots stand on the ground at their origin.
* Layers: see `Core/GameInfo.cs` (`Layers`). Doors on `Layers.Door`, static level on `Layers.World`,
  foliage on `Layers.Foliage` (no colliders), interaction triggers on `Layers.Interactable`.
* All renderers use materials from `PsxMaterials` (never Standard shader). No Unity `Light` components:
  use `PsxLight` (custom PS1 vertex lighting).
* No real shadows. Shadow casting off on every renderer (MeshBuilder.Build does it).
* Mesh winding: clockwise front faces (see MeshBuilder docs).
* Avoid APIs removed or renamed in Unity 6: use `FindFirstObjectByType` / `FindObjectsByType`, no `Rigidbody.velocity`
  (use AddForce with ForceMode.VelocityChange), no `Object.FindObjectOfType`.

## Rendering pipeline (summary)

`PsxCameraRig` renders the world at ~240 lines into `LowRes` (point filtered), the view model camera renders
first-person arms on top, the UI draws into `LowRes` through `DrawOverlay`, then the VHS shader blits to the
screen (tape wobble, chroma bleed, noise, scanlines, head-switching, vignette, Omar interference…).
World shaders do vertex snapping, affine texture mapping, per-vertex lighting from `PsxLight`s, per-vertex fog,
and the global "anomaly" warping driven by `AnomalySystem`.

## Networking (summary)

Host-authoritative game state over a custom UDP transport (reliable + unreliable channels).
Owner-authoritative movement: each client simulates its own character and streams its state; the host relays.
Every peer builds the same map from the match seed, so static objects are identified by their index in the
`MapData` lists. Up to 4 prisoners + 1 Omar (human or AI bot on the host).
