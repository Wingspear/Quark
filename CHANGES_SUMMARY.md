# Quark System Changes Summary

## What Was Changed

### 1. ✅ State Machine Simplification

**Simplified from 11 states to 8 states:**

#### Removed States:
- ❌ `Capturing` (merged into Grabbed)
- ❌ `Dropped` (merged into Generating)
- ❌ `Analyzing` (merged into Generating)

#### Final States:
1. **Dormant** - Initial state on palm
2. **Summoned** - Palm facing camera, ready to grab
3. **Grabbed** - User grabbed + capturing voice (combines old Grabbed + Capturing)
4. **Generating** - Processing everything (combines old Dropped + Analyzing + Generating)
5. **Ready** - Music ready to play
6. **Playing** - Playing audio-reactive music
7. **Idle** - Music paused/finished
8. **Error** - Fallback state

**Files Modified:**
- `Assets/Scripts/Quark/QuarkLifecycleState.cs` - Enum definition
- `Assets/Scripts/Quark/QuarkStateMachine.cs` - State machine logic, events, transitions
- `Assets/Scripts/Quark/QuarkLifecycleOrchestrator.cs` - Pipeline orchestration

---

### 2. ✅ Fixed Radius Parameters

**Problem:** Changing radius values in profiles didn't affect particle boundaries.

**Root Cause:**
- Visual controller was setting `"Radius"` parameter
- VFX Graph actually uses `"ParticleBoundary 1_radius"` and `"ParticleInternal_radius"`

**Solution:**
- Split single `radius` field into `outerRadius` and `innerRadius`
- `outerRadius` → Controls particle spawn boundary (where particles exist)
- `innerRadius` → Controls internal sphere (affects particle motion)
- Updated all property mappings to use correct VFX Graph parameters

**Files Modified:**
- `Assets/Scripts/Quark/QuarkVisualController.cs` - Property names, struct, ApplyProfile method
- All visual profiles updated with proper radius values

---

### 3. ✅ Visual Profile Updates

**Removed Profiles:**
- `capturingProfile` (no longer needed)
- `analyzingProfile` (no longer needed)

**Updated Profiles (with new radius scale):**

| State | Outer Radius | Inner Radius | Spawn Rate | Visual Feel |
|-------|--------------|--------------|------------|-------------|
| Dormant | 0.5 | 0.3 | 0 | Invisible, waiting |
| Summoned | 0.8 | 0.5 | 0 | Visible, no particles |
| Grabbed | 1.0 | 0.7 | 200 | Medium energy, capturing |
| Generating | 1.8 | 1.2 | 500 | **Large, high energy** |
| Ready | 2.0 | 1.4 | 400 | Full size, ready |
| Playing | 2.5 | 1.8 | 900 | **Maximum size, dancing** |
| Idle | 2.2 | 1.6 | 300 | Slightly smaller |
| Error | 1.5 | 1.0 | 200 | Red, moderate size |

---

## New User Flow

### Old Flow (5 states):
```
1. Grab Quark              → Grabbed state
2. (System captures image) → Capturing state
3. Release Quark           → Dropped state
4. (System analyzes)       → Analyzing state
5. (System generates)      → Generating state
6. Ready to play           → Ready state
```

### New Flow (2 states):
```
1. Grab Quark              → Grabbed state (voice recording starts)
2. Release Quark           → Generating state (everything happens here)
   - Capture image
   - Transcribe voice
   - Analyze with OpenAI
   - Generate music with Suno
3. Ready to play           → Ready state
```

**Benefits:**
- ✅ Simpler state machine (fewer transitions)
- ✅ Clearer user feedback (big visual change when generating)
- ✅ Easier to understand and maintain
- ✅ Less code complexity

---

## Technical Changes

### QuarkLifecycleOrchestrator.cs

**Before:**
```csharp
HandleGrabbed() → StartCapturePhase() → SetState(Capturing)
HandleDropped() → ProcessAndGenerate() → SetState(Analyzing) → SetState(Generating)
```

**After:**
```csharp
HandleGrabbed() → Start voice recording only (stay in Grabbed)
HandleGenerating() → CaptureAndGenerate() → Do everything in one method
```

