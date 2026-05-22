using JoyconSteering.Config;
using JoyconSteering.JoyCon;

namespace JoyconSteering.Output;

/// <summary>
/// Routes one tick of state into the wheel output: steering axis, throttle/brake from
/// configured source, and all button mappings.
/// </summary>
public sealed class WheelOutputMapper
{
    private readonly AppConfig _config;

    public WheelOutputMapper(AppConfig config) => _config = config;

    /// <summary>
    /// Apply one tick of input to the vJoy sink.
    ///
    /// <paramref name="rawButtons"/> is the steering Joy-Con's raw button word — bits
    /// from either <see cref="LeftJoyConButton"/> or <see cref="RightJoyConButton"/>,
    /// interpreted via <see cref="_config"/>.Side. This indirection means the same
    /// mapper works whichever side the user has chosen for steering.
    /// </summary>
    public void Apply(double steering, double stickX, double stickY, uint rawButtons, IWheelOutput sink,
        (double Throttle, double Brake)? externalPedals = null)
    {
        sink.SetSteering(Math.Clamp(steering, -1.0, 1.0));

        double throttle, brake;
        if (PedalsConfigHelper.RequiresPedalJoyCon(_config.ThrottleBrake))
        {
            // Pedal joycon is the source; if it hasn't reported yet, safe zero.
            (throttle, brake) = externalPedals ?? (0, 0);
            throttle = Math.Clamp(throttle, 0.0, 1.0);
            brake    = Math.Clamp(brake,    0.0, 1.0);
        }
        else
        {
            (throttle, brake) = ComputeThrottleBrake(stickX, stickY, rawButtons);
        }
        sink.SetThrottle(throttle);
        sink.SetBrake(brake);

        foreach (var (name, vjoyButton) in _config.ButtonMap)
        {
            if (vjoyButton <= 0) continue;
            sink.SetButton(vjoyButton, JoyConSideButtons.IsPressed(_config.Side, rawButtons, name));
        }

        sink.Flush();
    }

    private (double Throttle, double Brake) ComputeThrottleBrake(double stickX, double stickY, uint rawButtons)
    {
        switch (_config.ThrottleBrake)
        {
            case ThrottleBrakeMode.None:
                return (0, 0);
            case ThrottleBrakeMode.Buttons:
                // Shoulder buttons for whichever side is the steering Joy-Con
                // (L+ZL on left, R+ZR on right).
                uint throttleBit = JoyConSideButtons.ThrottleShoulderBit(_config.Side);
                uint brakeBit    = JoyConSideButtons.BrakeShoulderBit(_config.Side);
                return ((rawButtons & throttleBit) != 0 ? 1.0 : 0.0,
                        (rawButtons & brakeBit)    != 0 ? 1.0 : 0.0);
            case ThrottleBrakeMode.Stick:
            default:
                double stickAxisValue = _config.StickAxis == StickAxis.X ? stickX : stickY;
                return Steering.StickPedalMath.Compute(stickAxisValue, _config.StickDeadzone);
        }
    }
}
