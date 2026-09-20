# Project Overview
- **Game Title:** Nocturnal Breach (VR Room-Scale Bedroom Horror)
- **High-Level Concept:** An original, atmospheric VR room-scale survival-horror experience taking place inside a single small bedroom. The player faces ONE primary threat—Monster Mutant 7—attacking through three physical room thresholds: the window, the underside of the bed, and the bedroom door. Survival depends purely on concentration, spatial awareness, listening, peripheral vision, physical room-scale movement, and natural physical interactions (closing the window, crouching to illuminate the creature under the bed with a flashlight, physically bracing/holding the door shut against monster force).
- **Players:** Single Player (Physical Room-Scale VR)
- **Inspiration / Reference Games:** Alien: Isolation (sound-driven stalking & reactive behavioral director), Five Nights at Freddy's 4 (environmental dread & threshold defense), Duck Season (bedroom claustrophobia & physical VR grounding).
- **Tone / Art Direction:** Gritty, claustrophobic psychological horror. Deep shadows, subtle exterior moonlight, realistic creaks, heavy breathing, physical resistance, and dread.
- **Target Platform:** Meta Quest 2 Standalone (Android ARM64, OpenXR).
- **Screen Orientation / Resolution:** VR Stereo Single Pass Instanced (native Quest 2 per-eye resolution, 72Hz / 80Hz).
- **Render Pipeline:** Universal Render Pipeline (URP).

---

# System Requirements Categorization Matrix

Every system, parameter, and assumption in this implementation plan is explicitly categorized according to the project specifications:

| Component / System | Category | Description / Notes |
| :--- | :--- | :--- |
| **Physical Room-Scale Movement** | **[A] Hard Requirement** | Player physically gets out of bed and walks; no artificial locomotion as core loop. |
| **Single Monster Entity (Monster Mutant 7)** | **[A] Hard Requirement** | Reuses existing Monster Mutant 7 asset with unified behavioral state machine. |
| **Natural Interactions (No HUD / No QTEs)** | **[A] Hard Requirement** | No floating UI ("Press E", "Hold Button"), no artificial QTEs or button mashing. |
| **Room Reconstruction via Floor Plan** | **[A] Hard Requirement** | Rectangular room, window on upper wall, bed on left/upper-left, nightstand beside bed, door on lower/right. |
| **No Closet Gameplay** | **[A] Hard Requirement** | Closet is completely removed from gameplay; no hiding, no monster approach from closet. |
| **Window Physical Defense** | **[A] Hard Requirement** | Progressive outside stalking; physically closing/sliding window repels monster. |
| **Under-Bed Physical Defense** | **[A] Hard Requirement** | Monster reaches upward from beneath bed; player crouches and illuminates with flashlight. |
| **Door Physical Resistance Defense** | **[A] Hard Requirement** | Monster physically pushes door open; player physically resists door movement. |
| **Dual Modality (Controllers + Hands)** | **[A] Hard Requirement** | Touch controllers and hand tracking must both work across the same gameplay interactions. |
| **Meta XR Interaction SDK Integration** | **[A] Hard Requirement** | Use modular Interaction SDK components; do not build a custom VR interaction framework. |
| **Flashlight Battery Depletion & Spares** | **[B] Optional** | Modular system; only enabled if playtesting confirms it enhances tension without adding chore. |
| **Nightstand Diegetic Clock** | **[B] Optional** | 3D alarm clock for survival time; optional atmosphere element. |
| **Window Timing & Escalation Windows** | **[C] Design Parameter (Tuning)** | Pacing delays from stalking to glass breach are configurable Inspector parameters. |
| **Under-Bed Light Exposure Threshold** | **[C] Design Parameter (Tuning)** | Duration/intensity of light required to repel creature is a configurable designer parameter. |
| **Door Monster Push Force & Hold Duration** | **[C] Design Parameter (Tuning)** | Push torque, resistance thresholds, and retreat durations are configurable Inspector floats. |
| **Game Director Pacing & Difficulty** | **[C] Design Parameter (Tuning)** | Threat selection weights, cooldown curves, and player attention tracking are tunable curves. |
| **Input Backend (New vs Both)** | **[D] Technical Assumption (Verify)** | `activeInputHandler = 2` (Both) will be evaluated before modifying to verify package dependencies. |
| **Android Graphics API (Vulkan vs GLES3)** | **[D] Technical Assumption (Verify)** | Verify whether OpenGLES3 can be safely removed via Project Validation before changing PlayerSettings. |
| **ASTC Texture Compression** | **[D] Technical Assumption (Verify)** | Verify ASTC compatibility across all imported texture assets before switching subtarget. |
| **URP RP Asset Selection (Mobile vs PC)** | **[D] Technical Assumption (Verify)** | Verify `Mobile_RPAsset` settings (shadows, MSAA, render scale) against scene visual quality and Quest 2 GPU budget. |

---

# 1. Project Audit & Configuration Plan

### 1.1 Existing State Summary
- **Unity Engine:** 6000.6.0f1 (Unity 6.6).
- **Target Build Target:** Android (`BuildTarget.Android`).
- **Scripting Backend:** IL2CPP, Architecture: ARM64, Min SDK: API 32, Target SDK: API 34.
- **Active Rendering Pipeline:** In Editor, `PC_RPAsset` is active. In QualitySettings: Level 0 (Mobile) uses `Mobile_RPAsset`, Level 1 (PC) uses `PC_RPAsset`.
- **XR Plug-in Management:** OpenXR Loader (`UnityEngine.XR.OpenXR.OpenXRLoader`) is enabled for both Standalone and Android.
- **Installed Meta XR SDKs (v205.0 / v85.0):** Core, Interaction, Interaction OVR, Audio, Haptics, Platform, Voice, MR Utility Kit, All-in-One.
- **Input System:** `activeInputHandler: 2` (Both Legacy Input Manager and New Input System).
- **Console Warnings Identified:**
  - `Input Manager is marked for deprecation` (from activeInputHandler = 2).
  - `GPUResidentDrawer "BatchRendererGroup Variants" setting must be "Keep All"`.
  - Missing components on `StaticLightingSky`, `SceneIDMap`, and `Lamp/Light` in `Assets/cuarto.unity`.

