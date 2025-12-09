/// <summary>
/// Simplified lifecycle states for a Quark from spawn to reactive audio playback.
/// States have been combined for clearer user experience.
/// </summary>
public enum QuarkLifecycleState
{
    /// <summary>
    /// Initial state - Quark is dormant on the palm (white dot, minimal effects)
    /// </summary>
    Dormant,

    /// <summary>
    /// User's palm is facing camera - Quark becomes visible and ready to grab
    /// </summary>
    Summoned,

    /// <summary>
    /// User grabbed the Quark - capturing environment (camera + voice recording)
    /// Combines: Grabbed + Capturing states
    /// </summary>
    Grabbed,

    /// <summary>
    /// Processing and generating music (analyzing image/voice + generating with Suno)
    /// Combines: Dropped + Analyzing + Generating states
    /// </summary>
    Generating,

    /// <summary>
    /// Music is loaded and ready to play
    /// </summary>
    Ready,

    /// <summary>
    /// Actively playing generated music with audio-reactive visuals
    /// </summary>
    Playing,

    /// <summary>
    /// Music finished or paused - idle but fully initialized
    /// </summary>
    Idle,

    /// <summary>
    /// Error occurred - will use preset audio as fallback
    /// </summary>
    Error
}
