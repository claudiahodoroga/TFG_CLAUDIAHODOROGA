# Galatea — Technical Documentation

This file is the technical companion to `MEMORIA_TFG`. The thesis explains the
research question, design rationale, and validation results in prose; this
document describes the codebase that implements those decisions. Both should
be read together: the thesis says *why*, this file says *what* and *where*.

---

## 1. Project Overview

**Galatea: Un Sistema de Cuina Creativa en l'Espai** is a TFG (final degree
project) for the Grau en Disseny i Desenvolupament de Videojocs at the
Universitat de Girona.

The game is a first-person space prototype (~30–60 min) in which the player
takes the role of an astronaut-researcher aboard a small ship, experimenting
with alien plants to determine how they can be consumed by humans. The core
mechanic is a **parametric flavor system**: ingredients have six numeric
flavor attributes that are modified by cooking processes; an alien creature
reacts emotionally to the resulting dishes without ever telling the player
whether a dish is "good" or "bad."

The thesis this project validates: **a cooking game where the player can try
anything is more faithful to the cooking experience, and more engaging, than
existing prescriptive recipe-based systems.**

- Author: Claudia Rebeca Hodoroga
- Tutor: Antoni Bardera Reig (anton.bardera@udg.edu)
- Submission: June 2026 (2nd convocation), defence June 26 2026
- Grade weighting: Aesthetics 35%, Mechanics 30%, Technology 25%, Narrative 10%

---

## 2. Technical Stack

- **Unity** 6000.3.6f1 (Unity 6) with **Universal Render Pipeline** v17.3.0
- **Input** — New Input System (`com.unity.inputsystem` 1.18.0)
- **Language** — C# / .NET, with `Galatea.*` namespaces per layer
- **IDE** — JetBrains Rider (student licence)
- **3D content** — Blender 4.x → FBX, URP materials
- **VCS** — Git + GitHub
- All third-party software is free for academic use (Unity Personal, Blender,
  Rider student, Git).

---

## 3. Architecture

The project is organised into three layers with strict separation of
responsibilities. The same separation is described in **section 7.1** of the
thesis.

```
Galatea.Data         Pure C# — no UnityEngine dependencies in the value types.
                     FlavorProfile, IngredientInstance, FlavorAnalysis,
                     DishResult, EmotionThresholds, FlavorCalculator (static).

ScriptableObjects    Authorable content as project assets — IngredientData,
                     DiscreteVariant, ContinuousVariant, EmotionalResponse.
                     Editable from the Inspector; no code changes needed to
                     add a new ingredient, process or emotion.

MonoBehaviours       Scene components that orchestrate runtime behaviour:
Galatea.Systems      CookingStation, StationSlot, PlatingStation, DishVessel,
Galatea.Player       CleanupStation, IngredientBasket, PickupItem,
Galatea.Creature     SoundManager, PlayerController, InteractionSystem,
Galatea.UI           CreatureEmotionController, VisorHUD, FlavorMapGraphic,
                     Crosshair.
```

MonoBehaviours **orchestrate** but do not compute. Every flavour-math
operation is centralised in the static `FlavorCalculator` so that the entire
numeric model is testable in isolation, with no Unity scene needed.

### Folder layout

```
Assets/
├── Scripts/
│   ├── Data/          pure C# value types + ScriptableObjects
│   ├── Systems/       cooking, plating, audio, item carrying
│   ├── Player/        controller + interaction state machine
│   ├── Creature/      emotion controller
│   ├── UI/            HUD + radar chart + crosshair
│   └── Editor/        editor-only tooling
├── ScriptableObjects/
│   ├── Ingredients/   IngredientData assets
│   ├── Processes/     DiscreteVariant / ContinuousVariant assets
│   └── Emotions/      EmotionalResponse assets
├── Prefabs/           Stations, ingredients, creature, dish vessels
├── Scenes/GameScene.unity
├── Materials/  Models/  Textures/  Shaders/  Fonts/  SFX/
```

---

## 4. Data Layer

All types in `Galatea.Data` are pure C#. They never reference `UnityEngine`
behaviour (some structs are serialisable so they survive the Inspector).

### Enums

