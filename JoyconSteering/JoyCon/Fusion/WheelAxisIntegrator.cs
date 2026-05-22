namespace JoyconSteering.JoyCon.Fusion;

/// <summary>
/// Tracks rotation around the controller's body-frame Z axis (perpendicular to the front
/// face). For the typical sideways "wheel grip" — Joy-Con held with the face pointing up,
/// rotated like a steering wheel — this is the wheel rotation regardless of how the grip
/// is tilted forward, sideways, or otherwise.
///
/// Unlike the Madgwick Euler "yaw" output, which is world-frame and wraps at ±180°, this
/// integrator outputs an UNBOUNDED angle. Past full lock keeps clamping (in SteeringMath)
/// rather than wrapping around to the opposite side.
///
/// Input samples should already have gyro bias subtracted (run GyroBiasCalibrator first).
/// Stationary noise is rejected by the gyro magnitude ZUPT in <see cref="MadgwickFilter"/>;
/// here we apply the same threshold to the Z-axis component directly.
/// </summary>
public sealed class WheelAxisIntegrator
{
    private readonly double _zuptThresholdDps;

    /// <summary>Accumulated rotation around body Z, in degrees. Unbounded.</summary>
    public double AngleDegrees { get; private set; }

    public WheelAxisIntegrator(double zuptThresholdDps = 2.0)
    {
        _zuptThresholdDps = zuptThresholdDps;
    }

    public void Reset() => AngleDegrees = 0;

    /// <summary>Integrate one sample. <paramref name="dtSeconds"/> is the sample period (5 ms on Joy-Con).</summary>
    public void Apply(ImuSample sample, double dtSeconds)
    {
        double gz = sample.GzDps;

        // ZUPT on the wheel axis: when the controller is held still, suppress integration
        // so residual noise/bias doesn't drift. Threshold is on the magnitude of the full
        // gyro vector (matches MadgwickFilter), not just Z, so slow user motion off-axis
        // doesn't accidentally allow Z noise through.
        if (_zuptThresholdDps > 0)
        {
            double mag = Math.Sqrt(sample.GxDps * sample.GxDps
                                 + sample.GyDps * sample.GyDps
                                 + sample.GzDps * sample.GzDps);
            if (mag < _zuptThresholdDps) gz = 0;
        }

        AngleDegrees += gz * dtSeconds;
    }
}
