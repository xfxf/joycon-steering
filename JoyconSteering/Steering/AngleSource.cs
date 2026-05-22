namespace JoyconSteering.Steering;

/// <summary>Pure helper: pick the configured Euler component from a (roll, pitch, yaw) triple.</summary>
public static class AngleSource
{
    public static double Pick(SelectedAxis axis, double roll, double pitch, double yaw, double wheel) => axis switch
    {
        SelectedAxis.Pitch => pitch,
        SelectedAxis.Yaw => yaw,
        SelectedAxis.Wheel => wheel,
        _ => roll,
    };
}

/// <summary>Detects rising edges on a boolean signal. Used for "press button to recenter".</summary>
public sealed class RisingEdgeDetector
{
    private bool _prev;
    public bool Update(bool current)
    {
        bool rising = current && !_prev;
        _prev = current;
        return rising;
    }
}
