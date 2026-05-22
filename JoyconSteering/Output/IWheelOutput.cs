namespace JoyconSteering.Output;

/// <summary>
/// Abstract wheel output sink. Real implementation drives vJoy; tests use a fake.
///
/// Steering: -1..+1 (full left .. full right).
/// Throttle / Brake: 0..+1 each.
/// Buttons are addressed by 1-based vJoy button number (1..128).
/// </summary>
public interface IWheelOutput
{
    void SetSteering(double value);
    void SetThrottle(double value);
    void SetBrake(double value);
    void SetButton(int buttonNumber, bool pressed);
    void Flush();
}