```csharp
// Physical shape of an ingredient. Changed only by cut/extraction processes.
enum Form { Whole, Chopped, Diced, Julienned, Ground, Juiced, Blended }

// Heat progression. Independent of Form; changed only by continuous cooking.
enum CookState { Raw, Sauteed, Caramelized, Burnt, Boiled, Mush, Fried, Baked }

// Process family — DiscreteVariant is typically Cut; ContinuousVariant is
// DryHeat or WetHeat (Extraction is reserved for a future Juicer/Blender).
enum ProcessCategory { Cut, DryHeat, WetHeat, Extraction }

// Reserved authoring tags; not read at runtime in the MVP. Kept on the data
// layer so the SpecialBehavior system (post-MVP, section 10.4 of the thesis)
// can be re-introduced without a data migration.
[Flags] enum IngredientTag { None, Sweet, Fibrous, Volatile, Aqueous, Acidic, Starchy }

enum PlateType   { FlatPlate, Bowl, Tray }
enum FlavorType  { Sweet, Acidic, Bitter, Salty, Spicy, Neutralizer }
enum EmotionType { Cozy, Refreshed, Spicy, Delighted, Confused, Disappointed, Disgusted }
```

The split between `Form` and `CookState` is a deliberate design decision —
see **section 7.2** of the thesis. They evolve independently; mixing them
into a single enum was the source of a class of visual bugs (cooking an
already-chopped ingredient reverted its mesh to "whole" because the chop
state was lost).

### Value types

`FlavorProfile` — serialisable struct, six float attributes in `[0, 10]`:

```csharp
struct FlavorProfile { float sweet, acidic, bitter, salty, spicy, neutralizer; }
```

It exposes `Zero`, `Clamp()` and a debug-friendly `ToString()`. Every
calculation returns a clamped profile.

`IngredientInstance` — runtime state of one ingredient throughout its
lifecycle (basket → station → plate):

```csharp
class IngredientInstance
{
    IngredientData      source;          // immutable template
    FlavorProfile       currentProfile;  // starts as source.BaseProfile
    Form                Form;            // property; fires OnFormChanged
    CookState           CookState;       // property; fires OnCookStateChanged
    float               cookingProgress; // 0–1 during continuous cooking
    bool                IsBurnt;         // CookState == Burnt OR progress >= 1
}
```

`FlavorAnalysis` — computed from a `FlavorProfile`:

```csharp
class FlavorAnalysis
{
    List<FlavorType> dominantFlavors;  // top 1 or 2 within codominantTolerance
    float            balanceScore;     // 1 / (1 + variance)
    float            intensity;        // sum of all six values
    bool             isBurnt;
}
```

`DishResult` — immutable plate snapshot built when the player picks the
finalised dish off the plating station:

```csharp
class DishResult
{
    IReadOnlyList<IngredientInstance> Ingredients;
    FlavorProfile                     CombinedProfile;
    FlavorAnalysis                    Analysis;
    PlateType                         Vessel;
}
```

`EmotionThresholds` — plain struct populated by `CreatureEmotionController`
from its `[SerializeField]` fields and passed into
`FlavorCalculator.EvaluateEmotion`. All thresholds are Inspector-tunable.

### `FlavorCalculator` (static)

Centralises every flavour calculation. No MonoBehaviour contains flavour
maths; any transformation passes through this class.

| Method | Contract |
|---|---|
| `Combine(List<IngredientInstance>)` | Per-axis additive sum of every `currentProfile`, clamped to `[0,10]`. |
| `Analyze(profile, isBurnt = false)` | Dominant flavours from the 5 tasteable axes (neutralizer excluded); empty if 3+ axes tie. Intensity = sum of 5 axes − neutralizer (floored at 0). Balance = `1 / (1 + variance)` over the 5 tasteable axes. |
| `ApplyDiscreteModifier(profile, multiplier, modifier)` | Cut response. `profile * multiplier + modifier`, clamped. |
| `ApplyContinuousDelta(profile, delta, dt)` | One cook tick. `profile + delta * dt`, clamped. |
| `EvaluateEmotion(analysis, responses, thresholds)` | Priority-ordered rule walk; returns the first matching asset (Confused is the always-fallback). |

---

## 5. ScriptableObjects

`IngredientData` — template for an ingredient type. **Owns the chemical
response**: how this ingredient transforms under each process. Stations
describe the environment; the ingredient defines the reaction.

