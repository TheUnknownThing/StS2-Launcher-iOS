using System;
using STS2MobileIos;

static void Equal(ulong expected, ulong actual)
{
    if (expected != actual) throw new Exception($"Expected {expected}, got {actual}");
}

var window = new FrameWindow();
window.Tick(0);
window.Tick(16_667);
window.Tick(33_334);
Equal(33_334, window.Elapsed);
window.ClearSample();
window.Tick(50_001);
Equal(16_667, window.Elapsed); // Sampling boundaries must not lose a frame.
window.ClearSample();
window.ResetClock();
window.Tick(10_000_000);
window.Tick(10_016_667);
Equal(16_667, window.Elapsed); // A suspension must never become a ten-second frame.
window.Tick(11_016_667);
Equal(1_000_000, window.Percentile(0.99)); // Real foreground stalls remain visible.
Equal(16_667, window.Percentile(0.50));
Console.WriteLine("Frame accounting tests passed.");

var hold = new LongPressGesture();
hold.Begin(0, 10, 10);
if (hold.Update(499_999, 10, 10, true)) throw new Exception("Preview opened before hold threshold");
if (!hold.Update(500_000, 11, 12, true)) throw new Exception("Stationary hold did not open preview");
if (hold.Update(600_000, 11, 12, true)) throw new Exception("Hold opened twice");
hold.Begin(0, 10, 10);
if (hold.Update(600_000, 10, 10, false)) throw new Exception("Release opened preview");
hold.Begin(0, 10, 10);
hold.Update(200_000, 50, 10, true);
if (hold.Update(600_000, 10, 10, true)) throw new Exception("Drag returning to start opened preview");
hold.Begin(0, 10, 10);
hold.Cancel();
if (hold.Update(600_000, 10, 10, true)) throw new Exception("Cancelled hold opened preview");
Console.WriteLine("Long-press gesture tests passed.");
