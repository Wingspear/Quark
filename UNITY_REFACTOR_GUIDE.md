# Unity Editor Refactor Completion Guide

This guide will help you complete the Quark refactor inside Unity Editor after all the script changes.

## Summary of Changes

### Files Deleted:
- ❌ `Quark.cs` (old component)
- ❌ `RoomScanner.cs` (old system)
- ❌ `QuarkManagerStreamlined.cs` (old manager)
- ❌ `QuarkVFXStateBinder.cs` (redundant)
- ❌ `QuarkVFXStateDriver.cs` (redundant)
- ❌ `Assets/Scripts/Pipeline/` folder (moved to Core)

### Files Created:
- ✅ `QuarkEntity.cs` (new main component)
- ✅ `QuarkStateMachine.cs` (state management)
- ✅ `QuarkVisualController.cs` (VFX state driver)
- ✅ `QuarkAudioController.cs` (audio with fallback)
- ✅ `QuarkLifecycleOrchestrator.cs` (pipeline coordinator)
- ✅ `PalmDetector.cs` (separated concern)
- ✅ `TwoHandScaler.cs` (separated concern)
- ✅ `Assets/Scripts/Core/` folder (Pipeline components moved here)

### Files Updated:
- ✅ `QuarkManager.cs` (now uses QuarkEntity)
- ✅ `Singleton.cs` (fixed with proper Unity patterns)
- ✅ All pipeline dependencies (namespace changed to `Jusvibes.Core`)

---

## Step 1: Open Unity and Check Console

1. **Open Unity Editor**
2. **Wait for recompilation** (progress bar in bottom-right)
3. **Check Console window** (Window → General → Console)

### Expected Warnings:
You may see warnings like:
- `"The referenced script on this Behaviour is missing!"` - This is OK, we're about to fix it
- `"Missing reference"` - Also OK, we'll reassign these

### If You See Errors:
- Look for any red error messages
- Most errors will be about missing script references in scenes/prefabs
- **Take note of which GameObjects have errors**

---

## Step 2: Update Quark Prefab

### 2.1: Find Your Quark Prefab
- Navigate to your Quark prefab (likely in `Assets/Prefabs/`)
- **Right-click → Open Prefab**

### 2.2: Remove Old Components
If the prefab has these components, remove them:
- ❌ `Quark` component (old)
- ❌ `QuarkVFXStateBinder`
- ❌ `QuarkVFXStateDriver`

### 2.3: Add New Components
Add these components in this order:
1. **QuarkStateMachine**
   - `Initial State`: Dormant
   - `Log State Changes`: ✓ (enabled)

2. **QuarkVisualController**
   - `State Machine`: Drag the QuarkStateMachine component
   - `Vfx`: Drag your VisualEffect component
   - Configure visual profiles for each state (or use defaults)
   - **VFX Property Names**: Ensure these match your VFX Graph:
     - `State Property`: "QuarkState"
     - `Radius Property`: "Radius"
     - `Spawn Rate Property`: "SpawnRate"
     - `Particle Size Property`: "ParticleSize"
     - `Turbulence Property`: "TurbulenceStrength"
     - `Vortex Property`: "VortexStrength"
     - `Primary Color Property`: "PrimaryColor"
     - `Secondary Color Property`: "SecondaryColor"
     - `Alpha Property`: "Alpha"

3. **QuarkAudioController**
   - `State Machine`: Drag the QuarkStateMachine component
   - `Main Audio Source`: Drag your AudioSource component
   - `Pickup Sfx`: Drag pickup AudioSource (if you have one)
   - `Drop Sfx`: Drag drop AudioSource (if you have one)
   - `Preset Clips`: Add your fallback audio clips
   - `Shuffle Presets`: ✓ (enabled)
   - `Fade In Duration`: 0.5
   - `Fade Out Duration`: 1.0
   - `Auto Play On Ready`: ✓ (enabled)

4. **QuarkLifecycleOrchestrator**
   - `State Machine`: Drag the QuarkStateMachine component
   - `Visual Controller`: Drag the QuarkVisualController component
   - `Audio Controller`: Drag the QuarkAudioController component
   - `Capture Controller`: Drag your CaptureController from scene
   - `Whisper Recorder`: Drag your WhisperRecorder from scene
   - `Insight Processor`: Drag your CaptureInsightProcessor from scene
   - `Music Generator`: Drag your MusicGenerator from scene
   - `Capture On Grab`: ✓ (enabled)
   - `Record Voice On Grab`: ✓ (enabled)
   - `Max Retries`: 3
   - `Timeout Seconds`: 300

5. **QuarkEntity** (main component)
   - `State Machine`: Drag the QuarkStateMachine component
   - `Visual Controller`: Drag the QuarkVisualController component
   - `Audio Controller`: Drag the QuarkAudioController component
   - `Orchestrator`: Drag the QuarkLifecycleOrchestrator component
   - `Grabbable`: Drag the Grabbable component (should already be on prefab)

### 2.4: Verify Grabbable Component
- Ensure your prefab has `Grabbable` component from Oculus Interaction Toolkit
- This should already exist from your old Quark prefab

