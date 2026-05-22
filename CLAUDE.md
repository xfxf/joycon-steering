# JoyconSteering

Use a single (left) Nintendo Switch Joy-Con as a steering wheel on Windows. The
Joy-Con's gyro angle drives a virtual wheel axis via the vJoy driver, so
Forza Horizon and other PC racing games see "an axis position" rather than
"a stick velocity." Throttle and brake come from the Joy-Con's analog stick
(or its L/ZL buttons). Buttons are remappable through `App.ini`.

The point of building this ourselves — instead of using BetterJoy's
gyro-to-stick — is **position-based steering**: tilt the controller 45°,
the wheel sits at 45°. BetterJoy is velocity-based, which feels wrong in
racing games.

---

## Where things live

```
joycon-steering/
├── JoyconSteering.sln              Solution
├── Directory.Build.props           Warnings-as-errors + analyzers for all projects
├── .editorconfig                   Formatting rules
├── CLAUDE.md                       This file
│
├── JoyconSteering/                 Main app — WinForms tray app
│   ├── JoyconSteering.csproj
│   ├── App.ini                     Default runtime config (copied to output)
│   ├── app.manifest                DPI awareness + Win10/11 compat
│   ├── Program.cs                  Entry point (Application.Run + TrayAppContext)
│   ├── SteeringWorker.cs           Owns runtime pipeline; Start/Stop/Recenter
│   ├── PedalJoyConWorker.cs        Optional second-Joy-Con worker for throttle/brake
│   │
│   ├── Config/
│   │   ├── IniReader.cs            Section-based INI parser
│   │   ├── IniWriter.cs            Comment-preserving INI updater
│   │   └── AppConfig.cs            Strongly-typed config record + defaults
│   │
│   ├── JoyCon/
│   │   ├── JoyConDevice.cs         HID I/O (HARDWARE-TOUCHING)
│   │   ├── InputReportParser.cs    Pure: bytes → JoyConState / RightJoyConState
│   │   ├── JoyConState.cs          Record struct (left): buttons + stick + 3 IMU samples
│   │   ├── RightJoyConState.cs     Record struct (right): same shape, right buttons
│   │   ├── ImuSample.cs            Record struct: accel (g) + gyro (deg/s)
│   │   ├── JoyConButtons.cs        Left button flag enum + name lookup
│   │   ├── RightJoyConButtons.cs   Right button flag enum + name lookup
│   │   ├── JoyConBattery.cs        Nibble decode → percent + charging
│   │   └── Fusion/
│   │       ├── MadgwickFilter.cs   Quaternion AHRS, IMU-only + ZUPT
│   │       ├── GyroBiasCalibrator.cs Startup bias cal + running refinement
│   │       ├── GravityTiltAxis.cs  Body-frame tilt from gravity vector
│   │       └── WheelAxisIntegrator.cs Body-frame gyro Z integration
│   │
│   ├── Steering/
│   │   ├── SteeringMath.cs         Pure: angle → axis (-1..+1) with calibration
│   │   ├── TiltPedalMath.cs        Pure: signed tilt → (throttle, brake)
│   │   └── AngleSource.cs          Axis picker + rising-edge detector
│   │
│   ├── Output/
│   │   ├── IWheelOutput.cs         Sink interface (steering / pedals / buttons)
│   │   ├── WheelOutputMapper.cs    Pure: JoyConState + steering → IWheelOutput calls
│   │   └── VJoyOutput.cs           vJoy P/Invoke (HARDWARE-TOUCHING)
│   │
│   └── Ui/
│       ├── TrayAppContext.cs       NotifyIcon + context menu + worker ownership
│       ├── DiagnosticsForm.cs      Live values window (50 ms timer poll)
│       └── SettingsForm.cs         Config editor → IniWriter + reload
│
└── JoyconSteering.Tests/           xUnit test suite
    ├── IniReaderTests.cs
    ├── IniWriterTests.cs
    ├── AppConfigTests.cs
    ├── InputReportParserTests.cs
    ├── MadgwickFilterTests.cs
    ├── SteeringMathTests.cs
    ├── AngleSourceTests.cs
    ├── WheelOutputMapperTests.cs
    ├── PipelineIntegrationTests.cs Cross-stage end-to-end tests
    └── TestHelpers/
        └── ReportBuilder.cs        Synthesises 0x30 HID reports for parser tests
```

