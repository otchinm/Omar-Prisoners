# Asset contract — names every module agrees on

All runtime assets live under `Assets/PrisonersOfOmar/Resources/` and are loaded by path **without extension**
(`Resources.Load`). Texture paths below are relative to `Resources/` (e.g. `Textures/Env/wall_wallpaper_blue`).

Style rules for every texture (see `Tools/AssetPipeline/`):
* Built from **real photographs** (CC0: scikit-image sample photos `brick`, `grass`, `gravel`, `coffee`…,
  Poly Haven HDRIs shipped in the `@pmndrs/assets` npm package) and procedural layers, then **heavily degraded**:
  down-sampled to PS1 sizes, posterized to ~5 bits/channel, JPEG-crushed, grime / stains / water damage,
  slightly desaturated, dark. Point filtered in game, no mip maps.
* Tiling textures must tile seamlessly. Sizes are powers of two (64 / 128 / 256).
* Cutout textures are RGBA PNG with hard (0 / 255) alpha.

## Textures/Env (128×128 tiling unless noted)

| name | description |
|---|---|
| wall_wallpaper_blue | pale blue wallpaper with small dark-blue floral / diamond motifs, brown stains (clock bedroom) |
| wall_wallpaper_green | sickly green-gray damask wallpaper, peeling, water damage (hallways) |
| wall_wallpaper_rose | faded lilac / rose wallpaper, big dark grime blotches (study / upper rooms) |
| wall_wallpaper_yellow | nicotine-yellow wallpaper, vertical stripes, mold at the bottom (cage room) |
| wall_wainscot | dark blue-gray painted wooden wainscot panel (lower 1 m of walls) |
| wall_plaster_dirty | purple-gray rough plaster with dark grime (basement upper walls) |
| wall_brick_basement | old whitewashed brick, dirty (basement lower walls, tunnel) |
| wall_wood_planks | vertical weathered gray wooden planks (supply closet, shed inside) |
| wall_wood_dark | dark stained wood paneling (dining room, radio room) |
| wall_tile_red | terracotta / red square tiles with white grout, slightly warped (mannequin room) |
| wall_tile_white_dirty | white bathroom tiles, cracked, blood smears & grime |
| wall_concrete_block | gray cinder blocks (restroom building, bunker) |
| wall_siding_white | white wooden clapboard exterior siding, dirty, peeling (house exterior) |
| wall_barn_red | weathered red barn planks, vertical |
| wall_corrugated_metal | rusty corrugated sheet metal (shed exterior, silo) |
| floor_wood_planks | worn gray-brown floorboards |
| floor_wood_dark | dark varnished floorboards, scratched |
| floor_linoleum_dirty | dirty checkered linoleum (kitchen) with stains |
| floor_concrete | stained cracked concrete (basement, shed, tunnel) |
| floor_tile_dirty | grimy small floor tiles (bathroom) |
| floor_carpet_stained | old brown/red carpet with stains |
| ceiling_plaster_stained | off-white plaster ceiling with yellow-brown water stains |
| ceiling_wood | wooden plank ceiling / beams underside |
| ground_dirt | compacted dark dirt yard with small stones |
| ground_grass_dead | dry dead grass + dirt |
| ground_gravel | gravel driveway |
| ground_asphalt | dark parking lot asphalt, oil stains |
| ground_asphalt_cracked | cracked old road asphalt |
| roof_shingles | dark mossy asphalt shingles |
| roof_tin_rusty | rusty corrugated tin roof |
| metal_rusty | rusty steel |
| metal_painted_green | chipped army-green painted metal |
| metal_dark | dark gunmetal |
| metal_galvanized | dull galvanized steel (fence posts, silo bands) |
| wood_raw | fresh-ish pine beam wood |
| wood_furniture | dark varnished furniture wood with wear |
| wood_painted_white | white painted wood, chipped (porch rails, window frames) |
| fabric_mattress_stained | stained mattress ticking |
| fabric_sofa | ugly brown floral upholstery |
| fabric_dirty | generic dirty cloth / curtains |
| concrete_rough | rough concrete (silo, bunker, steps) |
| bark | dark tree bark |
| meat | raw meat / gore chunks |
| hay | hay bale straw |
| tarp_blue | dirty blue tarp |
| chainlink (RGBA, 64×64) | chain-link fence diamond pattern, cutout, tiles every 1 m |
| barbed_wire (RGBA, 128×32) | barbed wire strand, cutout |