### 1.2 Verification-First Configuration Strategy
1. **Input Backend [D]**:
   - *Verification Step:* Inspect whether any package or editor tool requires Legacy Input before changing. (The sample script `DoorController.cs` uses `Input.GetKeyDown(KeyCode.E)`, but it is slated for replacement with VR physical interaction).
   - *Action:* Once `DoorController.cs` is replaced with `PhysicalVRDoor.cs`, test setting `activeInputHandler` to `1` (Input System New) in a separate branch/test to confirm zero compilation errors. If any tool requires legacy, leave at `2` (Both).
2. **Android Graphics API [D]**:
   - *Verification Step:* Inspect Project Validation for OpenXR Android. While Vulkan is the modern standard for Quest 2, verify if removing `OpenGLES3` causes any shader compilation failures on existing bedroom materials.
   - *Action:* If validated, configure Vulkan as the primary/sole graphics API for Android builds.
3. **Texture Compression [D]**:
   - *Verification Step:* Evaluate `EditorUserBuildSettings.androidBuildSubtarget`. Default is `Generic`.
   - *Action:* Test setting to `ASTC`. Verify that PBR textures from `BasicBedroomPack-Mavi3D` and `MonsterMutant 7` compress without compression artifacts.
4. **URP Mobile Asset Tuning [D]**:
   - *Verification Step:* `Mobile_RPAsset` currently has `additionalLightShadows = False` and `renderScale = 0.8`. Flashlight requires an additional Spot Light.
   - *Action:* Determine if flashlight needs dynamic shadows or if an unshadowed soft-falloff spot light with a high-quality projected cookie achieves superior visual clarity and saves substantial GPU frame time on Quest 2.

---

# 2. Room Reconstruction Plan

### 2.1 Audit of Existing Room Scene (`Assets/cuarto.unity`)
- The current bedroom is an unstructured prototype composed of 9 primitive scaled cubes (`Cube` to `Cube (9)`).
- `Window` is accidentally misplaced at `(-106.15, 1.02, -0.04)`.
- `Window (1)` is at `(-3.15, 0.96, -0.18)`.
- Four separate Directional Lights are illuminating simultaneously, completely washing out the room and destroying horror atmosphere.
- `Closet` with 4 drawers sits at `(-1.45, 0.02, -2.23)`, conflicting with the floor plan and room-scale walking path.
- HDRP remnant GameObjects (`StaticLightingSky`, `SceneIDMap`, `Bedroom HDRP`) hold missing component references.

### 2.2 Intended Floor-Plan Architecture
The bedroom will be reconstructed to exact architectural proportions to ensure natural sightlines, horror staging, and physical room-scale clearances:

```
================================[ NORTH / UPPER WALL ]================================
|                                                                                    |
|                                    WINDOW                                          |
|                          [ Physical Sliding Sash ]                                 |
|                               (Pos: 0, 1.15, 2.2)                                  |
|                                                                                    |
|   +--------------------+                                                           |
|   |                    |           [ WALKWAY TO WINDOW ]                           |
|   |        BED         |           Clearance: 1.3m wide                            |
|   |  (Pos: -1.4, 0,    |                                                           |
|   |         0.8)       |                                                           |
|   |                    |           [ OPEN ROOM-SCALE CENTER ]                      |
|   |   Underside Crawl  |           Walking Area: ~2.4m x 2.2m                      |
|   |     Zone Facing    |           Player physical movement space                  |
|   |      Center        |                                                           |
|   +--------------------+                                                           |
|                                                                                    |
|   +--------------------+                                    +------------------+   |
|   |     NIGHTSTAND     |                                    |       DOOR       |   |
|   | (Pos: -1.4, 0,     |           [ WALKWAY TO DOOR ]      | (Pos: 2.15, 0,   |   |
|   |        -0.9)       |           Clearance: 1.4m wide     |        -1.2)     |   |
|   |   Holds Flashlight |                                    | Heavy Wood Slab  |   |
|   +--------------------+                                    +------------------+   |
|                                                                                    |
================================[ SOUTH / LOWER WALL ]================================
```

### 2.3 Proportions, Dimensions & Clearances
- **Room Enclosure:** Width = 4.4m (X: -2.2 to +2.2), Length = 4.6m (Z: -2.3 to +2.3), Height = 2.5m.
- **Bed (Left / Upper-Left):** Dimensions ~1.87m x 2.79m. Positioned along West wall. Height from floor provides a 0.35m open gap beneath the mattress rail, allowing a player to comfortably crouch and look underneath from the central floor.
- **Nightstand (Beside Bed):** Positioned at X = -1.4, Z = -0.9. Surface is physically within arm's reach while sitting/starting on the bed, holding the flashlight.
- **Window (Upper Wall):** Centered on North wall at X = 0.0, Z = 2.2, sill height Y = 1.0m. Direct physical walking route from bed to window.
- **Door (Lower / Right Wall):** Positioned on East wall at X = 2.15, Z = -1.2. Swings inward into the bedroom. Direct walking route from bed and window.
- **Central Clearance:** A continuous 2.4m x 2.2m unobstructed physical walking polygon in the center of the bedroom, accommodating standard Quest 2 physical boundaries.
- **No Closet [A]:** The closet asset is completely removed from the room layout. No closet gameplay systems will exist.
- **Environment Prefabs to Reuse:**
  - `Assets/BasicBedroomPack-Mavi3D/Prefabs/URP/Bed.prefab`
  - `Assets/BasicBedroomPack-Mavi3D/Prefabs/URP/Furniture01.prefab` (Nightstand)
  - `Assets/BasicBedroomPack-Mavi3D/Prefabs/URP/Room.prefab` or modular wall pieces
  - `Assets/Apartment_Door/Prefabs/Aparment_Door.prefab` (Door frame and door slab)
  - Flooring: `Assets/YughuesFreeFlooringMaterials/` PBR wood floor.