```
string         IngredientName
string         Description
IngredientTag  Tags                 (reserved; not read at runtime)
FlavorProfile  BaseProfile

// Cut response — applied once on a DiscreteStation's completion.
FlavorProfile  CutMultiplier        (per-attribute multipliers, default 1)
FlavorProfile  CutModifier          (additive offsets)

// Dry-heat response — per-tick delta; selected by ContinuousVariant.Category.
FlavorProfile  DryHeatStage1Delta / 2Delta / 3Delta

// Wet-heat response — per-tick delta; same per-stage layout.
FlavorProfile  WetHeatStage1Delta / 2Delta / 3Delta

Sprite              Icon
string              IconGlyph         (single Unicode char for the HUD)
GameObject          Prefab
List<MeshOverride>  meshOverrides     ((Form, CookState) → Mesh)
```

`GetMesh(Form, CookState)` resolves the right mesh through a four-tier
fallback chain (exact → same form + Raw → chopped family → liquid family),
so the visual stays consistent even when a specific cooked variant has not
been authored yet.

`ProcessVariant` (abstract) — base for every process option.

```csharp
abstract class ProcessVariant : ScriptableObject
{
    string          VariantName;
    string          Description;
    ProcessCategory Category;          // Cut | DryHeat | WetHeat | Extraction
}
```

