namespace JoyconSteering.Steering;

public sealed record TiltPedalSettings(
    double RangeDegrees,     // tilt this much (each side) for full throttle / brake
    double DeadzoneDegrees,  // centered deadband where both are zero
    bool Invert);            // swap throttle and brake directions

/// <summary>
/// Pure mapping: signed tilt angle → (throttle, brake) ∈ [0,1]².
///
/// Positive tilt (e.g., tilt forward / lean wrist down) → throttle ramps up,
/// brake = 0. Negative tilt → brake ramps up, throttle = 0. Inside the
/// deadzone, both are zero. Past full range, the active pedal clamps at 1.0.
/// Setting <see cref="TiltPedalSettings.Invert"/> swaps which direction
/// drives which pedal.
/// </summary>
public static class TiltPedalMath
{
    public static (double Throttle, double Brake) Compute(double angleDegrees, TiltPedalSettings s)
    {
        if (s.RangeDegrees <= 0) return (0, 0);
        double dead = Math.Max(0, s.DeadzoneDegrees);
        double mag = Math.Abs(angleDegrees);
        if (mag <= dead) return (0, 0);

        double span = s.RangeDegrees - dead;
        if (span <= 0) span = s.RangeDegrees;
        double v = Math.Clamp((mag - dead) / span, 0.0, 1.0);

        bool positive = angleDegrees > 0;
        if (s.Invert) positive = !positive;
        return positive ? (v, 0) : (0, v);
    }
}