---

# 3. XR & Rig Architecture

### 3.1 Non-Destructive Camera Rig Integration
- **Current Scene Rig:** `[[BuildingBlock] Camera Rig]` contains `OVRCameraRig`, `OVRManager`, `OVRHeadsetEmulator`, `OVRControllerHelper`, and `OVRHand` components.
- **Rule:** DO NOT blindly delete or replace this rig.
- **Integration Approach:**
  1. Set the root transform of `[[BuildingBlock] Camera Rig]` to Position `(0, 0, 0)` and Rotation `(0, 0, 0)` so that FloorLevel tracking accurately aligns with the virtual floor (Y = 0.0).
  2. The Meta XR Interaction SDK provides `OVRComprehensiveInteractionRig.prefab` (`Packages/com.meta.xr.sdk.interaction.ovr/Runtime/Prefabs/OVRComprehensiveInteractionRig.prefab`), which is architected specifically to be nested under `OVRCameraRig`.
  3. Integrate the Interaction SDK rig under the existing `OVRCameraRig` (or use the official Meta Quick Action wizard: `OVRComprehensiveInteractionRigWizard.cs`).
  4. Ensure references from `OVRCameraRig`'s tracking anchors (`LeftHandAnchor`, `RightHandAnchor`, `CenterEyeAnchor`) feed directly into the Interaction SDK interactors.
  5. Remove only redundant visual controller duplicates if the interaction rig provides superior synthetic hand and controller visuals.

### 3.2 Room-Scale Locomotion Configuration [A]
- **Locomotion Policy:** 100% physical room-scale locomotion.
- The `OVRComprehensiveInteractionRig` includes modular locomotion features (Teleport, Snap Turn, Smooth Locomotion).
- **Configuration:** Deactivate / disable the teleportation and smooth locomotion modules on the rig.
- The player physically walks across the virtual bedroom using their real-world feet.
- An optional subtle perimeter boundary fade/vignette will be provided if the player approaches the outer walls of the bedroom.

---

# 4. Interaction Architecture (Touch Controllers & Hand Tracking)

### 4.1 Dual Modality Foundation [A]
Both Meta Quest Touch Controllers and Meta Quest Hand Tracking must control the exact same gameplay mechanics seamlessly:

```
                        +---------------------------------------------+
                        |         Meta XR Interaction SDK 205         |
                        +----------------------+----------------------+
                                               |
                     +-------------------------+-------------------------+
                     |                                                   |
                     v                                                   v
      +-----------------------------+                     +-----------------------------+
      |      Controller Modality    |                     |    Hand Tracking Modality   |
      |  - GrabInteractor           |                     |  - HandGrabInteractor       |
      |  - Controller Pose / Grip   |                     |  - Synthetic Hand Visual    |
      +--------------+--------------+                     +--------------+--------------+
                     |                                                   |
                     +-------------------------+-------------------------+
                                               |
                                               v
                        +---------------------------------------------+
                        |            Unified Interactables            |
                        |  - Grabbable (Flashlight)                   |
                        |  - OneGrabTranslateTransformer (Window)     |
                        |  - OneGrabRotateTransformer (Door)          |
                        +---------------------------------------------+
```

### 4.2 Interaction SDK Modular Components
- **Grabbing Props (Flashlight):** `Grabbable` + `GrabInteractable` (for controller grab trigger/grip) + `HandGrabInteractable` (for natural finger pinch/grip poses).
- **Constrained Linear Motion (Window Sash):** `Grabbable` + `OneGrabTranslateTransformer` constrained strictly to the slider axis, with min/max position limits.
- **Constrained Angular Motion (Door Slab):** `Grabbable` + `OneGrabRotateTransformer` constrained strictly around the hinge Y-axis between 0° (fully latched) and 85° (fully open inward).
- **Physical Contact / Switches:** `PokeInteractable` for flashlight physical toggle switch and nightstand drawer pulls.

---

# 5. Flashlight Architecture

### 5.1 Flashlight Design [A]
- **Physical Assembly:**
  - Formed from a clean cylindrical flashlight chassis (barrel, knurled grip, flared reflector head, front glass lens).
  - Component `Grabbable` with `GrabInteractable` and `HandGrabInteractable` zones centered on the barrel.
  - Physical toggle button on the barrel with audio click and tactile toggle state.
- **Light Source & Performance Tuning [D]:**
  - URP dynamic Spot Light positioned at the reflector head.
  - Range: 7.0 meters. Inner angle: 25°, Outer angle: 45°.
  - Soft-falloff cookie texture to simulate authentic flashlight glass reflector dispersion.
  - To preserve Quest 2 GPU fill-rate, real-time shadow casting on the flashlight spot light is kept unshadowed or low-resolution baked shadow mask, preventing fill-rate spikes in mobile VR.
- **Gameplay Detection Cone:**
  - A forward Raycast / Conical Trigger (`FlashlightBeamDetector`) broadcasts light illumination onto targets tagged as `MonsterLightSensitive`.
  - When the beam illuminates the monster's under-bed target, an event `OnLightIllumination(float intensity)` is triggered on the monster.

### 5.2 Battery System [B] (Optional & Modular)
- Battery mechanics are strictly categorized as **[B] Optional**.
- The core script `FlashlightTool.cs` will have a boolean flag `enableBatteryDrain = false` by default.
- If enabled during later playtesting:
  - Battery charge drains slowly over continuous use.
  - Diegetic low-battery indicator: a subtle analog needle or faint yellow LED on the flashlight body (no floating HUD).
  - Nightstand drawer holds replacement cylindrical batteries that snap into the handle base.