## Textures/Decals (RGBA, cutout or soft alpha)

blood_pool (128), blood_splatter_1, blood_splatter_2, blood_splatter_3 (128), blood_smear (128×64), blood_handprint (64),
graffiti_scrawl_1, graffiti_scrawl_2 (128, crude black occult-looking scribbles / tally marks), grime (128, soft dark blotch),
water_stain (128), parking_line (64×16, yellow paint stripe, worn), road_dashes (64×16, white worn dash),
writing_help (128×64, "HELP" scrawled in blood), writing_omar (128×64, "OMAR SEES" scrawled), cracks (128).

## Textures/Props

| name | size | description |
|---|---|---|
| wardrobe_front | 128×256 | two-door paneled dark wood wardrobe front (doors split at u=0.5) |
| dresser_front | 128×128 | chest of drawers front (3–4 drawers, brass handles) |
| clock_face | 64×64 | grandfather clock face (roman numerals, yellowed) |
| clock_body | 64×256 | grandfather clock case front (glass pendulum window) |
| radiator | 128×64 | cast iron radiator fins |
| tv_static_0 … tv_static_3 | 64×64 | CRT screen frames with static / faint face |
| tv_body | 64×64 | wood-grain CRT TV case with knobs |
| fridge_front | 64×128 | old dirty fridge front, rust, magnets |
| portrait_omar | 64×128 | framed creepy oil portrait of a sack-masked figure |
| painting_landscape | 128×64 | dark stormy landscape painting in frame |
| sign_fallout | 64×64 | yellow/black fallout shelter radiation sign |
| sign_restricted | 128×64 | white road sign: "PRIVATE PROPERTY / NO TRESPASSING / VIOLATORS WILL BE SHOT" |
| sign_road | 128×64 | white road sign in the style of the reference: "DEAD END" / "NO EXIT AHEAD" "$1000 FINE FOR TRESPASSING" |
| barrel_water | 64×64 | black plastic barrel label "DRINKING WATER" (wraps around) |
| barrel_fuel | 64×64 | red rusty fuel drum with "FLAMMABLE" stencil (wraps around) |
| bucket_food | 64×64 | white bucket wrap with green "FOOD SUPPLY" label |
| box_cardboard | 64×64 | dirty cardboard box face |
| crate_wood | 64×64 | wooden crate |
| car_body | 128×128 | faded maroon sedan paint with rust / dirt (tiles over the body) |
| car_body_wreck | 128×128 | rusted-through wreck paint |
| car_front | 128×64 | sedan front: grille + two headlights + plate |
| car_rear | 128×64 | sedan rear: tail lights + trunk + plate |
| car_side | 256×64 | sedan lower body side (rocker to beltline, u from the front): door seams, locks, rust and road dirt round the wheel arches (the windows, arches and chrome are geometry) |
| car_dash | 128×32 | dashboard face seen from the front seat: vents, hooded gauges, radio, heater sliders, glove box |
| car_tire | 128×64 | atlas: sidewall + rusty rim (left half, cylinder caps), tread (right half, round the tyre) |
| car_glass | 64×64 | tinted car glass: reflections, dust film, water spots (tiles) |
| car_glass_broken | 64×64 | wreck glass: cracks, holes, mud (tiles) |
| car_engine | 128×64 | atlas: engine from above (left), battery top / labelled side (right) |
| enamel | 64×64 | old appliance / tub enamel, chipped to rusty iron (tiles) |
| porcelain | 64×64 | crazed bathroom porcelain with water lines (tiles; sink, toilet, plates) |
| plastic | 64×64 | scuffed neutral plastic, coloured by the material tint (tiles) |
| trash_bag | 64×64 | black bin-bag creases (tiles) |
| bottle | 32×64 | glass bottle round its axis: label band, neck foil (tinted green / brown) |
| jar | 32×64 | jar of something pickled, rusty lid on top |
| bone | 64×64 | stained, cracked bone (tiles) |
| coal | 64×64 | coal lumps (tiles) |
| skin_dead | 64×64 | waxy bruised dead skin (tiles) |
| mirror | 64×64 | tarnished mirror, desilvered edges, crack |
| stove_front | 64×64 | gas stove front: knobs, oven door window, drawer |
| wax | 32×32 | candle wax with runs and soot (tiles) |
| keypad | 32×64 | metal keypad with 0–9 buttons and small LED |
| fusebox | 64×64 | gray metal fuse box face (labels, empty slot) |
| radio_set | 128×64 | military radio front panel (dials, meters, frequency display) |
| generator | 64×64 | old green generator side |
| cage_bars (RGBA) | 64×64 | rusty steel cage mesh / bars cutout |
| mannequin_burnt | 128×128 | charred mannequin skin (black/brown burnt plastic, ash) |
| flamingo_pink | 32×32 | faded pink plastic |
| water_tower_tank | 128×128 | red rusty tank panels with rivets |
| silo_metal | 128×128 | ribbed galvanized silo wall, rust streaks |
| utility_pole | 32×128 | creosote wood pole |
| window_red_glow | 64×64 | window seen from outside with red lit curtain behind (emissive) |
| window_dark | 64×64 | dark dirty glass in white frame |
| window_boarded | 64×64 | window boarded with planks |
| door_wood | 64×128 | paneled interior door (brass knob on the right) |
| door_wood_dirty | 64×128 | same, filthy, bloody hand prints |
| door_front | 64×128 | exterior door with small glass window |
| door_metal_shelter | 64×128 | heavy gray metal door with fallout sign and rust (like basement reference) |
| door_metal | 64×128 | plain metal door |
| door_planks | 64×128 | shed plank door |
| stairs_wood | 64×64 | stair tread wood |
| bed_frame_metal | 64×64 | rusty painted metal |
| table_wood | 128×128 | scratched table top |
| shelf_metal | 64×64 | black metal shelving |
| paper_note | 64×64 | torn yellowed paper (wall note, blank – text added in game) |
| hay_bale | 64×64 | hay bale side |
| meat_slab | 64×64 | raw meat on table |
| book_spines | 128×64 | shelf of old book spines |
| boards_nailed | 64×128 | planks nailed across a doorway |
| porch_screen | 64×64 | rusty insect screen (RGBA soft) |

