namespace JoyconSteering.JoyCon.Fusion;

/// <summary>
/// Gravity-anchored wheel angle ("Mario Kart" style). Uses the FILTERED gravity vector
/// from <see cref="MadgwickFilter"/> (gyro+accel fusion) rather than raw accelerometer
/// readings, so it's smooth even during fast motion when linear acceleration would
/// otherwise contaminate raw accel.
///
/// Works for the typical "face-toward-user" wheel grip: the controller's face axis
/// (body Z) is the wheel rotation axis; body X and Y spin in the vertical plane as
/// the wheel turns. The projection of gravity onto body XY rotates with the wheel.
///
/// **Drift-free** — the angle is computed fresh from the current gravity vector each
/// tick (no integration, no accumulator). Trade-off: bounded to ±180° relative to
/// the neutral position; past that it wraps. If you need unbounded tracking, use the
/// gyro-integrated <see cref="WheelAxisIntegrator"/> ("Wheel" axis) instead.
/// </summary>
public sealed class GravityTiltAxis
{
    private double _neutralRad;
    private bool _hasNeutral;
    private double _lastRawRad;

    /// <summary>Wheel angle in degrees, relative to last recenter (-180..+180].</summary>
    public double AngleDegrees { get; private set; }

    /// <summary>
    /// Feed the body-frame gravity vector (from MadgwickFilter.GravityInBody, or raw
    /// accel as a fallback). Only the X and Y components are used.
    /// </summary>
    public void Update(double gravityX, double gravityY)
    {
        double rawRad = Math.Atan2(gravityX, gravityY);
        _lastRawRad = rawRad;

        if (!_hasNeutral)
        {
            _neutralRad = rawRad;
            _hasNeutral = true;
        }

        // Shortest-path delta from neutral, wrapped to (-π, +π].
        double relative = rawRad - _neutralRad;
        while (relative > Math.PI) relative -= 2 * Math.PI;
        while (relative < -Math.PI) relative += 2 * Math.PI;
        AngleDegrees = relative * 180.0 / Math.PI;
    }

    /// <summary>Capture the current sample's gravity direction as the new zero.</summary>
    public void SetNeutral()
    {
        _neutralRad = _lastRawRad;
        _hasNeutral = true;
        AngleDegrees = 0;
    }

    public void Reset()
    {
        _hasNeutral = false;
        _lastRawRad = 0;
        _neutralRad = 0;
        AngleDegrees = 0;
    }
}