- If disabled, flashlight functions reliably with standard on/off toggle.

---

# 6. Window Mechanic Plan

### 6.1 Progressive Threat Sequence [A]
The monster approaches from outside the window through distinct, perceivable stages without arbitrary QTEs or floating text:
1. **Distant Environmental Audio:** Distant snapping branches, heavy rustling in grass outside the north wall.
2. **Stalking Proximity:** Heavy creature breathing and positional footsteps approach the exterior window sill.
3. **Visual & Auditory Contact:** A massive silhouette looms outside the glass; claws tap and scratch against the glass pane.
4. **Escalation Threat:** The monster places its face/hands against the glass, letting out a guttural hiss (`rage` animation). Glass vibrations resonate into the room.
5. **Breach Threat:** If the window is left open/unlatched as the threat reaches its peak, the monster forces through the window frame.

### 6.2 Physical VR Interaction [A]
- **Window Assembly:**
  - Fixed outer frame set into North bedroom wall.
  - Sliding sash with handle equipped with `OneGrabTranslateTransformer` constrained along the sliding axis.
  - Dual grab support: player can grip the sash with Touch controller grip or by pinching/grabbing with tracked hands.
  - Audio: Believable wood/glass sliding friction, ending with a solid mechanical latching sound when pushed to the fully shut position.
- **Repelling the Monster:**
  - Closing the window to the latched position while the monster is approaching or at the glass breaks the monster's approach vector.
  - The creature plays a recoil/flinch animation (`gethit1`), emits a frustrated roar, and retreats into the darkness outside.
- **Configurable Pacing [C]:**
  - Stalking duration, scratch intervals, and escalation thresholds are designer-configurable Inspector parameters (e.g. `stalkingWarningDuration`, `breachEscalationTime`), tuned via playtesting rather than hardcoded timers.

---

# 7. Bed & Under-Bed Mechanic Plan

### 7.1 Progressive Threat Sequence [A]
1. **Auditory Warning:** Low, rhythmic scratching against the underside of the floorboards and wooden bed slats. Low guttural breathing beneath the player's mattress.
2. **Physical Visual Emergence:** From beneath the open bed frame, Monster Mutant 7's spiked arm and clawed hand slowly emerge, creeping upward toward the mattress edge and top rail.
3. **Escalation Threat:** The arm reaches higher, testing the bed edge, preparing to pull the rest of the creature upward.

### 7.2 Natural Player Reaction & Defense [A]
- **The Physical Response:**
  - The player hears the localized spatial audio under the bed.
  - The player grabs the flashlight from the nightstand.
  - The player physically crouches or leans down beside the bed, aiming the flashlight beam directly into the dark recess beneath the bed frame.
- **Technical Light Detection:**
  - An `UnderBedLightTarget` component is attached to the reaching arm/torso.
  - When the flashlight Spot Light cone intersects the target collider, the script evaluates beam angle and distance.
  - Detection Threshold [C]: Tunable parameter `requiredLightExposureDuration` (default ~1.5s, tunable in Inspector).
- **Repelling the Creature:**
  - As light floods the creature, its skin smokes/reacts; it screeches in agony, violently jerks its arm back under the bed (`gethit2` / `gethit3` recoil), and retreats through the sub-floor portal, entering a cooldown state.
  - No button prompt, no QTE, no minigame. Pure physical crouching, aiming, and observation.

---

# 8. Door Mechanic Plan

### 8.1 Progressive Threat Sequence [A]
1. **Hallway Auditory Warning:** Slow, heavy footsteps approach the bedroom door from the dark corridor outside.
2. **Handle Testing:** The door handle jiggles and rattles as the creature tests the latch from the exterior.
3. **Monster Push Force:** The door unlatches and begins swinging inward by 10°–20°. The monster applies physical pushing torque against the door slab from the outside, attempting to force it wide open.

### 8.2 Physical Resistance & Defense [A]
- **Physical Door Assembly:**
  - Detailed door slab hinged to frame using `OneGrabRotateTransformer` (0° to 85° inward swing).
  - Replaces obsolete `DoorController.cs` (no KeyCode.E, no UI text).
  - Dual grab zones: handle grab point and inner door slab push surface.
- **Dynamic Force & Resistance Calculation:**
  - When monster attacks, `PhysicalVRDoor.cs` applies an inward opening torque `MonsterPushTorque` over time.
  - The player physically walks to the door, grips the handle or places their virtual hand against the door face, and pushes forward/inward to force the door back to 0° (latched).
  - The script calculates the resistance torque applied by the player's hand relative to the monster's force.
  - If the player holds the door shut against the monster's pushing pulses until the monster's assault stamina depletes, the monster gives a final heavy thud against the wood, emits a muffled snarl, and its heavy footsteps retreat down the hallway. The door latch clicks shut.
  - If the door is forced open past a configurable angle (e.g., >60°), the monster breaches for a game over sequence.
- **Configurable Parameters [C]:**
  - Monster push force, pulse intervals, resistance thresholds, and retreat timings are fully exposed Inspector parameters.

---

# 9. Monster Mutant 7 Architecture

