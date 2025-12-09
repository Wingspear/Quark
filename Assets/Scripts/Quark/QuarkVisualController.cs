using UnityEngine;
using UnityEngine.VFX;

/// <summary>
/// Controls Quark VFX based on lifecycle state.
/// Provides smooth transitions and state-specific visual profiles.
/// </summary>
public class QuarkVisualController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private QuarkStateMachine stateMachine;
        [SerializeField] private VisualEffect vfx;

        [Header("VFX Property Names")]
        [SerializeField] private string stateProperty = "QuarkState";
        [SerializeField] private string outerRadiusProperty = "ParticleBoundary 1_radius";
        [SerializeField] private string innerRadiusProperty = "ParticleInternal_radius";
        [SerializeField] private string spawnRateProperty = "SpawnRate";
        [SerializeField] private string particleSizeProperty = "ParticleSize";
        [SerializeField] private string turbulenceProperty = "TurbulenceIntensity";
        [SerializeField] private string vortexProperty = "VortexStrength";
        [SerializeField] private string primaryColorProperty = "PrimaryColor";
        [SerializeField] private string secondaryColorProperty = "SecondaryColor";
        [SerializeField] private string alphaProperty = "Alpha";

        [Header("State Visual Profiles")]
        [SerializeField] private QuarkVisualProfile dormantProfile = new()
        {
            outerRadius = 0.5f,
            innerRadius = 0.3f,
            spawnRate = 0f,  // No particles - simple orb
            particleSize = 0.02f,
            turbulence = 0f,  // No movement
            vortex = 0f,
            alpha = 0f,  // Fully transparent particles
            primaryColor = Color.white,
            secondaryColor = Color.gray
        };

        [SerializeField] private QuarkVisualProfile summonedProfile = new()
        {
            outerRadius = 0.8f,
            innerRadius = 0.5f,
            spawnRate = 0f,  // No particles - simple orb only
            particleSize = 0.02f,
            turbulence = 0f,  // No dancing/movement
            vortex = 0f,
            alpha = 0f,  // Fully transparent particles
            primaryColor = Color.white,
            secondaryColor = new Color(0.8f, 0.8f, 1f)
        };

        [SerializeField] private QuarkVisualProfile grabbedProfile = new()
        {
            outerRadius = 1.0f,
            innerRadius = 0.7f,
            spawnRate = 200f,
            particleSize = 0.03f,
            turbulence = 0.5f,
            vortex = 0.2f,
            alpha = 1f,
            primaryColor = Color.cyan,
            secondaryColor = Color.blue
        };

        [SerializeField] private QuarkVisualProfile generatingProfile = new()
        {
            outerRadius = 1.8f,
            innerRadius = 1.2f,
            spawnRate = 500f,
            particleSize = 0.05f,
            turbulence = 1.5f,
            vortex = 0.6f,
            alpha = 1f,
            primaryColor = new Color(1f, 0.5f, 0f), // Orange
            secondaryColor = Color.yellow
        };

        [SerializeField] private QuarkVisualProfile readyProfile = new()
        {
            outerRadius = 2.0f,
            innerRadius = 1.4f,
            spawnRate = 400f,
            particleSize = 0.045f,
            turbulence = 1f,
            vortex = 0.3f,
            alpha = 1f,
            primaryColor = Color.magenta,
            secondaryColor = Color.cyan
        };

        [SerializeField] private QuarkVisualProfile playingProfile = new()
        {
            outerRadius = 2.5f,
            innerRadius = 1.8f,
            spawnRate = 900f,
            particleSize = 0.05f,
            turbulence = 2f,
            vortex = 0.3f,
            alpha = 1f,
            // Colors will be driven by AudioReactiveVFX in this state
            primaryColor = Color.magenta,
            secondaryColor = Color.cyan
        };

        [SerializeField] private QuarkVisualProfile idleProfile = new()
        {
            outerRadius = 2.2f,
            innerRadius = 1.6f,
            spawnRate = 300f,
            particleSize = 0.04f,
            turbulence = 0.8f,
            vortex = 0.2f,
            alpha = 0.9f,
            primaryColor = Color.blue,
            secondaryColor = Color.cyan
        };

        [SerializeField] private QuarkVisualProfile errorProfile = new()
        {
            outerRadius = 1.5f,
            innerRadius = 1.0f,
            spawnRate = 200f,
            particleSize = 0.035f,
            turbulence = 0.5f,
            vortex = 0.1f,
            alpha = 0.8f,
            primaryColor = Color.red,
            secondaryColor = new Color(1f, 0.3f, 0f)
        };

        [Header("Transition Settings")]
        [SerializeField] private float transitionTime = 0.5f;
        [SerializeField] private AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        // Injected colors (from environment analysis)
        private Color? _injectedPrimaryColor;
        private Color? _injectedSecondaryColor;

        // Transition state
        private QuarkVisualProfile _currentProfile;
        private QuarkVisualProfile _targetProfile;
        private float _transitionProgress = 1f;
        
        // Cache previous values to avoid unnecessary VFX updates
        private QuarkVisualProfile _lastAppliedProfile;

        private void Awake()
        {
            if (stateMachine == null)
                stateMachine = GetComponent<QuarkStateMachine>();

            if (vfx == null)
                vfx = GetComponentInChildren<VisualEffect>();

            if (stateMachine != null)
            {
                stateMachine.OnStateChanged += OnStateChanged;
            }

            _currentProfile = GetProfile(QuarkLifecycleState.Dormant);
            _targetProfile = _currentProfile;
        }
        
        private void OnEnable()
        {
            // When GameObject is enabled (e.g., when summoned), immediately apply the current profile
            // This prevents VFX Graph from using default values before our script runs
            if (vfx != null && stateMachine != null)
            {
                var currentState = stateMachine.CurrentState;
                var profile = GetProfile(currentState);
                // Immediately set spawnRate to prevent particles from spawning
                if (!string.IsNullOrEmpty(spawnRateProperty) && vfx.HasFloat(spawnRateProperty))
                {
                    vfx.SetFloat(spawnRateProperty, profile.spawnRate);
                }
            }
        }

        private void OnDestroy()
        {
            if (stateMachine != null)
            {
                stateMachine.OnStateChanged -= OnStateChanged;
            }
        }

        private void Update()
        {
            if (vfx == null) return;

            // Smooth transition (but don't lerp spawnRate - it's set immediately)
            if (_transitionProgress < 1f)
            {
                _transitionProgress += Time.deltaTime / transitionTime;
                float t = Mathf.Clamp01(_transitionProgress);
                float curved = transitionCurve.Evaluate(t);

                _currentProfile = QuarkVisualProfile.Lerp(_currentProfile, _targetProfile, curved);
                
                // Keep spawnRate at target value (don't lerp it - prevents particle bursts)
                _currentProfile.spawnRate = _targetProfile.spawnRate;
            }

            // Only apply if profile actually changed (reduces unnecessary VFX updates)
            if (!_currentProfile.Equals(_lastAppliedProfile))
            {
                ApplyProfile(_currentProfile);
                _lastAppliedProfile = _currentProfile;
            }
        }

        /// <summary>
        /// Inject colors from environment analysis
        /// </summary>
        public void InjectColors(Color primary, Color secondary)
        {
            _injectedPrimaryColor = primary;
            _injectedSecondaryColor = secondary;

            Debug.Log($"[QuarkVisualController] Colors injected: Primary={primary}, Secondary={secondary}");
        }

        /// <summary>
        /// Clear injected colors and return to profile defaults
        /// </summary>
        public void ClearInjectedColors()
        {
            _injectedPrimaryColor = null;
            _injectedSecondaryColor = null;
        }

        private void OnStateChanged(QuarkLifecycleState previous, QuarkLifecycleState current)
        {
            _targetProfile = GetProfile(current);
            _transitionProgress = 0f;

            // Immediately set spawnRate to target value to prevent particle bursts
            // This is critical when transitioning between states with different spawn rates
            _currentProfile.spawnRate = _targetProfile.spawnRate;

            // Set the state int for VFX Graph (if needed)
            if (vfx != null && !string.IsNullOrEmpty(stateProperty))
            {
                vfx.SetInt(stateProperty, (int)current);
            }
            
            // Immediately apply spawnRate to VFX Graph to prevent particles from spawning during transition
            // This is especially important when transitioning to states with spawnRate = 0
            if (vfx != null && !string.IsNullOrEmpty(spawnRateProperty) && vfx.HasFloat(spawnRateProperty))
            {
                vfx.SetFloat(spawnRateProperty, _targetProfile.spawnRate);
            }
            
            // Force immediate application of all properties to avoid lerping artifacts
            // This ensures smooth transitions without particle bursts
            ApplyProfile(_currentProfile);
            _lastAppliedProfile = _currentProfile;
        }

        private QuarkVisualProfile GetProfile(QuarkLifecycleState state)
        {
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

            // Apply injected colors for states after analysis
            if (_injectedPrimaryColor.HasValue && state >= QuarkLifecycleState.Ready)
            {
                profile.primaryColor = _injectedPrimaryColor.Value;
            }
            if (_injectedSecondaryColor.HasValue && state >= QuarkLifecycleState.Ready)
            {
                profile.secondaryColor = _injectedSecondaryColor.Value;
            }

            return profile;
        }

        private void ApplyProfile(QuarkVisualProfile profile)
        {
            if (vfx == null) return;

            // Batch VFX property updates to reduce overhead
            // Only update if values actually changed to avoid unnecessary GPU calls
            if (!string.IsNullOrEmpty(outerRadiusProperty) && vfx.HasFloat(outerRadiusProperty))
                vfx.SetFloat(outerRadiusProperty, profile.outerRadius);

            if (!string.IsNullOrEmpty(innerRadiusProperty) && vfx.HasFloat(innerRadiusProperty))
                vfx.SetFloat(innerRadiusProperty, profile.innerRadius);

            if (!string.IsNullOrEmpty(spawnRateProperty) && vfx.HasFloat(spawnRateProperty))
                vfx.SetFloat(spawnRateProperty, profile.spawnRate);

            if (!string.IsNullOrEmpty(particleSizeProperty) && vfx.HasFloat(particleSizeProperty))
                vfx.SetFloat(particleSizeProperty, profile.particleSize);

            if (!string.IsNullOrEmpty(turbulenceProperty) && vfx.HasFloat(turbulenceProperty))
                vfx.SetFloat(turbulenceProperty, profile.turbulence);

            if (!string.IsNullOrEmpty(vortexProperty) && vfx.HasFloat(vortexProperty))
                vfx.SetFloat(vortexProperty, profile.vortex);

            if (!string.IsNullOrEmpty(alphaProperty) && vfx.HasFloat(alphaProperty))
                vfx.SetFloat(alphaProperty, profile.alpha);

            // Only apply colors if not in Playing state (AudioReactiveVFX handles that)
            if (stateMachine == null || !stateMachine.IsInState(QuarkLifecycleState.Playing))
            {
                if (!string.IsNullOrEmpty(primaryColorProperty) && vfx.HasVector4(primaryColorProperty))
                    vfx.SetVector4(primaryColorProperty, profile.primaryColor);

                if (!string.IsNullOrEmpty(secondaryColorProperty) && vfx.HasVector4(secondaryColorProperty))
                    vfx.SetVector4(secondaryColorProperty, profile.secondaryColor);
            }
        }
    }

