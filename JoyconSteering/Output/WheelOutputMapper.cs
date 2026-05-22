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

    public void Apply(double steering, JoyConState joycon, IWheelOutput sink,
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
            (throttle, brake) = ComputeThrottleBrake(joycon);
        }
        sink.SetThrottle(throttle);
        sink.SetBrake(brake);

        foreach (var (name, vjoyButton) in _config.ButtonMap)
        {
            if (vjoyButton <= 0) continue;
            var jcb = JoyConButtonNames.FromName(name);
            if (jcb == LeftJoyConButton.None) continue;
            sink.SetButton(vjoyButton, joycon.Buttons.HasFlag(jcb));
        }

        sink.Flush();
    }

    private (double Throttle, double Brake) ComputeThrottleBrake(JoyConState joycon)
    {
        switch (_config.ThrottleBrake)
        {
            case ThrottleBrakeMode.None:
                return (0, 0);
            case ThrottleBrakeMode.Buttons:
                return (joycon.Buttons.HasFlag(LeftJoyConButton.L) ? 1.0 : 0.0,
                        joycon.Buttons.HasFlag(LeftJoyConButton.Zl) ? 1.0 : 0.0);
            case ThrottleBrakeMode.Stick:
            default:
                double y = joycon.StickY;
                double dead = _config.StickDeadzone;
                double t = 0, b = 0;
                if (y > dead) t = Math.Clamp((y - dead) / (1.0 - dead), 0.0, 1.0);
                else if (y < -dead) b = Math.Clamp((-y - dead) / (1.0 - dead), 0.0, 1.0);
                return (t, b);
        }
    }
}