### 9.1 Asset Reuse & Integrity
- **Mesh & Rig:** Existing `Assets/MonsterMutant 7/Base mesh/Base mesh MonsterMutant7.fbx` with `Mat_MonsterMutant7_Skin1.mat` and `Base mesh MonsterMutant7Avatar`.
- **Policy:** NEVER modify the imported FBX, mesh, or material assets destructively.
- **Animation Clips Inspected & Assigned:**
  - *Stalking / Pacing:* `idle1`, `idle2`, `walk2`, `walk3`.
  - *Window Threat & Slam:* `rage` (screaming/flexing against glass), `attack1` (claw swipe against window pane).
  - *Window Recoil:* `gethit1` (head jerk back and step back).
  - *Under-Bed Arm Reach:* `attack2RLSpike` / `attack5` (long forward/upward reach of clawed arm), combined with localized bone pose or crouched root positioning.
  - *Under-Bed Light Recoil:* `gethit2` / `gethit3` (violent flinching and pulling limbs inward).
  - *Door Assault:* `rage` (charging impact), `attack4` (heavy shoulder and claw bash into door slab).
  - *Door Knockback:* `gethit4` (stumble back into hallway).
  - *Breach / Jumpscare:* `attack3` or `jump`.
- **Project-Owned Animator Controller:**
  - Create `Assets/Animations/Monster/MonsterMutant7_Behavior.controller` (project-owned, non-destructive).
  - Contains sub-state machines for Window, UnderBed, Door, and Transitions driven by clean float/trigger parameters.

### 9.2 Under-Bed Staging Solution
- Placing a massive upright mutant under a low bed produces impossible geometry clipping.
- **Staging Approach:**
  - Position Monster Mutant 7 rotated in a low-profile prowling/prone orientation beneath the bed frame floor cutout / proxy volume.
  - Alternatively, stage the creature root below the floor level with only its upper torso and reaching spiked arms emerging through the under-bed shadow plane into view.
  - Bed skirt / shadow geometry naturally occludes lower body while spotlight illumination dramatically highlights the reaching claws.

---

# 10. Unified Monster State Machine & Game Director

### 10.1 Monster State Machine (`MonsterBrain.cs`)
ONE intelligent entity transitioning through coherent states:

```
                          +------------------------+
                          |        DORMANT         |
                          |  (Prowling Perimeter)  |
                          +-----------+------------+
                                      |
                     [ Game Director Selects Threshold ]
                                      |
         +----------------------------+----------------------------+
         |                            |                            |
         v                            v                            v
+------------------+         +------------------+         +------------------+
|  WINDOW APPROACH |         |   BED APPROACH   |         |  DOOR APPROACH   |
+--------+---------+         +--------+---------+         +--------+---------+
         |                            |                            |
         v                            v                            v
+------------------+         +------------------+         +------------------+
|  WINDOW THREAT   |         | UNDER-BED THREAT |         |   DOOR THREAT    |
+--------+---------+         +--------+---------+         +--------+---------+
         |                            |                            |
    [Window Shut]               [Light Shone]               [Door Resisted]
         |                            |                            |
         +----------------------------+----------------------------+
                                      |
                                      v
                          +------------------------+
                          |        REPELLED        |
                          | (Recoil Anim + Retreat)|
                          +-----------+------------+
                                      |
                                      v
                          +------------------------+
                          |        COOLDOWN        |
                          |  (Repositioning Delay) |
                          +-----------+------------+
                                      |
                                      +--> (Back to DORMANT)
```

### 10.2 Game Director & Threat Selection Architecture (`GameDirector.cs`)
- **No Hardcoded RNG Percentages:** The monster is NOT an arbitrary random number generator (no 35/35/30 hardcoded split).
- **Intelligent Threat Selection Criteria:**
  1. *Repetition Prevention:* Discourages selecting the exact same threshold twice in a row unless testing player fatigue.
  2. *Pacing & Escalation Curve [C]:* Starts with single, deliberate stalking phases; escalates attack frequency as the night progresses.
  3. *Player Attention Tracking [C]:* Evaluates player's physical gaze / position. If the player camps continuously at the window, the director exploits the door or under-bed threshold to keep the player physically moving across their room.
  4. *Tunable Designer Knobs [C]:* Director parameters (cooldown ranges, threat escalation rate, attack weighting) exposed as AnimationCurves and ScriptableObject settings for playtest tuning.

---

# 11. Spatial Audio Architecture

### 11.1 Meta XR Audio Foundation
- Active spatializer plugin in Unity AudioSettings is `Meta XR Audio`.
- Low-latency spatialization configured with bedroom acoustic geometry reflection profile.
- **Horror Philosophy:** Silence is a weapon. No constant loud combat music. Pure environmental realism, breathing, subtle creaks, and directional terror.

### 11.2 Positional Emitters
1. **Window Emitter (North Exterior):**
   - Distant grass rustling, twigs breaking, glass tapping, claw scraping, window slide friction, heavy latch click.
2. **Under-Bed Emitter (South-West Floor):**
   - Sub-bass rumble, floorboard creaking, fabric rustling, claw clicking on wood slats, creature hiss/snarl, screech upon light contact.
3. **Door Emitter (East Wall / Hallway):**
   - Heavy corridor footsteps, handle jiggling, door wood groaning under stress, explosive shoulder slams, wood impact thuds.
4. **Flashlight Emitter (Held Item):**
   - Tactile mechanical switch click, subtle electrical hum.
5. **Room Ambient Emitter:**
   - Soft night wind outside, house settling creaks, distant rain on roof.

---

# 12. Haptics Architecture

### 12.1 Meaningful Physical Feedback [A]
Haptics communicate real physical events rather than generic rumble:
1. **Door Impacts & Resistance:**
   - *Impact Thud:* Sharp, heavy impulse (Amplitude 0.9, 0.25s) when monster slams against door while player is holding handle/panel.
   - *Continuous Struggle:* Dynamic rumble modulated directly by the differential between monster push force and player holding force.
2. **Window Latch:**
   - Solid, sharp tactile bump (Amplitude 0.8, 0.08s) confirming the window has safely locked into place.
3. **Flashlight Click:**
   - Crisp micro-pulse (Amplitude 0.5, 0.02s) on the thumb/index finger upon toggling the switch.