[System.Serializable]
public struct QuarkVisualProfile
{
    public float outerRadius;  // Particle boundary (where particles spawn/exist)
    public float innerRadius;  // Internal sphere (affects particle motion)
    public float spawnRate;
    public float particleSize;
    public float turbulence;
    public float vortex;
    public float alpha;
    public Color primaryColor;
    public Color secondaryColor;

    public static QuarkVisualProfile Lerp(QuarkVisualProfile a, QuarkVisualProfile b, float t)
    {
        return new QuarkVisualProfile
        {
            outerRadius = Mathf.Lerp(a.outerRadius, b.outerRadius, t),
            innerRadius = Mathf.Lerp(a.innerRadius, b.innerRadius, t),
            spawnRate = Mathf.Lerp(a.spawnRate, b.spawnRate, t),
            particleSize = Mathf.Lerp(a.particleSize, b.particleSize, t),
            turbulence = Mathf.Lerp(a.turbulence, b.turbulence, t),
            vortex = Mathf.Lerp(a.vortex, b.vortex, t),
            alpha = Mathf.Lerp(a.alpha, b.alpha, t),
            primaryColor = Color.Lerp(a.primaryColor, b.primaryColor, t),
            secondaryColor = Color.Lerp(a.secondaryColor, b.secondaryColor, t)
        };
    }

    // Efficient comparison for change detection
    public bool Equals(QuarkVisualProfile other)
    {
        return Mathf.Approximately(outerRadius, other.outerRadius) &&
               Mathf.Approximately(innerRadius, other.innerRadius) &&
               Mathf.Approximately(spawnRate, other.spawnRate) &&
               Mathf.Approximately(particleSize, other.particleSize) &&
               Mathf.Approximately(turbulence, other.turbulence) &&
               Mathf.Approximately(vortex, other.vortex) &&
               Mathf.Approximately(alpha, other.alpha) &&
               primaryColor == other.primaryColor &&
               secondaryColor == other.secondaryColor;
    }
}