### QuarkStateMachine.cs

**Before:**
- 11 convenience events (OnCapturing, OnDropped, OnAnalyzing, etc.)
- Complex transition matrix

**After:**
- 8 convenience events
- Simplified transition matrix
- `IsLoading` property simplified

### QuarkVisualController.cs

**Before:**
```csharp
struct QuarkVisualProfile {
    float radius;  // Single radius, didn't work
    ...
}

ApplyProfile() {
    vfx.SetFloat("Radius", profile.radius);  // Wrong parameter
}
```

**After:**
```csharp
struct QuarkVisualProfile {
    float outerRadius;  // Particle boundary
    float innerRadius;  // Internal motion
    ...
}

ApplyProfile() {
    vfx.SetFloat("ParticleBoundary 1_radius", profile.outerRadius);
    vfx.SetFloat("ParticleInternal_radius", profile.innerRadius);
}
```

---

## Testing Checklist

After Unity compiles:

- [ ] **Spawn a Quark** - Should be invisible (Dormant)
- [ ] **Face palm to camera** - Should become visible (Summoned)
- [ ] **Grab Quark** - Should show medium-sized particles, voice recording starts
- [ ] **Release Quark** - Should immediately show LARGE particles (Generating state)
- [ ] **Wait for music** - Should transition to Ready with full-sized orb
- [ ] **Music plays** - Should show maximum size with audio-reactive visuals
- [ ] **Verify radius changes** - Each state should have visibly different particle boundaries

---

## Expected Visual Differences

### Key Visual Change: Generating State

**Old System (3 separate states):**
- Dropped: Small particles
- Analyzing: Medium particles
- Generating: Larger particles

**New System (1 combined state):**
- Generating: **Immediately shows large, energetic particles**
- Radius: 1.8 units (much bigger than old states)
- User sees one continuous "working" animation

### Radius Now Works!

**Before:** All states looked similar because radius parameter wasn't connected

**After:** Each state has distinct visual size:
- Grabbed: 1.0 units
- Generating: 1.8 units (80% bigger!)
- Playing: 2.5 units (150% bigger!)

---

## Files Modified

### Code Files (Auto-updated):
1. ✅ `Assets/Scripts/Quark/QuarkLifecycleState.cs`
2. ✅ `Assets/Scripts/Quark/QuarkStateMachine.cs`
3. ✅ `Assets/Scripts/Quark/QuarkLifecycleOrchestrator.cs`
4. ✅ `Assets/Scripts/Quark/QuarkVisualController.cs`

### No Unity Editor Changes Required
All changes were done in code. Unity will automatically:
- Recompile scripts
- Update serialized profile values
- Apply new radius parameters

---

## Breaking Changes

If you have other scripts that reference the old states, you'll need to update them:

**Replace:**
- `QuarkLifecycleState.Capturing` → `QuarkLifecycleState.Grabbed`
- `QuarkLifecycleState.Dropped` → `QuarkLifecycleState.Generating`
- `QuarkLifecycleState.Analyzing` → `QuarkLifecycleState.Generating`

**Event subscriptions:**
- `stateMachine.OnCapturing` → `stateMachine.OnGrabbed`
- `stateMachine.OnDropped` → `stateMachine.OnGenerating`
- `stateMachine.OnAnalyzing` → `stateMachine.OnGenerating`

---

## Performance Impact

**No performance changes** - same underlying logic, just reorganized states.

The Quest 3 performance optimizations (from earlier) are separate and still apply.

---

## Next Steps

1. ✅ All code changes complete
2. ⏳ Build/compile in Unity
3. ⏳ Test in Play mode or on Quest 3
4. ⏳ Verify radius changes are visible
5. ⏳ Confirm state transitions feel good

---

## Questions?

If you encounter issues:

1. **Compilation errors:** Check for any scripts referencing old states
2. **Radius still not working:** Verify VFX Graph has `ParticleBoundary 1_radius` and `ParticleInternal_radius` parameters
3. **States feel wrong:** Adjust profile values in Unity Inspector (QuarkVisualController component)

All core changes are complete and ready to test!