4. **Under-Bed Proximity:**
   - Low-frequency heartbeat sensation in controllers when the player crouches close to the under-bed creature.
5. **Implementation:** Driven via `OVRInput.SetControllerVibration` with optional Meta XR Haptics `HapticClipPlayer` integration.

---

# 13. Quest 2 Performance & Optimization Plan

### 13.1 Hardware Targets (Quest 2 Standalone)
- **Framerate Target:** 72 FPS rock-solid (13.88ms frame budget per eye).
- **Profiling Targets (Guides, not dogmatic caps):**
  - Draw Calls: Targeted < 100 per frame.
  - Triangle Count: Targeted < 150k visible triangles.
  - Zero garbage generation per frame in `Update()` loops (cache all arrays, RaycastHit buffers, and queries).

### 13.2 Lighting & GPU Optimization
- **Audit Finding:** Scene currently has 4 conflicting Directional Lights (`Directional Light`, `Directional Light (1)`, `(2)`, `(3)`), all white intensity 1.0.
- **Correction:**
  - Preserve ONE Directional Light configured as faint, cool-blue moonlight filtering through the North window (low intensity, soft shadows).
  - Remove the 3 redundant, conflicting Directional Lights.
  - The Flashlight is the primary dynamic light source in the room.
  - Bedroom static walls, floor, and furniture will utilize URP baked lighting / lightmaps.

### 13.3 Texture & Shading Strategy
- Consolidate bedroom materials to standard URP Lit / Simple Lit shaders.
- Verify ASTC texture compression across all textures to minimize memory bandwidth on the Quest 2 Snapdragon XR2 SoC.
- Render Scale set to 0.85 with 4x MSAA for sharp anti-aliasing without supersampling overhead.

---

# 14. Destructive Operations & Safety Strategy

### 14.1 Destructive Operations Register

| Operation | Why Necessary | Assets Affected | Dependencies | Safety / Rollback Measure |
| :--- | :--- | :--- | :--- | :--- |
| **Scene Backup Creation** | Safeguard current state before any edits. | `Assets/cuarto.unity` | None | Create `Assets/cuarto_backup_original.unity` prior to edits. |
| **Remove Misplaced Window** | Window at X=-106m is an accidental artifact. | `Window` at (-106.15, 1.02, -0.04) | None | Verified separate from `Window (1)`. Safe to remove. |
| **Remove 3 Redundant Directional Lights** | 4 white directional lights ruin horror lighting and crush mobile GPU. | `Directional Light (1)`, `(2)`, `(3)` | None | Keep original `Directional Light`, retune to moonlight. |
| **Remove Obsolete Closet** | Design rule: NO closet gameplay. Reclaims floor space for VR movement. | `Closet` in `cuarto.unity` | None | Verify no scripts reference `Closet`. Remove from room. |
| **Replace Cube Wall Primitives** | 9 raw cubes form crude room enclosure. | `Cube` to `Cube (9)` | None | Replace with clean modular bedroom walls/floor. |
| **Remove HDRP Remnants** | `StaticLightingSky`, `SceneIDMap`, `Bedroom HDRP` have missing scripts. | Remnant GameObjects | None | Verified HDRP demo remnants in URP project. |
| **Deprecate DoorController.cs** | Uses KeyCode.E and Screen Text; non-functional in VR. | `DoorController.cs` on `Aparment_Door` | Apartment Door prefab | Create `PhysicalVRDoor.cs` to handle VR physics before removing old script. |

---

# 15. Implementation Steps (Phased Execution)

### Phase 0: Baseline & Safety Backup
- **Step 0.1**: Save a complete duplicate of the active scene as `Assets/Scenes/cuarto_backup_original.unity`.
  - *Assigned role:* developer
  - *Dependencies:* None | *Parallelizable:* No
- **Step 0.2**: Record current Project Settings, XR Settings, and Quality Settings state into a versioned baseline document.
  - *Assigned role:* explorer
  - *Dependencies:* None | *Parallelizable:* Yes

### Phase 1: Verified Project & Platform Configuration
- **Step 1.1**: Test setting `GPUResidentDrawer` "BatchRendererGroup Variants" to "Keep All" in GraphicsSettings.
  - *Assigned role:* developer
  - *Dependencies:* Step 0.1 | *Parallelizable:* Yes
- **Step 1.2**: Evaluate Android Graphics API and test removing OpenGLES3, leaving Vulkan as primary.
  - *Assigned role:* developer
  - *Dependencies:* Step 0.1 | *Parallelizable:* Yes
- **Step 1.3**: Evaluate ASTC texture compression on Android target.
  - *Assigned role:* developer
  - *Dependencies:* Step 0.1 | *Parallelizable:* Yes
- **Step 1.4**: Verify `Mobile_RPAsset` configuration and ensure Android Quality Level references it cleanly.
  - *Assigned role:* developer
  - *Dependencies:* Step 0.1 | *Parallelizable:* Yes

### Phase 2: Room Reconstruction & Enclosure
- **Step 2.1**: Remove HDRP remnants (`StaticLightingSky`, `SceneIDMap`, `Bedroom HDRP`, `Lamp/Light` missing script) and obsolete misplaced `Window` at X=-106m.
  - *Assigned role:* developer
  - *Dependencies:* Step 0.1 | *Parallelizable:* No
- **Step 2.2**: Remove the 3 redundant Directional Lights, configuring the remaining light as dim moonlight.
  - *Assigned role:* developer
  - *Dependencies:* Step 2.1 | *Parallelizable:* Yes
- **Step 2.3**: Remove obsolete `Closet` and cube primitives (`Cube` to `Cube (9)`). Construct the 4.4m x 4.6m x 2.5m room enclosure with PBR wood flooring.
  - *Assigned role:* developer
  - *Dependencies:* Step 2.1 | *Parallelizable:* No