### 2.5: Save Prefab
- **Apply All** changes to the prefab
- Close prefab editor

---

## Step 3: Update QuarkManager in Scene

### 3.1: Find QuarkManager GameObject
- In your main scene hierarchy, find the GameObject with `QuarkManager`
- Select it

### 3.2: Clear Old References
Unity may show "Missing" references for removed fields. That's OK.

### 3.3: Assign New Fields
In the Inspector for QuarkManager:

**Quark Spawning:**
- `Quark Prefab`: Drag your updated QuarkEntity prefab (from Step 2)
- `Quark Spawn Parent`: Drag your wrist transform (b_l_wrist)

**Dependencies:**
- `Palm Detector`: Drag the PalmDetector GameObject (we'll create this next)

**Settings:**
- `Respawn Delay`: 3

---

## Step 4: Create PalmDetector GameObject

### 4.1: Create GameObject
- Right-click in Hierarchy → Create Empty
- Rename to "PalmDetector"
- Position: (0, 0, 0) - doesn't matter, it's just a logical component

### 4.2: Add PalmDetector Component
- With PalmDetector selected, Add Component → PalmDetector

### 4.3: Assign Fields
**Detection Settings:**
- `Palm Transform`: Drag your wrist transform (b_l_wrist)
- `Palm Facing Threshold`: -0.5
- `Main Camera`: Leave empty (auto-finds Camera.main)

**Audio Feedback:**
- `Audio Source`: Create or drag an AudioSource for "Just Vibes" sound
- `Just Vibes Clips`: Add your Just Vibes audio clips (the welcome sounds)

**Debug Visualization:**
- `Show Debug Rays`: ✓ (enabled) - shows palm direction in Scene view

### 4.4: Connect to QuarkManager
- Go back to QuarkManager GameObject
- In `Palm Detector` field, drag the PalmDetector GameObject you just created

---

## Step 5: (Optional) Create TwoHandScaler

If you want to keep two-hand pinch scaling for the world/room:

### 5.1: Create GameObject
- Right-click in Hierarchy → Create Empty
- Rename to "TwoHandScaler"

### 5.2: Add TwoHandScaler Component
- Add Component → TwoHandScaler

### 5.3: Assign Fields
**Hand References:**
- `Left Hand`: Drag your left IHand reference
- `Right Hand`: Drag your right IHand reference

**Scale Target:**
- `Scale Target`: Drag the Transform you want to scale (e.g., room/world root)

**Settings:**
- `Pinch Strength Threshold`: 0.7
- `Min Scale`: 0.3
- `Max Scale`: 3.0
- `Log Scaling`: ✗ (disabled, unless debugging)

---

## Step 6: Update ApiConfigManager (if needed)

### 6.1: Find ApiConfigManager
- Should exist as a singleton in your scene
- If not, create a new GameObject and add `ApiConfigManager` component

### 6.2: Verify References
**API Configurations:**
- `Open AI Configuration`: Drag your OpenAIConfiguration ScriptableObject
- `Suno Configuration`: Drag your SunoConfig ScriptableObject

**Note:** All scripts now reference `Jusvibes.Core.ApiConfigManager` instead of `Jusvibes.Pipeline.ApiConfigManager`

---

## Step 7: Update AudioReactiveVFX (if present)

If you have AudioReactiveVFX on your Quark prefab:

### 7.1: Verify References
- `Audio Source`: Should point to the AudioSource on QuarkAudioController
- `Vfx`: Should point to the VisualEffect
- `Music Generator`: Can leave empty or point to scene's MusicGenerator

### 7.2: Integration Note
AudioReactiveVFX is **independent** and doesn't need updates - it works with the new system automatically.

---

## Step 8: Clean Up Old Prefab Variants

### 8.1: Search for Old Quark Instances
In the Project window:
- Search: `t:Prefab Quark`
- Look for any prefabs with the old `Quark` component

### 8.2: Update or Delete
For each old prefab:
- **Option A**: Delete it (if you don't need it)
- **Option B**: Update it following Steps 2.2-2.5

---

## Step 9: Test in Play Mode

### 9.1: Enter Play Mode
- Click the Play button
- Watch the Console for errors

### 9.2: Test Workflow
1. **Palm Detection**:
   - Hold palm up facing camera → Quark should appear (Summoned state)
   - Turn palm away → Quark should disappear (Dormant state)

2. **Grab Quark**:
   - Grab the Quark → Should transition to Grabbed state
   - Should start camera capture and voice recording
   - Check Console for: `[QuarkLifecycleOrchestrator] ✊ Quark grabbed - starting capture phase`

3. **Release Quark**:
   - Release the Quark → Should transition to Dropped → Analyzing → Generating → Ready → Playing
   - Check Console for state transitions
   - Music should start playing when Ready state is reached

4. **Verify Logs**:
   Look for these log messages:
   - `[PalmDetector] Palm facing camera`
   - `[QuarkManager] Spawned new Quark`
   - `[QuarkStateMachine] ✊ Dormant → Grabbed`
   - `[QuarkLifecycleOrchestrator] 📸 Image captured`
   - `[QuarkLifecycleOrchestrator] 🎤 Transcription complete`
   - `[QuarkLifecycleOrchestrator] 🔍 Analysis complete`
   - `[QuarkLifecycleOrchestrator] 🎵 Music generated`
   - `[QuarkStateMachine] ✅ Generating → Ready`
   - `[QuarkStateMachine] ▶️ Ready → Playing`
   - `[QuarkAudioController] ▶️ Playing: [clip name]`

### 9.3: Test Fallback
To test preset audio fallback:
- Temporarily disconnect your internet or break the API keys
- Grab and release Quark
- Should see: `[QuarkStateMachine] ❌ [State] → Error`
- Should hear preset audio playing

---

## Step 10: Final Cleanup

### 10.1: Remove Old Meta Files
Unity should automatically remove `.meta` files for deleted scripts. If you see any warnings about missing scripts:
- Select the GameObject/Prefab with the warning
- Remove the missing script component
- Save

### 10.2: Verify No Errors
- Console should have 0 errors (warnings are OK)
- All Quarks should work end-to-end

### 10.3: Save Scene
- File → Save Scene
- File → Save Project

---

## Troubleshooting

### Issue: "The referenced script on this Behaviour is missing"
**Solution:** This means a GameObject is referencing a deleted script (like old Quark.cs)
- Find the GameObject in the hierarchy
- Remove the missing component
- Add the new components (see Step 2)

### Issue: "NullReferenceException" in QuarkManager
**Solution:** QuarkManager is missing a reference
- Check that `quarkPrefab` is assigned
- Check that `palmDetector` is assigned
- Check that `quarkSpawnParent` is assigned

### Issue: "NullReferenceException" in PalmDetector
**Solution:** PalmDetector is missing palmTransform
- Assign the wrist transform (b_l_wrist) to `palmTransform` field

### Issue: Quark doesn't appear when palm faces camera
**Solution:**
- Check PalmDetector is assigned in QuarkManager
- Check palm transform is assigned in PalmDetector
- Check that Quark prefab is correctly configured
- Enable debug rays in PalmDetector to see palm direction

### Issue: Music doesn't play after releasing Quark
**Solution:**
- Check QuarkLifecycleOrchestrator has all dependencies assigned
- Check Console for pipeline errors
- Verify API keys are correct in ApiConfigManager
- Check that preset audio clips are assigned in QuarkAudioController

### Issue: Compiler errors about missing namespaces
**Solution:**
- Close and reopen Unity Editor (force full recompilation)
- Check that all files are in correct folders:
  - Core scripts in `Assets/Scripts/Core/`
  - Quark scripts in `Assets/Scripts/Quark/`

### Issue: Cannot find Jusvibes.Core namespace
**Solution:**
- Verify Core folder exists: `Assets/Scripts/Core/`
- Verify all Core scripts have `namespace Jusvibes.Core` at the top
- Reimport all scripts: Right-click Assets → Reimport All

---

## Architecture Overview (For Reference)

### Component Hierarchy:
```
QuarkEntity (main component)
├── QuarkStateMachine (state management)
├── QuarkVisualController (VFX)
├── QuarkAudioController (audio + fallback)
└── QuarkLifecycleOrchestrator (pipeline)
    ├── CaptureController (camera)
    ├── WhisperRecorder (voice)
    ├── CaptureInsightProcessor (OpenAI vision)
    └── MusicGenerator (Suno)
```

### Manager Components:
```
QuarkManager (singleton)
└── PalmDetector (separate GameObject)

TwoHandScaler (optional, separate GameObject)

ApiConfigManager (singleton, DontDestroyOnLoad)
```

### Event Flow:
```
Palm faces camera
  → PalmDetector.OnPalmFacingCamera
  → QuarkManager.HandlePalmFacingCamera()
  → QuarkEntity.Summon()
  → QuarkStateMachine.SetState(Summoned)
  → QuarkVisualController sees state change
  → VFX updates

Grab Quark
  → QuarkEntity.HandleGrab()
  → QuarkLifecycleOrchestrator.NotifyGrabbed()
  → Starts capture + voice recording

Release Quark
  → QuarkEntity.HandleRelease()
  → QuarkLifecycleOrchestrator.NotifyReleased()
  → Processes pipeline: Analyze → Generate → Ready → Play
```

---

## Next Steps After Refactor

1. **Test all edge cases** (errors, timeouts, retries)
2. **Adjust visual profiles** in QuarkVisualController to match your aesthetic
3. **Tune audio fade times** in QuarkAudioController
4. **Configure pipeline timeouts** in QuarkLifecycleOrchestrator
5. **Add more preset audio clips** for fallback variety

---

## Questions?

If you encounter issues not covered here:
1. Check Unity Console for specific error messages
2. Verify all serialized fields are assigned
3. Check that namespaces are correct (`Jusvibes.Core` for pipeline)
4. Ensure API keys are configured in ApiConfigManager

Good luck! 🚀
