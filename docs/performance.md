# Performance and benchmarking

See [the measured iPad results](benchmarks/2026-10-02.md): gameplay averaged
59.50 FPS over 95 seconds, with a 29.04 ms p99 frame interval. Scene transitions
still produced longer stalls.

## Live display

Tap **iOS > Show FPS** to display frame rate and average frame interval. This
preference persists across launches. The display updates twice per second and
does not record files, change the frame cap, or enable the detailed profiler.
It measures engine process cadence, not actual screen presentation. Background
time is excluded.

## Capture a benchmark

With the profiled build running on the iPad, keep it foreground and play a normal
run while this command captures two minutes of data:

Enable `"profile": true` in `ios/config.local.json`, build and install the app,
then launch it. Profiling is off in the public config template. The recorder
writes individual frame intervals to `Documents/benchmarks/` and flushes a summary
every five seconds. Background/resume resets the clock to exclude suspended time.

```sh
python3 scripts/ios/benchmark.py capture --seconds 120 \
  --udid YOUR_PHYSICAL_IPAD_UDID \
  --pymobiledevice3 /path/to/pymobiledevice3
```

Use the physical UDID for pymobiledevice3; `config.local.json` supplies the
CoreDevice identifier for retrieving the app's frame logs. The command uses the
local pymobiledevice3 checkout's DVT graphics and process-monitor commands over
the existing paired connection. It starts profiler clients, leaves the game
running when finished, and copies only benchmark logs from the app.

Install [uv](https://docs.astral.sh/uv/) to run the optional profiler. The reference
capture used a development checkout of
[pymobiledevice3](https://github.com/doronz88/pymobiledevice3) with `developer dvt
graphics` and `developer dvt sysmon process monitor process --keep-monitoring`.
CLI layouts vary by version; check that your checkout supports both commands.
The profiler is optional and is not downloaded by `bootstrap.py`. To avoid it,
copy the app's frame JSONL through file sharing and run `benchmark.py report`;
frame statistics work without CPU/GPU counters.

Results are stored under `.cache/benchmarks/<timestamp>/`: raw JSONL, `report.json`,
`report.md`, and a standalone `fps.svg` chart with sample tooltips. CPU figures use
one CPU core as 100%; memory is the app's physical footprint. GPU utilization and
compositor FPS are device-wide counters. Keep other apps in the background.

To regenerate a report for the same interval from saved data:

```sh
python3 scripts/ios/benchmark.py report FRAME_LOG \
  --process PROCESS_LOG --graphics GRAPHICS_LOG \
  --window CAPTURE_JSON --output .cache/benchmarks/report
```

The report calculates FPS from total frames / total active elapsed time, frame
time p50/p95/p99, counts above 50/100 ms, and 1% low FPS (reciprocal of the mean of
the slowest 1% of intervals). Separate scene groups distinguish menu, run, loading,
and batches spanning a transition. `run` includes combat, map, reward and pause
screens within a run. These are process-frame timings, not GPU presentation times.

Only complete batches within the capture interval are used. The app flushes every
five seconds, so data at the edges can be omitted. This is a warm-cache session;
do not describe it as a clean-install or deterministic replay benchmark. The
profiling overhead is included. Memory leaks, thermal throttling and battery life
require longer controlled sessions.

```sh
.tools/dotnet/dotnet run --project tests/ios/FrameWindowTests.csproj -c Release
python3 -m unittest discover -s scripts/ios -p 'test_benchmark.py'
```

Profiling is opt-in at build time. Set `"profile": false` and rebuild/install to
stop recording on later launches. Raw logs are ignored by Git.