- **Step 2.4**: Place `Bed.prefab` at `(-1.4, 0, 0.8)` with 0.35m under-bed clearance, `Furniture01.prefab` (Nightstand) at `(-1.4, 0, -0.9)`, and position door frame opening at `(2.15, 0, -1.2)` and window opening at `(0, 1.15, 2.2)`.
  - *Assigned role:* developer
  - *Dependencies:* Step 2.3 | *Parallelizable:* No

### Phase 3: XR Rig & Meta Interaction SDK Foundation
- **Step 3.1**: Inspect existing `[[BuildingBlock] Camera Rig]`, reset root to `(0, 0, 0)`, and integrate `OVRComprehensiveInteractionRig` as a child using Meta Interaction SDK architecture.
  - *Assigned role:* developer
  - *Dependencies:* Step 2.4 | *Parallelizable:* No
- **Step 3.2**: Configure controller and hand tracking interactors (`GrabInteractor`, `HandGrabInteractor`, `PokeInteractor`) and verify dual-modality auto-switching.
  - *Assigned role:* developer
  - *Dependencies:* Step 3.1 | *Parallelizable:* Yes
- **Step 3.3**: Disable smooth locomotion and teleportation modules inside the interaction rig to enforce 100% room-scale physical walking.
  - *Assigned role:* developer
  - *Dependencies:* Step 3.1 | *Parallelizable:* Yes

### Phase 4: Physical Interactive Props
- **Step 4.1**: Build `PhysicalWindow.cs` and window prefab with sliding sash using Meta Interaction SDK `OneGrabTranslateTransformer` with min/max stops and shut event.
  - *Assigned role:* developer
  - *Dependencies:* Step 3.2 | *Parallelizable:* Yes
- **Step 4.2**: Assemble `Flashlight_VR.prefab` with `Grabbable`, `GrabInteractable`, `HandGrabInteractable`, toggle switch, Spot Light cone, and `FlashlightTool.cs`.
  - *Assigned role:* developer
  - *Dependencies:* Step 3.2 | *Parallelizable:* Yes
- **Step 4.3**: Implement `PhysicalVRDoor.cs` on `Apartment_Door` using `OneGrabRotateTransformer`, physical push resistance against monster force, and latch logic.
  - *Assigned role:* developer
  - *Dependencies:* Step 3.2 | *Parallelizable:* Yes

### Phase 5: Monster Staging & Staging Subsystems
- **Step 5.1**: Create project-owned `MonsterMutant7_Behavior.controller` referencing the existing 35 FBX animation clips (Idles, Walks, Attacks, Hits, Rage).
  - *Assigned role:* developer
  - *Dependencies:* Step 0.1 | *Parallelizable:* Yes
- **Step 5.2**: Build window exterior staging platform and monster window presence controller (`WindowMonsterStager.cs`).
  - *Assigned role:* developer
  - *Dependencies:* Step 4.1, Step 5.1 | *Parallelizable:* No
- **Step 5.3**: Build under-bed staging setup (`UnderBedMonsterStager.cs`) with light detection target collider and upward reaching arm animation.
  - *Assigned role:* developer
  - *Dependencies:* Step 4.2, Step 5.1 | *Parallelizable:* No
- **Step 5.4**: Build door exterior staging setup (`DoorMonsterStager.cs`) applying physical push torque and handle rattle.
  - *Assigned role:* developer
  - *Dependencies:* Step 4.3, Step 5.1 | *Parallelizable:* No

### Phase 6: Monster Brain & Game Director
- **Step 6.1**: Implement `MonsterBrain.cs` unified state machine handling transitions (Dormant, Stalking, Threshold Threats, Repelled, Cooldown).
  - *Assigned role:* developer
  - *Dependencies:* Steps 5.2, 5.3, 5.4 | *Parallelizable:* No
- **Step 6.2**: Implement `GameDirector.cs` managing non-RNG threat selection, pacing curves, repetition avoidance, and player attention tracking.
  - *Assigned role:* developer
  - *Dependencies:* Step 6.1 | *Parallelizable:* No

### Phase 7: Spatial Audio & Acoustic Integration
- **Step 7.1**: Implement `HorrorAudioManager.cs` routing positional audio clips through Meta XR Audio spatializer emitters at Window, Under Bed, Door, and Flashlight.
  - *Assigned role:* developer
  - *Dependencies:* Step 6.2 | *Parallelizable:* Yes

### Phase 8: Meaningful Haptics Integration
- **Step 8.1**: Implement `HorrorHapticsManager.cs` triggering physical feedback for door impact, door resistance struggle, window latch bump, and flashlight toggle.
  - *Assigned role:* developer
  - *Dependencies:* Step 6.2 | *Parallelizable:* Yes

### Phase 9: Playtesting & Parameter Balancing [C]
- **Step 9.1**: Conduct Editor and headset playtests to tune pacing parameters: monster stalking warning duration, light exposure threshold, and door push resistance curve.
  - *Assigned role:* developer
  - *Dependencies:* Steps 7.1, 8.1 | *Parallelizable:* No

### Phase 10: Quest 2 Performance Profiling & Hardware Validation
- **Step 10.1**: Profile on Meta Quest 2 hardware using OVR Metrics Tool. Verify 72 FPS stability, draw calls < 100, and zero memory allocations in update loops.
  - *Assigned role:* developer
  - *Dependencies:* Step 9.1 | *Parallelizable:* No

---

# 16. Verification Criteria (Per Phase)

