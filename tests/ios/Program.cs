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