## Textures/Foliage (RGBA cutout)

tree_dead_1, tree_dead_2, tree_dead_3 (256×256: bare branched deciduous silhouettes, gray-black, like the reference field trees),
tree_pine_1, tree_pine_2 (128×256: dark narrow conifer / poplar silhouettes),
tree_big (256×256: big dark leafy tree, like the one beside the house),
bush_dark_1, bush_dark_2 (128×128), grass_tall_1, grass_tall_2 (128×128: tall dry grass blades),
cornstalks (128×256: dead corn stalks), weeds (64×64),
treeline (1024×256: wide strip of dense forest silhouettes for the far boundary ring).

## Textures/Sky

sky_night (1024×256 cylindrical panorama: overcast night sky, very dark blue-gray clouds, faint moon glow),
sky_menu (512×128 darker variant).

## Textures/FX

flame_0…flame_3 (32×64 RGBA, lighter flame frames), fire_0…fire_3 (64×128 big fire frames), smoke (64×64 soft),
spark (16×16), glow (64×64 soft radial), blood_drop (32×32), blood_spray (64×64), explosion_0…explosion_3 (128×128),
noise_rgb (256×256 random RGB), noise_gray (256×256), scratches (256×256 film / tape scratches, grayscale),
blob_shadow (64×64 soft dark radial alpha), glint (16×16), dust (32×32), glass_shard (16×16).

## Textures/UI