- **After Phase 0:** `Assets/Scenes/cuarto_backup_original.unity` exists, loads identically to `cuarto.unity`, and has zero errors.
- **After Phase 1:** Project compiles cleanly; no OpenXR validation warnings; GraphicsSettings correctly retains BRG variants.
- **After Phase 2:** Bedroom enclosure rendered with correct 4.4m x 4.6m dimensions; no HDRP remnants; moonlight illumination active; closet absent; open walking area clear.
- **After Phase 3:** OVR Camera Rig sits at Y=0; head height is physically 1:1; hands and controllers track smoothly with synthetic hand visuals; locomotion joystick disabled.
- **After Phase 4:** Player can physically grab flashlight, toggle light, slide and shut window sash, and push/pull door with both hands and controllers.
- **After Phase 5:** Monster stages outside window, under bed, and at door with correct animation playback and zero mesh clipping.
- **After Phase 6:** Monster cycles through states; Game Director selects threats without repetition; successfully repelling a threat returns monster to cooldown.
- **After Phase 7:** 3D audio is audibly localized; player can pinpoint threat direction with eyes closed in headset.
- **After Phase 8:** Distinct haptic impulses felt on door hits, door resistance, window shut, and flashlight toggle.
- **After Phase 9:** Threat escalation feels natural, terrifying, and fair through environmental observation.
- **After Phase 10:** Standalone APK runs at stable 72 FPS on Quest 2 with no stutter or thermal throttling.

---

# 17. Risks & Mitigations

1. **Player Play Space Discrepancy:**
   - *Risk:* Real-world room may be smaller than the virtual bedroom (e.g. 2.0m x 2.0m vs 2.4m x 2.2m).
   - *Mitigation:* The bedroom layout is kept tightly compact with walking clearances tuned so all interaction points (bed edge, nightstand, window sill, door handle) fall within a standard 2.0m x 2.0m room-scale boundary.
2. **Dynamic Spot Light Fill-Rate Overhead on Quest 2:**
   - *Risk:* Real-time spotlight shadows can drop framerate below 72 FPS on mobile VR hardware.
   - *Mitigation:* The flashlight Spot Light will operate without real-time shadow casting, utilizing a high-resolution falloff cookie texture to give realistic lens definition while preserving GPU fill-rate.
3. **Hand Tracking Stability on Fast Moving Objects:**
   - *Risk:* Rapid door vibration under monster force could cause hand tracking loss.
   - *Mitigation:* The door uses a generous grab interaction trigger volume with a high release threshold (`ReleaseDistance` buffer) in Meta Interaction SDK, preventing accidental grip drops.

---

# 18. Rollback & Recovery Strategy

1. **Scene-Level Rollback:** The original scene is preserved as `Assets/Scenes/cuarto_backup_original.unity`. If room reconstruction encounters unexpected issues, reverting to the original scene is instantaneous.
2. **Prefab-Level Isolation:** All new interactive props (`PhysicalWindow`, `PhysicalVRDoor`, `Flashlight_VR`) and staging systems are built as isolated prefabs in `Assets/Prefabs/`, leaving existing asset packages (`Apartment_Door`, `MonsterMutant 7`, `BasicBedroomPack-Mavi3D`) untouched.
3. **Setting Rollback:** ProjectSettings and QualitySettings changes are recorded in Step 0.2 before modification, allowing exact restoration if build issues arise.

---

# 19. Credit-Efficient Unity AI Agent Task Breakdown

To ensure cost efficiency, minimal token waste, and precise human verification, implementation is structured into small, verifiable Agent tasks:

- **Task 1: Baseline Backup & Project Configuration Validation**
  - *Scope:* Create scene backup; check BRG variants in GraphicsSettings; inspect OpenXR validation.
  - *Files:* `ProjectSettings/GraphicsSettings.asset`, `Assets/Scenes/cuarto_backup_original.unity`.
  - *Deliverable:* Baseline verified; zero build errors.
- **Task 2: Scene Cleanup & Enclosure Reconstruction**
  - *Scope:* Remove HDRP remnants, misplaced window, closet, cube walls; build 4.4m x 4.6m enclosure with URP bed, nightstand, and door openings.
  - *Files:* `Assets/cuarto.unity`.
  - *Deliverable:* Clean bedroom rendered with moonlight and correct clearances.
- **Task 3: XR Rig Configuration & Meta Interaction SDK Setup**
  - *Scope:* Configure `OVRCameraRig`, nest `OVRComprehensiveInteractionRig`, set up hand/controller interactors, disable artificial locomotion.
  - *Files:* `Assets/cuarto.unity`.
  - *Deliverable:* Player can walk physically in VR with tracking hands/controllers.
- **Task 4: Interactive Props (Window, Door, Flashlight)**
  - *Scope:* Assemble `PhysicalWindow`, `PhysicalVRDoor`, and `Flashlight_VR` with Interaction SDK grab transformers.
  - *Files:* `Assets/Scripts/Interactions/*`, `Assets/Prefabs/*`.
  - *Deliverable:* All 3 props interactable with hands and controllers in PlayMode.
- **Task 5: Monster Animator & Staging Systems**
  - *Scope:* Create project-owned Animator controller; build window exterior stager, under-bed stager, and door stager.
  - *Files:* `Assets/Animations/Monster/*`, `Assets/Scripts/Monster/*`.
  - *Deliverable:* Monster visibly stages at all 3 thresholds with appropriate animations.
- **Task 6: Monster Brain & Game Director**
  - *Scope:* Implement `MonsterBrain.cs` and `GameDirector.cs` state machine and pacing logic.
  - *Files:* `Assets/Scripts/Monster/MonsterBrain.cs`, `Assets/Scripts/Core/GameDirector.cs`.
  - *Deliverable:* Complete threat-and-defense loop operational without button prompts.
- **Task 7: Spatial Audio, Haptics & Polish**
  - *Scope:* Meta XR Audio emitters, controller haptic impulses, and Quest 2 performance validation.
  - *Files:* `Assets/Scripts/Audio/*`, `Assets/Scripts/Haptics/*`.
  - *Deliverable:* Fully immersive, playable VR horror experience ready for Quest 2 standalone deployment.

