namespace JoyconSteering.Steering;

public sealed record SteeringSettings(
    double RangeDegrees,
    double DeadzoneDegrees,
    double SmoothingMs,
    bool Invert);

/// <summary>
/// Position-based steering: maps a physical rotation angle (deg) to a wheel axis in [-1, +1].
/// Tracks a re-centerable origin; supports a centered deadzone, linear scaling to range,
/// optional invert, and time-aware exponential smoothing.
///
/// Math: RangeDegrees = how far the user tilts in EACH direction for full lock.
/// RangeDegrees = 90 means full lock at ±90°. RangeDegrees = 180 means full lock at ±180°.
/// </summary>
public sealed class SteeringMath
{
    private readonly SteeringSettings _settings;
    private double _centerOffsetDeg;
    private double _smoothedOutput;

    public SteeringMath(SteeringSettings settings)
    {
        _settings = settings;
        _centerOffsetDeg = 0;
        _smoothedOutput = 0;
    }

    public void Recenter(double currentAngleDegrees) => _centerOffsetDeg = currentAngleDegrees;

    /// <summary>Compute axis value (-1..+1) for current physical angle and time delta in ms.</summary>
    public double Compute(double angleDegrees, double dtMs)
    {
        double relative = angleDegrees - _centerOffsetDeg;
        double halfRange = _settings.RangeDegrees;
        if (halfRange <= 0) return 0;

        double dead = _settings.DeadzoneDegrees;
        double sign = Math.Sign(relative);
        double magnitude = Math.Abs(relative);

        double target;
        if (magnitude <= dead)
        {
            target = 0;
        }
        else
        {
            double span = halfRange - dead;
            if (span <= 0) span = halfRange;
            target = sign * Math.Clamp((magnitude - dead) / span, 0.0, 1.0);
        }

        if (_settings.Invert) target = -target;

        if (_settings.SmoothingMs > 0 && dtMs > 0)
        {
            // EWMA with time constant tau = SmoothingMs.
            // alpha = 1 - exp(-dt/tau).
            double alpha = 1.0 - Math.Exp(-dtMs / _settings.SmoothingMs);
            _smoothedOutput += alpha * (target - _smoothedOutput);
            return _smoothedOutput;
        }

        _smoothedOutput = target;
        return target;
    }
}

public enum SelectedAxis { Roll, Pitch, Yaw, Wheel, Tilt }

public static class SteeringAxisSelector
{
    public static SelectedAxis Resolve(JoyconSteering.Config.SteeringAxis axis, JoyconSteering.Config.JoyConSide side)
        => axis switch
        {
            JoyconSteering.Config.SteeringAxis.Roll => SelectedAxis.Roll,
            JoyconSteering.Config.SteeringAxis.Pitch => SelectedAxis.Pitch,
            JoyconSteering.Config.SteeringAxis.Yaw => SelectedAxis.Yaw,
            JoyconSteering.Config.SteeringAxis.Wheel => SelectedAxis.Wheel,
            JoyconSteering.Config.SteeringAxis.Tilt => SelectedAxis.Tilt,
            // Auto: gravity-anchored tilt — drift-free and unwrapped past ±180°.
            // The previous default was Wheel (gyro integration), but tilt holds the
            // centre rock-solid across a long session in exchange for a slightly
            // softer feel near full lock. Better default for most users.
            _ => SelectedAxis.Tilt,
        };
}