| name | description |
|---|---|
| title_logo | RGBA ~512×256: "THE PRISONERS OF OMAR" in big bold condensed red letters with ragged edges / VHS bleed (style of the reference menu) |
| title_logo_small | RGBA ~256×64 single line version |
| font_vhs + font_vhs.json | monospace bitmap font atlas (white glyphs on transparent), ASCII 32–126 plus ▶ ◀ ■ ● ▲ ▼; JSON: `{"cellW":..,"cellH":..,"cols":..,"first":32,"glyphs":"<chars in atlas order>","advance":..,"lineHeight":..}` |
| cursor | 16×16 RGBA pixel arrow |
| arrow_left, arrow_right | 16×16 RGBA white triangles (inventory) |
| icon_<item> | 32×32 RGBA pixel icons for every ItemType (lowercase names: icon_lighter, icon_lighterfuel, icon_bandages, icon_flashlight, icon_batteries, icon_soundmeter, icon_boltcutters, icon_carkeys, icon_gascan, icon_carbattery, icon_fuse, icon_cagekey, icon_lockpick, icon_crowbar, icon_bottle, icon_pills) |
| note_paper | 256×256 torn yellowed paper for the note reader |
| vignette_mask | 512×256 RGBA: black with two ragged eye holes (Omar's sack mask view) |
| vignette_blood | 512×256 RGBA: red-black blood at the edges, clear center |
| vignette_dark | 512×256 RGBA: soft black edges |
| vhs_cover | ~512×400: a fake VHS box cover for "THE PRISONERS OF OMAR" (sack-masked man with cleaver, the house, red title) used on the boot / credits screen |
| ending_road, ending_car, ending_tunnel, ending_signal, ending_ashes, ending_caught, ending_dawn | 256×144 degraded "photo" stills for ending cards (night road, headlights, tunnel exit, helicopter searchlight, burning fence, dark cage, gray dawn over the field) |
| panel | 32×32 9-slice dark translucent panel |

## Audio (Resources/Audio, .ogg unless noted)

User supplied (copied verbatim, never re-encoded except where noted):
* `Audio/Music/menu_theme` (mp3) — main menu / lobby / settings / ending screens only.
* `Audio/Omar/alarm` — tripwire siren. `Audio/Omar/find` — Omar detects a prisoner.
* `Audio/Omar/scream_1` … `scream_4` — Omar's four screams.
* `Audio/Stingers/screams_long` (mp3) — long scream bed (capture / Omar wins ending).

Generated (Tools/AssetPipeline/audio):

* UI: `Audio/UI/ui_move`, `ui_select`, `ui_back`, `ui_error`, `ui_type`, `tape_insert`, `tape_eject`, `tape_play`, `tape_stop`, `tape_rewind_loop`, `inventory_open`, `inventory_close`, `inventory_scroll`.
* Ambience loops (seamless): `Audio/Ambience/amb_exterior_loop`, `amb_house_loop`, `amb_basement_loop`, `amb_barn_loop`, `amb_tunnel_loop`, `amb_restroom_loop`, `amb_menu_loop`,
  `static_loop` (quiet dirty-tape hum, crackle and faint garbled radio, only while Omar is in sight), `static_heavy_loop` (denser, when he is close), `chase_loop`, `anomaly_loop`,
  `fluorescent_buzz_loop`, `tv_static_loop`, `radio_static_loop`, `generator_loop`, `fire_loop`, `car_idle_loop`,
  `helicopter_loop`, `flame_loop`, `omar_breath_loop`, `wind_gust_loop`, `windmill_creak_loop`.
* Ambience one-shots: `Audio/Ambience/creak_1`…`creak_4`, `distant_scream_1`…`distant_scream_3`, `thump_1`…`thump_3`,
  `metal_scrape_1`, `metal_scrape_2`, `whisper_1`, `whisper_2`, `thunder_1`, `thunder_2`, `drip_1`…`drip_3`, `phone_ring`, `dog_howl`.
* Footsteps: `Audio/Steps/<surface>_<1..4>` for surface ∈ {wood, concrete, dirt, grass, metal, asphalt, tile, carpet, gravel}; `Audio/Steps/omar_<1..4>` (muffled, heavy body thumps that carry through walls).
* Player: `Audio/Player/heartbeat`, `breath_heavy_1`…`3`, `hurt_1`…`3`, `body_fall`, `cage_rattle`, `struggle`, `gasp`, `bed_crawl_in`, `bed_crawl_out`.
* Character voices (gen_voices.py): folders Audio/Voices/prisoner1 … prisoner8 (prisoner8 = the secret prisoner), each with hurt_1..3, scream_1, scream_2, breath_1..3,
  gasp_1, gasp_2, struggle_1, struggle_2, death_1 — eight distinct degraded VHS voices cut from the human screams in
  the user's recording (never Omar's), re-pitched / coloured per character (`Snd.Voice(skin, line)`).
