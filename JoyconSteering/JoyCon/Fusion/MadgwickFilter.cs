namespace JoyconSteering.JoyCon.Fusion;

/// <summary>
/// IMU-only Madgwick AHRS filter (no magnetometer). Maintains a unit orientation quaternion
/// updated from gyro (rad/s) and corrected toward gravity (accel in g).
///
/// Reference: Madgwick, "An efficient orientation filter for inertial and inertial/magnetic
/// sensor arrays" (2010). https://x-io.co.uk/downloads/madgwick_internal_report.pdf
///
/// Convention: body frame matches Joy-Con IMU (X right, Y forward along length, Z up out of face).
/// Euler output is ZYX intrinsic (yaw, pitch, roll). Pitch is clamped to ±90°.
/// </summary>
public sealed class MadgwickFilter
{
    private readonly double _beta;
    private readonly double _zuptThresholdDps;
    private double _q0 = 1, _q1 = 0, _q2 = 0, _q3 = 0;

    /// <param name="beta">Madgwick gain. 0.04-0.10 is typical.</param>
    /// <param name="zuptThresholdDps">
    /// Below this gyro magnitude (deg/sec), assume the device is stationary and skip
    /// gyro integration entirely (Zero-Velocity Update). Prevents yaw drift accumulation.
    /// Set to 0 to disable.
    /// </param>
    public MadgwickFilter(double beta, double zuptThresholdDps = 2.0)
    {
        _beta = beta;
        _zuptThresholdDps = zuptThresholdDps;
    }

    public void Reset()
    {
        _q0 = 1; _q1 = 0; _q2 = 0; _q3 = 0;
    }

    public void Update(ImuSample sample, double dtSeconds)
    {
        double gxDps = sample.GxDps;
        double gyDps = sample.GyDps;
        double gzDps = sample.GzDps;

        // ZUPT: if the gyro magnitude is below the noise floor, treat as stationary.
        // This keeps yaw stable when the controller is held still.
        if (_zuptThresholdDps > 0)
        {
            double mag = Math.Sqrt(gxDps * gxDps + gyDps * gyDps + gzDps * gzDps);
            if (mag < _zuptThresholdDps) { gxDps = gyDps = gzDps = 0; }
        }

        // Convert gyro deg/s → rad/s
        double gx = gxDps * Math.PI / 180.0;
        double gy = gyDps * Math.PI / 180.0;
        double gz = gzDps * Math.PI / 180.0;

        double ax = sample.AxG, ay = sample.AyG, az = sample.AzG;

        // Quaternion derivative from gyro
        double qDot0 = 0.5 * (-_q1 * gx - _q2 * gy - _q3 * gz);
        double qDot1 = 0.5 * (_q0 * gx + _q2 * gz - _q3 * gy);
        double qDot2 = 0.5 * (_q0 * gy - _q1 * gz + _q3 * gx);
        double qDot3 = 0.5 * (_q0 * gz + _q1 * gy - _q2 * gx);

        double accelNorm = Math.Sqrt(ax * ax + ay * ay + az * az);
        if (accelNorm > 1e-9)
        {
            // Normalize accelerometer
            ax /= accelNorm; ay /= accelNorm; az /= accelNorm;

            // Gradient descent algorithm corrective step
            double s0 = -2 * _q2 * (2 * _q1 * _q3 - 2 * _q0 * _q2 - ax)
                       + 2 * _q1 * (2 * _q0 * _q1 + 2 * _q2 * _q3 - ay);
            double s1 =  2 * _q3 * (2 * _q1 * _q3 - 2 * _q0 * _q2 - ax)
                       + 2 * _q0 * (2 * _q0 * _q1 + 2 * _q2 * _q3 - ay)
                       - 4 * _q1 * (1 - 2 * _q1 * _q1 - 2 * _q2 * _q2 - az);
            double s2 = -2 * _q0 * (2 * _q1 * _q3 - 2 * _q0 * _q2 - ax)
                       + 2 * _q3 * (2 * _q0 * _q1 + 2 * _q2 * _q3 - ay)
                       - 4 * _q2 * (1 - 2 * _q1 * _q1 - 2 * _q2 * _q2 - az);
            double s3 =  2 * _q1 * (2 * _q1 * _q3 - 2 * _q0 * _q2 - ax)
                       + 2 * _q2 * (2 * _q0 * _q1 + 2 * _q2 * _q3 - ay);
            double sNorm = Math.Sqrt(s0 * s0 + s1 * s1 + s2 * s2 + s3 * s3);
            if (sNorm > 1e-9)
            {
                s0 /= sNorm; s1 /= sNorm; s2 /= sNorm; s3 /= sNorm;
                qDot0 -= _beta * s0;
                qDot1 -= _beta * s1;
                qDot2 -= _beta * s2;
                qDot3 -= _beta * s3;
            }
        }

        // Integrate
        _q0 += qDot0 * dtSeconds;
        _q1 += qDot1 * dtSeconds;
        _q2 += qDot2 * dtSeconds;
        _q3 += qDot3 * dtSeconds;

        // Normalize quaternion
        double qNorm = Math.Sqrt(_q0 * _q0 + _q1 * _q1 + _q2 * _q2 + _q3 * _q3);
        if (qNorm > 1e-9)
        {
            _q0 /= qNorm; _q1 /= qNorm; _q2 /= qNorm; _q3 /= qNorm;
        }
    }

    /// <summary>Return (roll, pitch, yaw) in degrees, ZYX intrinsic convention.</summary>
    public (double Roll, double Pitch, double Yaw) GetEulerDegrees()
    {
        double sinrCosp = 2 * (_q0 * _q1 + _q2 * _q3);
        double cosrCosp = 1 - 2 * (_q1 * _q1 + _q2 * _q2);
        double roll = Math.Atan2(sinrCosp, cosrCosp);

        double sinp = 2 * (_q0 * _q2 - _q3 * _q1);
        double pitch = Math.Abs(sinp) >= 1 ? Math.CopySign(Math.PI / 2, sinp) : Math.Asin(sinp);

        double sinyCosp = 2 * (_q0 * _q3 + _q1 * _q2);
        double cosyCosp = 1 - 2 * (_q2 * _q2 + _q3 * _q3);
        double yaw = Math.Atan2(sinyCosp, cosyCosp);

        const double r2d = 180.0 / Math.PI;
        return (roll * r2d, pitch * r2d, yaw * r2d);
    }
}
