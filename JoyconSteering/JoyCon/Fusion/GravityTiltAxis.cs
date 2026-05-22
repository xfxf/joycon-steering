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
/// **Drift-free** because gravity is always there. **Unwrapped** — past ±180° doesn't
/// flip; the accumulated angle grows without bound and SteeringMath clamps cleanly.
/// </summary>
public sealed class GravityTiltAxis
{
    private double _neutralAccumRad;
    private bool _hasNeutral;

    // Continuous (unwrapped) accumulated angle since reset, in radians.
    private double _accumRad;
    private double _lastRawRad;
    private bool _hasLastRaw;

    /// <summary>Wheel angle in degrees, relative to last recenter. UNBOUNDED.</summary>
    public double AngleDegrees { get; private set; }

    /// <summary>
    /// Feed the body-frame gravity vector (from MadgwickFilter.GravityInBody, or raw
    /// accel as a fallback). Only the X and Y components are used.
    /// </summary>
    public void Update(double gravityX, double gravityY)
    {
        double rawRad = Math.Atan2(gravityX, gravityY);

        if (!_hasLastRaw)
        {
            _lastRawRad = rawRad;
            _accumRad = rawRad;
            _hasLastRaw = true;
        }
        else
        {
            // Unwrap: if the raw atan2 jumped by more than π, it crossed the ±π
            // discontinuity. Add the inverse to keep _accumRad continuous.
            double delta = rawRad - _lastRawRad;
            if (delta > Math.PI) delta -= 2 * Math.PI;
            else if (delta < -Math.PI) delta += 2 * Math.PI;
            _accumRad += delta;
            _lastRawRad = rawRad;
        }

        if (!_hasNeutral)
        {
            _neutralAccumRad = _accumRad;
            _hasNeutral = true;
        }

        AngleDegrees = (_accumRad - _neutralAccumRad) * 180.0 / Math.PI;
    }

    /// <summary>Capture the current accumulated angle as the new zero.</summary>
    public void SetNeutral()
    {
        _neutralAccumRad = _accumRad;
        _hasNeutral = true;
        AngleDegrees = 0;
    }

    public void Reset()
    {
        _hasNeutral = false;
        _hasLastRaw = false;
        _accumRad = 0;
        _neutralAccumRad = 0;
        AngleDegrees = 0;
    }
}
