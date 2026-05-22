namespace JoyconSteering.JoyCon.Fusion;

/// <summary>
/// Estimates and removes the gyroscope's DC bias.
///
/// MEMS gyros (the Joy-Con included) report a small non-zero rate even when fully stationary.
/// Over time this integrates into yaw drift, which is the dominant error source for IMU-only
/// orientation tracking (no magnetometer to anchor yaw).
///
/// On startup we collect <see cref="SampleCount"/> samples while the device is stationary,
/// compute the per-axis mean, and treat that as the bias. Every subsequent sample has the
/// bias subtracted before it reaches the filter.
///
/// Call <see cref="Restart"/> to redo calibration mid-session (e.g., if temperature drift
/// has shifted the bias).
/// </summary>
public sealed class GyroBiasCalibrator
{
    public int SampleCount { get; }

    private int _collected;
    private double _sumX, _sumY, _sumZ;
    private double _biasX, _biasY, _biasZ;

    public GyroBiasCalibrator(int sampleCount = 200)
    {
        if (sampleCount <= 0) throw new ArgumentOutOfRangeException(nameof(sampleCount));
        SampleCount = sampleCount;
    }

    /// <summary>True once enough samples have been collected and the bias is fixed.</summary>
    public bool IsCalibrated => _collected >= SampleCount;

    public double BiasXDps => _biasX;
    public double BiasYDps => _biasY;
    public double BiasZDps => _biasZ;

    /// <summary>
    /// Continuous bias refinement, called per-sample only when the controller is detected
    /// as stationary (gyro magnitude under a tight threshold). Slowly nudges the bias
    /// toward the current reading to track thermal drift mid-session.
    ///
    /// alpha is a very small EWMA coefficient (per-sample). At 200 Hz with alpha=0.0005,
    /// the time constant is ~10 seconds — enough to follow slow temperature changes,
    /// short enough to ignore single noisy samples.
    /// </summary>
    public void UpdateRunning(ImuSample stationarySample, double alpha = 0.0005)
    {
        if (!IsCalibrated) return;
        _biasX = (1 - alpha) * _biasX + alpha * stationarySample.GxDps;
        _biasY = (1 - alpha) * _biasY + alpha * stationarySample.GyDps;
        _biasZ = (1 - alpha) * _biasZ + alpha * stationarySample.GzDps;
    }

    /// <summary>Throw away the current calibration and start collecting again.</summary>
    public void Restart()
    {
        _collected = 0;
        _sumX = _sumY = _sumZ = 0;
        _biasX = _biasY = _biasZ = 0;
    }

    /// <summary>
    /// Returns the sample with its gyro bias subtracted (after calibration completes).
    /// During the collection phase, returns the sample unchanged so the filter still
    /// runs but its yaw will be ignored anyway until calibration finishes.
    /// </summary>
    public ImuSample Apply(ImuSample sample)
    {
        if (_collected < SampleCount)
        {
            _sumX += sample.GxDps;
            _sumY += sample.GyDps;
            _sumZ += sample.GzDps;
            _collected++;
            if (_collected == SampleCount)
            {
                _biasX = _sumX / SampleCount;
                _biasY = _sumY / SampleCount;
                _biasZ = _sumZ / SampleCount;
            }
            return sample;
        }
        return sample with
        {
            GxDps = sample.GxDps - _biasX,
            GyDps = sample.GyDps - _biasY,
            GzDps = sample.GzDps - _biasZ,
        };
    }
}