## Optional second Joy-Con (pedals)

`PedalJoyConWorker` is an independent worker — same shape as `SteeringWorker`
(open device, init, read loop, auto-reconnect) — that opens the OPPOSITE
Joy-Con (`PedalsConfigHelper.PedalSideFor(steeringSide)`) and computes
`(throttle, brake)` from either assignable buttons or a gravity-anchored
tilt. It runs on its own task; the steering worker reads its `CurrentPedals`
each tick and passes them to `WheelOutputMapper.Apply` as an
`externalPedals` override.

This worker is **only spawned when the configured throttle/brake mode
requires it** (`pedal_buttons` or `pedal_tilt`), and gracefully handles
the second Joy-Con being unpaired — it just sits in its reconnect loop and
yields zero throttle/brake until the device shows up.

The two workers write to *different* vJoy axes (steering writes X + buttons;
pedals writes Y + Rz when external), so there's no contention.

## Architectural boundaries

Two classes touch hardware. The UI is a thin shell over the workers.
Everything else is pure and unit-tested:

| Layer                                | Touches hardware?       | Unit tested? |
| ------------------------------------ | ----------------------- | ------------ |
| `JoyConDevice.Read`                  | Yes (HID over BT)       | No           |
| `InputReportParser.*`                | No (pure bytes → state) | Yes          |
| `MadgwickFilter.*`                   | No                      | Yes          |
| `SteeringMath.*`                     | No                      | Yes          |
| `AngleSource`, `RisingEdgeDetector`  | No                      | Yes          |
| `WheelOutputMapper.Apply`            | No                      | Yes          |
| `IniReader`, `IniWriter`, `AppConfig`| No                      | Yes          |
| `VJoyOutput.*`                       | Yes (kernel driver)     | No           |
| `SteeringWorker.*`                   | Owns hardware shims     | No (manual)  |
| `Ui/*` (TrayApp, Diagnostics, Settings) | Yes (WinForms)       | No (manual)  |

When adding behaviour, push logic into the pure layer where you can write
a test for it. The hardware shims should stay tiny.

## How the runtime pipeline composes

```
TrayAppContext (UI thread)
  └── SteeringWorker (background Task) ────────────────────────────────────┐
        │                                                                  │
        ▼                                                                  │
        HID packet (64 bytes, ~15ms cadence)                               │
          └── InputReportParser.ParseStandard                              │
               └── JoyConState { buttons, stickX, stickY, 3× ImuSample }   │
                    ├── Per IMU sample: MadgwickFilter.Update(sample, 5ms) │
                    │    └── GetEulerDegrees() → (roll, pitch, yaw)        │
                    │         └── AngleSource.Pick(axis, …) → angle°       │
                    │              ├── recenter edge → SteeringMath.Recenter
                    │              └── SteeringMath.Compute(angle, dtMs)   │
                    │                   └── WheelOutputMapper.Apply(...)   │
                    │                        └── VJoyOutput → vJoy → game  │
        Worker publishes WorkerStatus (locked) ─────────────────────────────┘
                                                                            │
DiagnosticsForm.Refresh() reads Status via 50ms WinForms.Timer ◄────────────┘
SettingsForm.Save → IniWriter.Update → AppConfig.Load → Worker.Stop+Start
```

---

## Build & run

Prereqs:

- **.NET 8 SDK** (or newer — the project targets `net8.0-windows`).
- **vJoy driver** installed from <https://sourceforge.net/projects/vjoystick/>.
  Configure device 1 with axes X, Y, Rz and ≥ 16 buttons in **vJoy Conf**.