`DiscreteVariant : ProcessVariant` — one-shot cut process. Adds
`Form ResultForm`, written to the ingredient on completion (CookState is
untouched — chopping doesn't cook).

`ContinuousVariant : ProcessVariant` — per-tick heat process. Defines the
*environment* (stage CookStates, thresholds, heat curve). Per-tick flavour
deltas live on `IngredientData` and are selected by `Category`.

```
CookState  ResultCookState        // Stage 1 base
CookState  Stage2CookState        Stage2Threshold ∈ [0,1]
CookState  Stage3CookState        Stage3Threshold ∈ [0,1]
float      HeatLevelMultiplier    // cookingProgress accumulation rate at max heat
```

`EmotionalResponse` — one mappable emotion outcome.

```
EmotionType  Emotion
Gradient     ColorGradient    // creature peak colour sampled at t = 0.5
string       ReactionLine     // HUD narration; empty → hard-coded default
```

---

## 6. Subsystems

### 6.1 Flavour system — `FlavorCalculator` rules

**Intensity** is the sum of the five tasteable axes (sweet + acidic + bitter
+ salty + spicy), minus neutralizer: `intensity = max(0, rawIntensity -
neutralizer)`. Neutralizer dampens the overall perceived strength of a dish
without affecting individual flavour values or balance.

**Balance** is `1 / (1 + variance)` over the five tasteable axes only
(neutralizer is excluded). It measures how evenly the five flavours are
distributed.

**Dominant detection** considers only the five tasteable axes (neutralizer
excluded). If 3 or more axes are within `codominantTolerance = 1.0` of the
maximum, the dish has *no clear dominant* and the dominant list is empty —
this routes the dish to Confused rather than arbitrarily picking a winner
from a many-way tie.

Emotion evaluation walks the rules in priority order; the first asset in the
`emotionalResponses` list whose `Emotion` matches a triggered rule wins.
Confused is the always-fallback.

| Priority | Emotion | Trigger condition | Default threshold |
|---|---|---|---|
| 1 | Disgusted    | `isBurnt OR balanceScore < disgustMaxBalance` | 0.10 |
| 2 | Disappointed | `intensity < disappointedMaxIntensity` | 3 |
| 3 | Delighted    | exactly 2 co-dominant flavours, `balanceScore > delightedMinBalance`, `intensity >= delightedMinIntensity` | 0.15 / 8 |
| 4 | Cozy         | dominant is sweet or salty, `balanceScore > cozyMinBalance`, `intensity <= cozyMaxIntensity` | 0.10 / 20 |
| 5 | Refreshed    | dominant is acidic, `balanceScore > refreshedMinBalance` | 0.25 |
| 6 | Spicy        | dominant is spicy, `balanceScore > spicyMinBalance` | 0.25 |
| 7 | Confused     | fallback — always appended last | — |

Every threshold is `[SerializeField]` on `CreatureEmotionController`. The
calibration history (what changed and why) is in **section 7.7** of the
thesis and in **Annex D — Taula de Calibratge**.

**Cooking freeze.** Continuous cooking stops mutating the ingredient's
flavour profile once `cookingProgress` reaches 1.0 (stage 3 / burnt / mush).
The coroutine keeps running so the player can still stop the process, but
no further deltas are applied. This prevents indefinite flavour drift on
forgotten ingredients.

### 6.2 Stations

The architecture is described in detail in **section 7.4** of the thesis.

```
CookingStation                         thin host
└── StationSlot (1..n)                 each carries its own ProcessVariant
```

`CookingStation` holds a list of `StationSlot` children (auto-discovered
from children in `Awake` if the Inspector list is empty). It exposes
aggregate state — `IsProcessing` and `IngredientCount` — that the HUD reads
when the player aims at the station.

`StationSlot` is where the actual cooking lives. Each slot has:

- `ProcessVariant variant` — assigned in the Inspector. May be null at
  authoring time; a variantless slot accepts ingredients but cannot cook.
- `float heatLevel` — used by `ContinuousVariant` only.
- `float minProcessTime` — used by `DiscreteVariant` only.
- `Transform snapPoint` — where the carried `PickupItem` is mounted.

`TryAccept` snaps the item to `snapPoint`, switches it to kinematic via
`PickupItem.OnPickedUp`, and back-references itself onto `item.CurrentSlot`.
`Release` reverses the same steps and stops any in-flight process.

`StartProcess` pattern-matches on the assigned variant:

- `DiscreteVariant` → `RunDiscrete` coroutine: `WaitForSeconds(minProcessTime)`,
  apply `FlavorCalculator.ApplyDiscreteModifier` using the ingredient's
  `CutMultiplier` / `CutModifier`, write `variant.ResultForm` to
  `ingredient.Form`, and play `SoundManager.PlayIngredientChopped`.
- `ContinuousVariant` → `RunContinuous` coroutine: every frame pulls a
  per-tick delta from the ingredient (selected by `Category` and the current
  `CookState`), applies it via `FlavorCalculator.ApplyContinuousDelta`,
  accumulates `cookingProgress`, and advances `CookState` whenever the
  stage 2 or stage 3 threshold is crossed. The slot also requests a
  category-routed looping audio source from `SoundManager.StartCookingLoop`.

Because the *kind* of process lives on the slot, a single physical station
can mix slot kinds (one chop slot + one sauté slot + one boil slot on the
same surface). Adding a new process — bake, fry, steam — is a content
action: create a new `ProcessVariant` asset, assign it to a slot in the
Inspector, and author the per-stage deltas on the relevant `IngredientData`.

### 6.3 Plating

The plating system is dish-centric, not slot-centric — the design decision
and its rationale are in **section 9.1** of the thesis. The plate is a
physical, carryable object so that serving and cleaning up become spatial
actions rather than abstract clicks.

`PlatingStation` is **not** a `CookingStation`. It is a one-button vessel
spawner: while empty-handed and aimed at the station, the player presses E
to instantiate `flatPlatePrefab` at `snapPoint`. `HasDish` exposes whether
a dish is currently on the station; `NotifyDishPickedUp` resets it.

`DishVessel` lives on the spawned dish prefab (alongside `PickupItem`). It
owns the absorbed ingredient list, the baked `DishResult`, and the
clean/dirty visual swap:

- `AbsorbIngredient(item)` adds the ingredient's `IngredientInstance` to its
  list and reparents the `PickupItem` GameObject onto a child FoodSlot.
  The mesh + cook state the player saw while carrying the ingredient are
  exactly what the dish now shows.
- `Finalize()` is called by `InteractionSystem.Pickup` right before the
  player lifts the dish. It builds the immutable `DishResult` (combined
  profile + analysis). Returns false (blocks pickup) on an empty dish.
- `SetDirty()` is called by `CreatureEmotionController.FeedDish` after the
  creature reacts. It hides `cleanVisualRoot`, shows `dirtyVisualRoot`,
  and gates the player into carrying the soiled vessel to a `CleanupStation`.

`CleanupStation` is an `IStationSlot` that accepts only `DishVessel`s
marked dirty. On accept it snaps the vessel, queues a destruction coroutine,
and yields one frame to re-disable colliders — this prevents the
re-enabled kinematic dish from depenetrating against the player's
`CharacterController` and launching them upward.

### 6.4 Player & interaction

`PlayerController` — first-person CharacterController:

- WASD movement via New Input System; mouse-look with cumulative pitch
  applied through `Quaternion.Euler` to avoid the `localEulerAngles`
  0/360 wrap-around glitch.
- Walking SFX as a looping audio source — starts on the first frame the
  movement vector exceeds a small threshold, stops on the first frame it
  drops below it.

`InteractionSystem` — raycast-driven state machine. The full decision tree
is documented in **section 7.5** of the thesis.

- Per-frame raycast from the camera (max 2.5m, layer mask excludes Player,
  `QueryTriggerInteraction.Collide` to also hit trigger colliders).
- Resolves `PickupItem`, `IStationSlot`, `CookingStation`,
  `CreatureEmotionController`, `PlatingStation`, `DishVessel` and
  `IngredientBasket` from the hit collider.
- Held items have their colliders disabled (`PickupItem.DisableColliderForHolding`)
  so they do not block the raycast. Every reparent of a `PickupItem`
  transform is immediately followed by `localScale = Vector3.one`; without
  it, holdPoint's lossy scale was baked into the item across pickup cycles.
- E-key handles the eight-case interaction priority chain:
  1. Held + slot accepts → release into slot (`place` / `clean`)
  2. Held DishVessel + creature → `FeedDish` (vessel stays dirty)
  3. Held ingredient + creature → `Feed` (legacy single-ingredient)
  4. Held ingredient + aimed DishVessel → `AbsorbIngredient`
  5. Empty + PickupItem → pick up (empty DishVessel blocks)
  6. Empty + cooking slot with ingredient + variant → toggle THAT slot's process
  7. Empty + empty PlatingStation → spawn dish
  8. Held + no match → drop
- Left-click is petting-only; routes to `CreatureEmotionController.OnPetted`.
- Public `GetInteractionPromptText()` returns the HUD prompt string for the
  current aim + held state. Priority mirrors `PerformInteraction` so the
  prompt always matches what E will do.

`IStationSlot` is the contract any drop target implements
(`StationSlot`, `CleanupStation`). The trigger collider can live on a
child — `InteractionSystem` resolves the slot via `GetComponentInParent`.

`IngredientBasket` — `RequestNext` walks a round-robin through
`availableIngredients` and queues spawns. R-key is the placeholder binding
until the AI-assistant UI flow is implemented (mentioned in **section 6.5**
of the thesis).

`PickupItem` — the carryable. Holds one `IngredientInstance` and swaps
the active `MeshFilter.sharedMesh` whenever the instance's `Form` or
`CookState` changes (each axis has its own change event; both route to
`ApplyMesh`). `OnPickedUp` / `OnDropped` toggle the `Rigidbody` between
kinematic and dynamic and route through `SoundManager`. Visual is
mesh-per-(Form, CookState) — there is no runtime colour overlay, each
cooked variant is its own authored mesh on `IngredientData.meshOverrides`.

### 6.5 Creature

`CreatureEmotionController` is a passive recipient:

- `FeedDish(DishVessel)` is the main entry point. Reads the vessel's baked
  `DishResult`, runs `React(dish.Analysis)`, then calls `vessel.SetDirty()`.
- `Feed(IngredientInstance)` is a legacy single-ingredient feed path used
  by case 3 of the interaction state machine.
- `OnPetted()` short-circuits evaluation and always plays the Delighted
  response.

`React` builds an `EmotionThresholds` from the Inspector fields and asks
`FlavorCalculator.EvaluateEmotion` for the matching `EmotionalResponse`.
`ApplyResponse` samples the response's `ColorGradient` at `t = 0.5` for
the peak colour, applies it through a cached `MaterialPropertyBlock` (probing
both `_BaseColor` for URP/Shader Graph and `_Color` for Built-in shaders),
calls `SoundManager.PlayCreatureReaction`, and broadcasts the reaction line
to `VisorHUD`. The colour holds for 1.5 s and then lerps back to white over
`fadeOutDuration` (default 4 s). A new reaction stops any in-flight fade —
back-to-back dishes/feeds/pets do not stack.

Animations are not wired in the MVP (documented in the thesis as a deferred
deliverable; the playtesting results in section 8.4 confirm that the
colour + sound + HUD-line combination is enough for participants to read
the creature's reaction).

### 6.6 Audio — `SoundManager`

Single-instance dispatcher with a static API. Missing instance and missing
clips are silent no-ops, so partial scene wiring just works.

- Final volume per SFX = `perClipVolume × categoryVolume × masterSfxVolume`.
- Three master-level sources are created on `Awake`: a music source, a
  pooled 2D SFX source (used by `PlayOneShot`), and a dedicated walk-loop
  source. Per-slot cooking loops are additional `AudioSource` components
  added on demand, indexed by `StationSlot.GetInstanceID()`.
- Categories: `cookingVolume`, `creatureVolume`, `playerVolume`,
  `ingredientVolume`, `platingVolume`. Each clip slot is a `SoundClip`
  wrapper with its own volume slider.

| API | Used by |
|---|---|
| `PlayIngredientChopped` | DiscreteVariant completion |
| `PlaySlotProcessOn` / `PlaySlotProcessOff` | One-shot wrap around continuous slot start/stop |
| `StartCookingLoop(slotKey, category)` / `StopCookingLoop(slotKey)` | StationSlot continuous coroutine |
| `StartWalkLoop` / `StopWalkLoop` | PlayerController (looped while moving) |
| `PlayCreatureFed`, `PlayCreatureReaction(emotion)` | CreatureEmotionController |
| `PlayIngredientSpawned`, `PlayIngredientPickedUp`, `PlayIngredientDropped` | PickupItem |
| `PlayPlateSpawned`, `PlayItemPlacedOnPlate` | PlatingStation, DishVessel |

### 6.7 UI

The HUD is fully diegetic — every panel is framed as a readout from the
investigator's visor. The visual identity is the retrofuturist
"food-truck-in-space" defined in **section 6.2** of the thesis.

`VisorHUD` — single HUD controller on the scene `Canvas`. Each frame it
polls `InteractionSystem.Held` / `AimedPickup`, routes to a dish view or an
ingredient view, and drives:

- Item panel: name, dominant flavours, form + cook-state label, icon glyph.
- Flavour radar chart (delegated to `FlavorMapGraphic`).
- Cooking progress bar with live indicator (` ●`).
- Interaction prompt (sourced from `InteractionSystem.GetInteractionPromptText`).
- NAVI strip: pulsing accent dot beside a static placeholder text. The
  thesis notes (section 6.5) that contextual NAVI messages are reserved
  for future work.
- SOL counter — auto-incrementing diegetic timer.
- Creature reaction line — brief fade-in / hold / fade-out narration cue,
  driven by `CreatureEmotionController.ShowCreatureReaction`.

`FlavorMapGraphic` — `MaskableGraphic` subclass that procedurally draws a
5-axis pentagon radar (sweet · acidic · bitter · salty · spicy; neutralizer
is a meta-attribute and is intentionally absent from the chart). Auto-creates
its five axis labels as TMP children on `Start`. Values are normalised to
`[0,1]`; `VisorHUD.ProfileToDict` handles the 0–10 scaling.

`Crosshair` — three-image procedural crosshair (dot + bars). Auto-creates
the children if the Inspector fields are empty. Tints from translucent
white to warm accent when `InteractionSystem.IsLookingAtInteractable` is
true.

---

## 7. Coding Conventions

- `[SerializeField] private` for every Inspector-exposed field; never public
  fields.
- `[CreateAssetMenu(menuName = "Galatea/…")]` on every authored
  ScriptableObject.
- Composition over inheritance. The original `BaseStation` /
  `DiscreteStation` / `ContinuousStation` abstract hierarchy was collapsed
  into the concrete `CookingStation` + per-slot `StationSlot` model
  (thesis, section 7.7).
- C# `event Action<T>` for cross-system notifications.
- `FlavorProfile` values are clamped to `[0, 10]` after any calculation.
- All flavour maths in `FlavorCalculator` — no flavour logic inside
  MonoBehaviours.
- All audio playback through `SoundManager`'s static API; missing-clip is a
  silent no-op (no per-call null guards at the call site).
- Comments are minimal and pre-/post-style. Narrative explanations live in
  this file or in the thesis, not in source. Unused getters / events /
  helpers are linted out rather than kept "for later."

---

## 8. Implementation Status

### Completed (MVP)

**Project setup** — Unity 6 + URP + New Input System; GitHub repository;
folder layout under `Assets/Scripts/{Data,Systems,Player,Creature,UI}` and
`Assets/ScriptableObjects/{Ingredients,Processes,Emotions}`; `GameScene.unity`
with TextMesh Pro imported.

**Data layer** — enums, `FlavorProfile`, `IngredientInstance` (with
independent `Form` / `CookState` change events), `FlavorAnalysis`,
`DishResult`, `EmotionThresholds`, `FlavorCalculator` (Combine, Analyze,
ApplyDiscreteModifier, ApplyContinuousDelta, EvaluateEmotion).

**ScriptableObjects** — `IngredientData` with the per-category response
fields, `DiscreteVariant`, `ContinuousVariant`, `EmotionalResponse`.

**Stations** — `CookingStation` + `StationSlot` with discrete and
continuous coroutines, per-slot heat / process time, per-slot variant
assignment.

**Plating loop** — `PlatingStation` spawns a `DishVessel`; the player
absorbs ingredients into it, picks it up, carries it to the creature,
and then to the `CleanupStation`.

**Player + interaction** — `PlayerController` with looping walk SFX,
`InteractionSystem` with the eight-case E-key state machine and left-click
petting.

**Creature** — `CreatureEmotionController` with full emotion evaluation,
`MaterialPropertyBlock` tint, HUD narration cue, and audio routing.

**Audio** — `SoundManager` with three-level mixing, per-slot cooking loops,
walk loop, creature reaction clips, and ingredient / plating one-shots.

**UI** — `VisorHUD`, `FlavorMapGraphic` (5-axis pentagon radar) and
`Crosshair`.

**Content** — placeholder `IngredientData` for AlienRoot, CrimsonClove and
SpottedBerry; `Chop_Diced` discrete variant and `Sautee` continuous variant;
all seven `EmotionalResponse` assets.

### Open items (scene authoring)

These are Inspector-side tasks for the scene/prefab pass — code is in place.

- [ ] Per-slot `Variant` assignment on every `StationSlot` child of
  `CuttingStation` and `SauteStation`.
- [ ] Wire `creatureReactionText` on `VisorHUD` (TMP child under Canvas).
- [ ] Author `reactionLine` strings on the seven `EmotionalResponse` SOs
  (hard-coded defaults exist as fallback).
- [ ] Author + assign `AudioClip`s on the `SoundManager` GameObject
  (background music + each SFX slot, including the new walk loop).
- [ ] Remove the orphan `DebugInfoPanel` GameObject under the HUD Canvas
  (script was deleted).
- [ ] Remove the legacy `StationDefinition` SO assets
  (`ChoppingStation_Definition.asset` / `SauteeingStation_Definition.asset`)
  and the orphan `InteractionPromptHUD` and `ContainerType` references —
  all three scripts were removed during cleanup.

### Deferred to post-MVP (see thesis sections 10.1 – 10.6)

- Improved balance model (moderation + contrast components — thesis 10.1).
- Additional stations: Oven, Blender, Sieve (10.2) — content-only, no code
  change required.
- The Greenhouse (10.3) — diegetic ingredient harvesting that replaces the
  basket.
- `SpecialBehavior` system (10.4) — probabilistic alien quirks driven by
  the existing `IngredientTag` field.
- Creature body animations per emotion + final material/shader pass (10.5).
- Broader playtesting validation (10.6).

---

## 9. Reference

- `MEMORIA_TFG.md` / `MEMORIA_TFG.docx` — the formal thesis document
  (Catalan). Sections referenced throughout this file:
  - **6** — Game design (narrative, aesthetics, characters, loops, audio).
  - **7** — Implementation (architecture, FlavorCalculator, stations,
    interaction, creature, design decisions, calibration).
  - **8** — Results and playtesting analysis.
  - **9–10** — Conclusions and future work.
  - **Annex B** — Full script roster.
  - **Annex C** — Ingredient flavour profiles.
  - **Annex D** — Emotion threshold calibration history.
  - **Annex E** — Playtesting protocol.
