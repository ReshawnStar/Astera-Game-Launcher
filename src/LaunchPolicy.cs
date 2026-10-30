using System;

namespace Astera
{
    // Waiting time is not an animation speed. Keep OS/process work off the UI
    // thread and independent from the six-dot/loading animations.
    static class LaunchPolicy
    {
        internal static readonly TimeSpan Timeout = TimeSpan.FromMinutes(2);
        internal static readonly TimeSpan WindowProbeInterval = TimeSpan.FromSeconds(1);
        internal static readonly TimeSpan GameProbeInterval = TimeSpan.FromSeconds(2.5);
    }
}