- **Joy-Con paired** via Windows Bluetooth.

```powershell
cd D:\dev\joycon-steering

# Build everything (warnings are errors).
dotnet build

# F5 from Visual Studio, or:
dotnet run --project JoyconSteering
dotnet run --project JoyconSteering -- C:\path\to\custom.ini

# Self-contained single-file exe (no runtime install needed on target).
dotnet publish JoyconSteering -c Release -r win-x64 `
  --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
# Output: JoyconSteering\bin\Release\net8.0-windows\win-x64\publish\JoyconSteering.exe (~155 MB)

# Smaller framework-dependent exe (requires .NET 8 desktop runtime on target).
dotnet publish JoyconSteering -c Release -r win-x64 --self-contained false
```

App lives in the system tray — no console window. Hover the tray icon for
live status, double-click for Diagnostics, right-click for the full menu
including Settings and Recenter.

---

## Validation framework

We have four layers of validation, three automatic and one manual.

### 1. Static — `Directory.Build.props`

`TreatWarningsAsErrors = true`. The .NET analyzers run on every build.
A build that compiles is a build with zero warnings. If a build "succeeds"
but you see yellow squiggles, treat them as failing.

### 2. Unit tests — per pure module

Each pure-logic file in the main project has a sibling `*Tests.cs` file in
`JoyconSteering.Tests`. Conventions:

- One xUnit `[Fact]` per behaviour; `[Theory]` with `[InlineData]` for
  parameter sweeps.
- No mocks. Pure functions are tested with literal inputs and expected
  outputs. The one collaborator that gets faked is `IWheelOutput`
  (`FakeWheelOutput` in `WheelOutputMapperTests.cs`).
- Test names read `Subject_Condition_ExpectedResult` (e.g.
  `Deadzone_OutputResumesPastDeadband`).

### 3. Pipeline integration tests — `PipelineIntegrationTests.cs`

Cross-stage tests that wire parser → fusion → steering → mapper → fake sink.
These exist to catch regressions at the *seams* between stages — a unit test
on `SteeringMath` won't catch you accidentally feeding it pitch instead of
roll, but a pipeline test will.

If you change a public API on any pipeline stage, run these *first* to see
what breaks before chasing unit-test failures.

### 4. Manual hardware check — run the tray app

The HID layer (`JoyConDevice`), vJoy layer (`VJoyOutput`), and UI
(`Ui/*`) are not unit-testable. To validate them you must run against a
real Joy-Con and a configured vJoy device.

**Smoke procedure:**

1. Pair the left Joy-Con via Bluetooth (hold the small button between SR
   and SL until the lights race; pair from Windows Settings).
2. Open vJoy Conf, ensure device 1 has X, Y, Rz axes and ≥16 buttons.
3. `dotnet run --project JoyconSteering` (or double-click the published
   exe). The blue "JS" tray icon should appear.
4. Hover the tray icon — tooltip should show `JoyconSteering ↵ angle … °
   steer … ↵ bat …`. If it shows "ERROR", read the message (most likely
   vJoy not configured or Joy-Con not paired).
5. Double-click the tray icon to open Diagnostics. Hold the Joy-Con
   neutral, right-click tray → Recenter. The angle should reset to ≈0°.
6. Rotate the Joy-Con like a wheel. `angle` should change, `steer` should
   move proportionally and stay where you point it (no auto-return).
7. Open `joy.cpl` (Set up USB game controllers) → vJoy Device →
   Properties. The X axis indicator should track your rotation live.
8. Right-click tray → Settings → change `Range (degrees)` to 270 →
   Save & Apply. Worker hot-restarts; same rotation now requires more
   physical movement.

If 3-4 fail, the failure is in the hardware shims. Check `joy.cpl` for
vJoy device state and `Settings → Bluetooth` for the Joy-Con connection
status.

---

## Running tests

```powershell
# All tests, one shot.
dotnet test

# Filter by class.
dotnet test --filter "FullyQualifiedName~PipelineIntegrationTests"

# Verbose, including names of passing tests.
dotnet test -v normal
```

Expected output: `Passed!  - Failed: 0, Passed: 92` (or higher as tests get
added).

---

## TDD discipline

The user asked for TDD. The expectation going forward:

1. Before changing the behaviour of a pure module, write the failing test
   first.
2. For new pure modules, write the tests in the same commit as the
   implementation (or before).
3. Hardware shims are exempt — there's no way to test them in isolation.
   Keep them as thin as possible so the untested surface stays small.

If you find yourself wanting to test something that lives inside
`JoyConDevice` or `VJoyOutput`, that's a sign the logic should be extracted
into the pure layer. `InputReportParser` was extracted out of
`JoyConDevice` for exactly this reason.

---

## Hardware reference: Joy-Con HID

- VID `0x057E`, PIDs: left `0x2006`, right `0x2007`.
- Input report `0x30` (standard full IMU) is the only one we consume.
  Layout per <https://github.com/dekuNukem/Nintendo_Switch_Reverse_Engineering>:
  - byte 2 (upper nibble) = battery level
  - bytes 3-5 = button state (we use 4 and 5 for left-side buttons + shared)
  - bytes 6-8 = left stick (12-bit X / 12-bit Y packed)
  - bytes 9-11 = right stick
  - bytes 13-48 = 3 × 12-byte IMU samples (accel int16 ×3, gyro int16 ×3)
- IMU sample interval: 5 ms. Three samples per HID frame → ~200 Hz IMU.
- Accel scale: 4096 LSB ≈ 1 g.
- Gyro scale: 0.06103 dps/LSB (FSR ±2000 dps).

To init: subcommand `0x40` (enable IMU = 1), then `0x03` (set report mode
`0x30`). Optional `0x30` to set the player LED. See `JoyConDevice.Initialize`.

---

## Known limits / non-goals

- **No force feedback.** The Joy-Con has rumble (HD rumble), but no
  steering counter-torque. Out of scope.
- **vJoy device class.** Forza Horizon 5/6 recognise vJoy as a generic
  controller, not a wheel. Steering still works correctly via the axis
  binding; the wheel-specific UI tab (per-wheel sensitivity sliders, FFB
  strength) won't appear. Spoofing as a Logitech G29 would require
  recompiling vJoy's kernel driver with patched VID/PID and Windows
  test-signing mode — see the conversation history.
- **Yaw drift.** Madgwick IMU-only (no magnetometer) cannot correct yaw.
  The default steering axis (`auto` → roll) is anchored to gravity, so it
  doesn't drift. If you reconfigure to `yaw`, expect to recenter often.
- **Right Joy-Con.** The HID open path exists (`OpenRight`), but the
  button-bit layout in `InputReportParser.ParseLeftButtons` is left-Joy-Con
  specific. A right-Joy-Con parser would need its own bit map (different
  buttons in byte 3 vs byte 5).
- **Calibration.** We use nominal stick centre/range, not factory calibration
  read from the Joy-Con's SPI. Sticks may have small dead-band asymmetry.
  Fixable later; not blocking.

---

## Quick troubleshooting

| Symptom                                    | Likely cause                                            |
| ------------------------------------------ | ------------------------------------------------------- |
| "vJoy driver is not enabled"               | vJoy not installed, or device 1 doesn't exist in vJoyConf |
| "vJoy device 1 is not available (status 4)"| Device not configured in vJoyConf                       |
| "No Joy-Con found"                         | Joy-Con not paired in Windows Bluetooth, or off         |
| Status line shows angle changing but `steer=0.00` | Recenter hasn't been pressed; press stick once   |
| `steer` jitters when stationary            | Increase `[fusion] madgwick_beta` (try 0.08-0.10)       |
| `steer` drifts slowly when held still      | Steering axis is yaw (drifts) — switch to `roll`        |
| Steering feels too sensitive               | Increase `[steering] range_degrees` (try 270 or 360)    |