* Items: `Audio/Items/lighter_open`, `lighter_flick`, `lighter_close`, `fuel_pour`, `bandage_rip`, `flashlight_click`, `battery_insert`,
  `item_pickup`, `item_drop`, `item_equip`, `bottle_throw`, `glass_break`, `key_unlock`, `locked_rattle`, `lockpick`, `crowbar_pry`,
  `wood_break`, `bolt_cut`, `chain_drop`, `soundmeter_tick`, `pills`, `gun_shot`, `gun_empty`, `gun_cock` (the revolver),
  `vent_screw_1`…`3` (screwdriver turning a rusty screw), `vent_screw_drop`, `vent_cover_off` (the cage room vent cover tips over),
  `vent_grate_fall` (the kitchen ceiling grate: pops loose, hisses down in dust, dull heavy clang at 0.84 s, rattles).
* World: `Audio/World/door_open_1`, `door_open_2`, `door_close_1`, `door_close_2`, `door_slam`, `metal_door_open`, `metal_door_close`,
  `wardrobe_open`, `wardrobe_close`, `gate_creak`, `cage_open`, `cage_close`, `keypad_beep`, `keypad_wrong`, `keypad_ok`,
  `shelter_door_open`, `fuse_insert`, `power_on`, `power_off`, `radio_tune`, `radio_sos`, `radio_voice`, `car_door`, `car_crank_fail`,
  `car_start`, `car_drive_away`, `gas_pour`, `hood_open`, `explosion`, `fire_whoosh`, `helicopter_flyby`, `fence_breach`, `tv_on`,
  `door_creak_loop_1`, `door_creak_loop_2`, `metal_door_creak_loop` (pitch follows the door speed), `door_latch_1`, `door_latch_2`,
  `door_bump_1`, `door_bump_2`, `door_unlock`, `wardrobe_enter`, `wardrobe_exit`, `drawer_open`, `drawer_close`.
* Omar: `Audio/Omar/cleaver_swing_1`, `cleaver_swing_2`, `cleaver_hit_1`, `cleaver_hit_2`, `cleaver_hit_wall`, `growl_1`, `growl_2`,
  `trap_place`, `trap_snap`, `tripwire_snap`, `hiding_rip`, `grab`, `chop_1`…`3` (cleaver into meat on the butcher block),
  `meat_squelch_1`, `meat_squelch_2`, `windup_1`, `windup_2` (attack wind-up grunt), `stunned`, `door_push`, `bed_lift`.
* Grandmother (gen_grandma.py): `Audio/Grandma/scream_1`…`3`, `spot`, `mutter_1`…`3`, `death` (from the women's screams in the
  user's recording: natural pitch, uneven old vibrato, wet rasp, VHS), `wheelchair_loop`, `tv_loop`.
* Stingers: `Audio/Stingers/sting_spotted`, `sting_jumpscare`, `sting_capture`, `sting_death`, `sting_anomaly_1`, `sting_anomaly_2`,
  `sting_escape`, `sting_ending_bad`, `sting_ending_good`, `static_burst_1`…`3`, `vhs_glitch_1`…`3`, `drone_hit_1`, `drone_hit_2`, `heartbeat_fast`.
