namespace JoyconSteering.JoyCon;

public readonly record struct ImuSample(
    double AxG,   double AyG,   double AzG,    // accelerometer in g
    double GxDps, double GyDps, double GzDps); // gyroscope in deg/sec
