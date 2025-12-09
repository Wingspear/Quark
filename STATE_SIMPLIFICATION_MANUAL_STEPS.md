# State Simplification & Radius Fix - Manual Steps Required

## Summary of Code Changes Made

### ✅ Completed Automatically:

1. **QuarkLifecycleState.cs** - Simplified enum
   - Removed: `Capturing`, `Dropped`, `Analyzing`
   - Kept: `Dormant`, `Summoned`, `Grabbed`, `Generating`, `Ready`, `Playing`, `Idle`, `Error`

2. **QuarkStateMachine.cs** - Updated state machine
   - Removed events: `OnCapturing`, `OnDropped`, `OnAnalyzing`
   - Simplified `IsLoading` property
   - Updated valid transitions
   - Fixed state emojis

3. **QuarkLifecycleOrchestrator.cs** - Simplified pipeline
   - Combined capture+analyze+generate into `CaptureAndGenerate()`
   - Grabbed state: Starts voice recording only
   - Generating state: Does everything (capture image, transcribe, analyze, generate music)

4. **QuarkVisualProfile struct** - Fixed radius
   - Changed from single `radius` field to `outerRadius` and `innerRadius`
   - Updated Lerp and Equals methods

5. **QuarkVisualController** - Added proper radius properties
   - Changed from `radiusProperty` to `outerRadiusProperty` and `innerRadiusProperty`
   - Now correctly maps to VFX Graph parameters

---

## ⚠️ Manual Steps Required in Unity Editor

You need to update the visual profiles in QuarkVisualController. Here's what needs to change:

### Step 1: Open QuarkVisualController in Inspector

1. Open Unity Editor
2. Select your Quark prefab
3. Find the `QuarkVisualController` component

### Step 2: Update Each Profile

For each profile (Dormant, Summoned, Grabbed, etc.), you'll see the old ` radius` field has been split into two fields:
- `outerRadius` - Controls particle boundary (where particles can exist)
- `innerRadius` - Controls internal sphere (affects particle motion)

**Recommended Values:**

#### Dormant Profile
```
outerRadius: 0.5
innerRadius: 0.3
spawn Rate: 0
particleSize: 0.02
turbulence: 0
vortex: 0
alpha: 0
```

#### Summoned Profile
```
outerRadius: 0.8
innerRadius: 0.5
spawnRate: 0
particleSize: 0.02
turbulence: 0
vortex: 0
alpha: 0
```

#### Grabbed Profile (includes capturing now!)
```
outerRadius: 1.0
innerRadius: 0.7
spawnRate: 200
particleSize: 0.03
turbulence: 0.5
vortex: 0.2
alpha: 1.0
primaryColor: Cyan
secondaryColor: Blue
```

#### Generating Profile (combines analyzing + generating!)
```
outerRadius: 1.8
innerRadius: 1.2
spawnRate: 500
particleSize: 0.05
turbulence: 1.5
vortex: 0.6
alpha: 1.0
primaryColor: Orange/Yellow
secondaryColor: Green
```

#### Ready Profile
```
outerRadius: 2.0
innerRadius: 1.4
spawnRate: 400
particleSize: 0.045
turbulence: 1.0
vortex: 0.3
alpha: 1.0
primaryColor: Magenta
secondaryColor: Cyan
```

#### Playing Profile
```
outerRadius: 2.5
innerRadius: 1.8
spawnRate: 900
particleSize: 0.05
turbulence: 2.0
vortex: 0.3
alpha: 1.0
```

#### Idle Profile
```
outerRadius: 2.2
innerRadius: 1.6
spawnRate: 300
particleSize: 0.04
turbulence: 0.8
vortex: 0.2
alpha: 0.9
```

#### Error Profile
```
outerRadius: 1.5
innerRadius: 1.0
spawnRate: 200
particleSize: 0.035
turbulence: 0.5
vortex: 0.1
alpha: 0.8
primaryColor: Red
secondaryColor: Dark Orange
```

### Step 3: Remove Old Profiles

You should also **remove** these profiles since their states no longer exist:
- ❌ `capturingProfile` (merged into Grabbed)
- ❌ `analyzingProfile` (merged into Generating)

### Step 4: Update ApplyProfile Method

