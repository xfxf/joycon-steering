namespace JoyconSteering.Steering;

/// <summary>
/// Maps an analog stick's Y axis to (throttle, brake) with a centered deadzone.
/// Used by both the steering-joycon stick path and the pedal-joycon stick path.
///
/// Convention: stick Y in [-1, +1]. Positive Y (stick pushed up) → throttle.
/// Negative Y (pulled down) → brake. Within ±<paramref name="deadzone"/> the
/// output is (0, 0); past it the response is linear and clamped to [0, 1].
/// </summary>
public static class StickPedalMath
{
    public static (double Throttle, double Brake) Compute(double stickY, double deadzone)
    {
        double dz = Math.Clamp(deadzone, 0, 0.99);
        if (stickY > dz)
            return (Math.Clamp((stickY - dz) / (1.0 - dz), 0, 1), 0);
        if (stickY < -dz)
            return (0, Math.Clamp((-stickY - dz) / (1.0 - dz), 0, 1));
        return (0, 0);
    }
}
