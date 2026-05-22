using System.Drawing;
using System.Windows.Forms;

namespace JoyconSteering.Ui;

/// <summary>
/// A simple horizontal bar that shows a current value with an inset deadzone band.
///
/// Two modes:
///   * Bipolar (e.g. steering): value ∈ [-1, +1], zero is centred. Deadzone is a
///     symmetric band around the centre. Position indicator can be left or right
///     of centre.
///   * Unipolar (e.g. throttle/brake): value ∈ [0, 1], zero on the left, one on
///     the right. Deadzone is a band at the left (below which value is treated as
///     zero by the underlying logic).
///
/// Cosmetic only — does not produce any input, just visualises what the worker
/// already computed.
/// </summary>
internal sealed class PositionBar : Control
{
    private double _value;
    private double _deadzone;
    private readonly bool _bipolar;
    private readonly Color _activeColor;

    public PositionBar(bool bipolar, Color activeColor)
    {
        _bipolar = bipolar;
        _activeColor = activeColor;
        DoubleBuffered = true;
        Height = 24;
    }

    public double Value
    {
        get => _value;
        set { if (_value != value) { _value = value; Invalidate(); } }
    }

    /// <summary>
    /// Deadzone size. For bipolar bars: a fraction of the half-range (0..1) where
    /// 0 means no deadzone and 1 means the whole bar is deadzone. For unipolar
    /// bars: a fraction of the full range (0..1) measured from the zero end.
    /// </summary>
    public double Deadzone
    {
        get => _deadzone;
        set { if (_deadzone != value) { _deadzone = Math.Clamp(value, 0, 1); Invalidate(); } }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var rect = new Rectangle(0, 0, Width - 1, Height - 1);

        // Frame
        using (var bg = new SolidBrush(Color.FromArgb(230, 230, 230))) g.FillRectangle(bg, rect);
        using (var border = new Pen(Color.FromArgb(170, 170, 170))) g.DrawRectangle(border, rect);

        // Deadzone band — visibly darker than background
        using var dzBrush = new SolidBrush(Color.FromArgb(210, 200, 200));
        if (_bipolar)
        {
            // Symmetric band around the centre
            int mid = Width / 2;
            int half = (int)Math.Round(_deadzone * (Width / 2.0));
            g.FillRectangle(dzBrush, mid - half, 1, half * 2, Height - 2);
        }
        else
        {
            int dzWidth = (int)Math.Round(_deadzone * Width);
            g.FillRectangle(dzBrush, 1, 1, Math.Max(0, dzWidth - 1), Height - 2);
        }

        // Filled portion (the active region — how much pedal is applied or how far the wheel is from centre)
        using var fill = new SolidBrush(_activeColor);
        if (_bipolar)
        {
            int mid = Width / 2;
            double v = Math.Clamp(_value, -1, 1);
            int fillWidth = (int)Math.Round(Math.Abs(v) * (Width / 2.0));
            if (v >= 0) g.FillRectangle(fill, mid, 2, fillWidth, Height - 4);
            else        g.FillRectangle(fill, mid - fillWidth, 2, fillWidth, Height - 4);

            // Centre tick
            using var midPen = new Pen(Color.FromArgb(80, 80, 80), 1);
            g.DrawLine(midPen, mid, 0, mid, Height);
        }
        else
        {
            double v = Math.Clamp(_value, 0, 1);
            int fillWidth = (int)Math.Round(v * Width);
            g.FillRectangle(fill, 1, 2, Math.Max(0, fillWidth - 1), Height - 4);
        }
    }
}