Open `QuarkVisualController.cs` and find the `ApplyProfile` method (around line 309-342).

Replace this line:
```csharp
if (!string.IsNullOrEmpty(radiusProperty) && vfx.HasFloat(radiusProperty))
    vfx.SetFloat(radiusProperty, profile.radius);
```

With these two lines:
```csharp
if (!string.IsNullOrEmpty(outerRadiusProperty) && vfx.HasFloat(outerRadiusProperty))
    vfx.SetFloat(outerRadiusProperty, profile.outerRadius);

if (!string.IsNullOrEmpty(innerRadiusProperty) && vfx.HasFloat(innerRadiusProperty))
    vfx.SetFloat(innerRadiusProperty, profile.innerRadius);
```

### Step 5: Update GetProfile Method

In `QuarkVisualController.cs`, find the `GetProfile` method (around line 234-263).

Remove these cases:
```csharp
QuarkLifecycleState.Capturing => capturingProfile,
QuarkLifecycleState.Dropped => analyzingProfile,
QuarkLifecycleState.Analyzing => analyzingProfile,
```

The switch statement should now be:
```csharp
var profile = state switch
{
    QuarkLifecycleState.Dormant => dormantProfile,
    QuarkLifecycleState.Summoned => summonedProfile,
    QuarkLifecycleState.Grabbed => grabbedProfile,
    QuarkLifecycleState.Generating => generatingProfile,
    QuarkLifecycleState.Ready => readyProfile,
    QuarkLifecycleState.Playing => playingProfile,
    QuarkLifecycleState.Idle => idleProfile,
    QuarkLifecycleState.Error => errorProfile,
    _ => dormantProfile
};
```

---

## Testing Checklist

After making these changes:

1. [ ] Build/compile in Unity (check for no errors)
2. [ ] Enter Play mode
3. [ ] Spawn a Quark
4. [ ] **Grab the Quark** - Should see particles with moderate size
5. [ ] **Release the Quark** - Should transition to Generating (large, energetic particles)
6. [ ] **Verify radius changes** - Particles should visibly grow/shrink with each state
7. [ ] Audio should generate successfully
8. [ ] Quark should transition to Playing state with audio-reactive visuals

---

## What Changed Conceptually

### Old Flow:
```
Grabbed → Capturing → Dropped → Analyzing → Generating → Ready
```

### New Flow:
```
Grabbed (capturing voice) → Generating (capture + analyze + generate) → Ready
```

### Key Differences:

1. **Grabbed state** now includes capturing
   - Voice recording starts immediately
   - Visual feedback shows user is being captured

2. **Generating state** is one unified state
   - Captures image
   - Transcribes voice
   - Analyzes with OpenAI
   - Generates music with Suno
   - Shows big, energetic particles throughout

3. **Radius now works properly**
   - `outerRadius` = particle containment sphere
   - `innerRadius` = internal motion sphere
   - Both are set correctly in VFX Graph

---

## Troubleshooting

**Issue: Particles still don't change size**
- Check that VFX Graph parameters `ParticleBoundary 1_radius` and `ParticleInternal_radius` exist
- Open VFX Graph and verify these properties are exposed
- Check QuarkVisualController inspector shows both radius properties set correctly

**Issue: Compilation errors about missing states**
- Make sure you removed references to `Capturing`, `Dropped`, `Analyzing` states
- Check for any other scripts that might reference old states

**Issue: States transition too fast**
- This is expected - the new flow is simpler and faster
- Generating state handles everything in one go

**Issue: Visual transitions feel abrupt**
- The `transitionTime` setting in QuarkVisualController controls smoothness
- Default is 0.5s, you can increase it for smoother transitions

---

## Files Modified

### Automatically (by me):
- ✅ `Assets/Scripts/Quark/QuarkLifecycleState.cs`
- ✅ `Assets/Scripts/Quark/QuarkStateMachine.cs`
- ✅ `Assets/Scripts/Quark/QuarkLifecycleOrchestrator.cs`
- ✅ `Assets/Scripts/Quark/QuarkVisualController.cs` (struct only)

### Manually (by you in Unity Editor):
- ⏳ Update profile values in QuarkVisualController component
- ⏳ Remove old profile references in code
- ⏳ Update ApplyProfile method
- ⏳ Update GetProfile method
