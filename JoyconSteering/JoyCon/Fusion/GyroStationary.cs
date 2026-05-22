namespace JoyconSteering.JoyCon.Fusion;

/// <summary>
/// Shared "is the controller still right now" check. The two workers
/// (SteeringWorker, PedalJoyConWorker) both inspect each packet's three IMU
/// samples and decide whether to refine the gyro bias estimate; centralised
/// here so they can't drift apart.
/// </summary>
public static class GyroStationary
{
    private static double MagnitudeSquared(ImuSample s)
        => s.GxDps * s.GxDps + s.GyDps * s.GyDps + s.GzDps * s.GzDps;

    /// <summary>
    /// True when the peak gyro magnitude across the three samples is below
    /// <paramref name="thresholdDps"/>. The peak (not the mean) is used so
    /// that a single noisy sample in an otherwise still packet doesn't
    /// trigger a stationary update.
    ///
    /// Compares squared magnitudes against the squared threshold so the hot
    /// path doesn't pay three Math.Sqrt calls per packet.
    /// </summary>
    public static bool IsStationary(ImuSample s0, ImuSample s1, ImuSample s2, double thresholdDps)
    {
        double thresholdSq = thresholdDps * thresholdDps;
        return MagnitudeSquared(s0) < thresholdSq
            && MagnitudeSquared(s1) < thresholdSq
            && MagnitudeSquared(s2) < thresholdSq;
    }
}
