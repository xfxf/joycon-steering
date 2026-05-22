# JoyconSteering

> ⚠️ **Vibe-coded.** This was hacked together end-to-end with an AI coding
> assistant rather than carefully designed up front. The goal was narrow and
> specific: get a single Nintendo Switch left Joy-Con working as a steering
> wheel for **Forza Horizon 5 and 6** on PC — including the Xbox Game Pass /
> Microsoft Store builds that don't accept third-party controller drivers
> through Steam Input. It works for that, and the architecture is reasonable,
> but treat it accordingly.

Use a single left Nintendo Switch Joy-Con as a **steering wheel** for PC racing
games — particularly ones that don't natively support Joy-Cons, like the
Xbox Game Pass / Microsoft Store version of **Forza Horizon 5/6**.

Tilt the Joy-Con left or right; the game sees a proper wheel axis. Unlike
BetterJoy's gyro-to-stick mode, this is **position-based** — hold the
controller at 45° and the wheel stays at 45°, instead of springing back to
centre.

## Why this exists

Existing free tools (BetterJoy, JoyShockMapper) can route Joy-Con gyro to a
joystick axis, but they map angular *velocity* — turn the Joy-Con and the
stick moves; stop turning and the stick centres again. That feels wrong in a
driving game. This tool integrates the gyro into an *angle* and outputs that
angle as a wheel axis, which is what a real wheel does.

## Status

Working build with full test coverage of the non-hardware code (parser,
sensor fusion, steering math, output routing). The hardware path (Bluetooth
HID → vJoy driver) is in place but needs a real Joy-Con to validate.

## Screenshots

The live diagnostics window (auto-opens on launch; double-click the tray
icon to re-show after minimising):

![Diagnostics window](docs/screenshots/diagnostics.png)

The Settings dialog — pick **Wheel** (gyro-integrated, position-based) or
**Tilt** (gravity-anchored, "Mario Kart" style, drift-free), set your
full-lock tilt angle, button map, etc. Save & Apply hot-reloads the
running pipeline:

![Settings dialog](docs/screenshots/settings.png)

## Quick install (prebuilt)

1. Grab the latest **`JoyconSteering.exe`** from the
   [Releases page](https://github.com/xfxf/joycon-steering/releases). It's a
   self-contained Windows x64 build — no .NET install required.
2. Install **vJoy** from <https://sourceforge.net/projects/vjoystick/>.
   Open **Configure vJoy**, ensure **Device 1** has axes **X**, **Y**, **Rz**
   enabled and **≥ 16 buttons**, click **Apply**.
3. Pair the **left Joy-Con** via Windows Bluetooth: Settings → Bluetooth &
   devices → Add device. On the Joy-Con, hold the small sync button between
   SR and SL until the four LEDs race; wait for "Joy-Con (L)" to appear and
   pair it.
4. Double-click `JoyconSteering.exe`. Look for the blue **JS** icon in the
   system tray; double-click it to open the live diagnostics window.
5. In **Settings → Steering**, pick a mode (Wheel for gyro integration, Tilt
   for gravity-anchored Mario Kart style), set your full-lock tilt angle,
   click **Save & Apply**, then **Recenter** from the tray menu while
   holding the controller straight-ahead.
6. In your game's controller config, bind **vJoy X axis** to steering and
   **Y / Rz** to throttle / brake.

If something's wrong on first launch, the tray icon's tooltip and the
Diagnostics window will tell you which check is failing. You can also run
`JoyconSteering.exe --diagnose` from a command prompt for a one-shot
self-check (vJoy DLL, driver state, device config, Joy-Con presence,
INI parse).

## Requirements

- **Windows 10 or 11** (64-bit).
- **.NET 8 SDK** (build-time) — <https://dotnet.microsoft.com/download>.
  Pre-built releases will ship a self-contained exe; for now you build from
  source.
- **vJoy driver** — <https://sourceforge.net/projects/vjoystick/> (2.1.9.1).
  After install, open **Configure vJoy** from the Start menu:
  - Make sure **Device 1** exists.
  - Enable axes **X**, **Y**, and **Rz**.
  - Set **Number of Buttons** to at least **16**.
  - Click **Apply**.
- **A Joy-Con** paired via Windows Bluetooth (hold the small recessed
  button between SR and SL until the lights race; pair from
  Settings → Bluetooth). Either left or right works as the steering Joy-Con
  — pick one in Settings.
- **(Optional) A second Joy-Con** for throttle/brake. If paired, you can
  set the throttle/brake mode to `pedal_buttons` or `pedal_tilt` and use
  the other Joy-Con (the one opposite your steering side) for pedals.
  Skipping this step is fine — pedals fall back to the steering Joy-Con's
  stick or buttons.

## Run

Double-click **`JoyconSteering.exe`** (under
`JoyconSteering\bin\Release\net8.0-windows\win-x64\publish\` after building,
or wherever you copy it). It runs in the system tray — look for the blue
**JS** icon next to the clock.

- **Hover** the tray icon — tooltip shows live angle, steer, battery.
- **Double-click** the tray icon — opens the Diagnostics window (live
  numeric values + a steering bar).
- **Right-click** the tray icon:
  - **Recenter** — sets the current Joy-Con angle as "straight ahead."
  - **Diagnostics…** — opens the live values window.
  - **Settings…** — opens the configuration UI (no INI editing needed).
  - **Reload config** — re-read `App.ini` from disk and hot-restart.
  - **Quit** — clean shutdown (releases vJoy, closes Joy-Con).

First time:
1. Grip the Joy-Con as you'd hold a wheel (long axis horizontal, buttons
   facing you).
2. Hold it in your "straight ahead" position.
3. Right-click tray → **Recenter** (or press the analog stick — the
   default in-controller recenter button).
4. Rotate the Joy-Con like a wheel. Open `joy.cpl` (Set up USB game
   controllers) → vJoy Device → Properties. The X axis indicator should
   track your rotation.

## Build from source

```powershell
cd D:\dev\joycon-steering
dotnet build                     # debug build for local testing
dotnet test                      # run the suite

# Self-contained single-file exe — no runtime install needed on target machine.
dotnet publish JoyconSteering -c Release -r win-x64 `
  --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
# Output: JoyconSteering\bin\Release\net8.0-windows\win-x64\publish\JoyconSteering.exe
```

## Use it in a game

In Forza Horizon (or your racing game of choice):

1. Plug Joy-Con in / make sure it's connected via Bluetooth, app running.
2. Open the game's controller settings.
3. Bind:
   - **Steering**: vJoy X axis
   - **Accelerator / Throttle**: vJoy Y axis (or button 5 if you set
     `mode = buttons`)
   - **Brake**: vJoy Rz axis (or button 6 in buttons mode)
   - Other buttons as you like — the defaults are listed in `App.ini`.

Forza will recognise the vJoy device as a generic DirectInput controller.
The steering axis will behave position-based (no auto-centring spring).

## Configuration — Settings UI (or `App.ini`)

Easiest way: right-click the tray icon → **Settings…**. The form has tabs
for Steering, Pedals, Buttons, and Advanced; every value in `App.ini` is
editable. **Save & Apply** writes the change back to the INI (preserving
all your comments) and hot-reloads the running pipeline.

You can also edit `App.ini` directly with a text editor — comments explain
every setting. Use **Reload config** in the tray menu to pick up changes
without restarting the exe. Highlights:

- `[steering] range_degrees` — total swing in degrees between full-left and
  full-right lock. Default 180 means tilting 90° each way hits full lock.
  Raise it to 270 or 360 to make steering feel less twitchy.
- `[steering] deadzone_degrees` — physical degrees of slop around centre
  that map to zero steering. Default 1.5°.
- `[steering] smoothing_ms` — exponential smoothing time constant. 0 to
  disable; 8-15 ms feels good.
- `[steering] invert = true` — flip the direction if rotation feels
  backwards.
- `[throttle_brake] mode` — pedal source:
  - `stick` — steering Joy-Con's analog stick Y (up = throttle, down = brake)
  - `buttons` — steering Joy-Con's L = throttle, ZL = brake (digital)
  - `pedal_buttons` — pedal (other) Joy-Con's assignable buttons (digital).
    See `[pedal_buttons]` section to pick which.
  - `pedal_tilt` — pedal Joy-Con's tilt (analog, gravity-anchored, drift-free).
    See `[pedal_tilt]` section for axis/range/deadzone/recenter.
  - `none` — disable; bind throttle/brake to keyboard or another device.
- `[buttons]` — remap each physical Joy-Con button (up, down, left, right,
  L, ZL, minus, stick, sl, sr, capture) to a vJoy button number 1-128. Set
  any to `0` to disable.
- `[recenter] button` — which Joy-Con button presses to re-zero the centre.
  Default `stick` (analog stick click).

Edit, save, restart the app.

## Testing

```powershell
dotnet test
```

92+ unit and integration tests covering INI parsing, config defaults, HID
report parsing, Madgwick filter, steering math, throttle/brake routing,
and full pipeline composition.

## Known limits

- **No force feedback.** The Joy-Con can't push back against you; out of
  scope.
- **Recognised as a controller, not a wheel.** Forza's wheel-specific UI
  tab (per-wheel sensitivity sliders, FFB strength) won't appear. The
  steering itself works correctly via axis binding — this is purely
  cosmetic.
- **Yaw drift.** If you reconfigure `[steering] axis` to `yaw`, expect
  drift over time and frequent recentering. The default (`auto` → roll) is
  gravity-anchored and doesn't drift.
- **One Joy-Con for steering at a time.** Pick left or right in Settings.
  The opposite one becomes the optional pedal Joy-Con.

## Troubleshooting

| Symptom | Fix |
| --- | --- |
| "vJoy driver is not enabled" | Install vJoy, or create device 1 in vJoyConf |
| "vJoy device 1 is not available" | Configure device 1 in vJoyConf (axes + buttons) |
| "No Joy-Con found" | Pair the Joy-Con via Windows Bluetooth |
| `steer` doesn't move | Press the recenter button (default: analog stick) |
| Steering jitters when stationary | Increase `madgwick_beta` to 0.08-0.10 |
| Drifts when held still | Make sure `axis = roll` (default), not `yaw` |
| Direction is reversed | Set `invert = true` in `[steering]` |
| Too sensitive | Increase `range_degrees` to 270 or 360 |

## Project layout & development guide

See **[CLAUDE.md](CLAUDE.md)** for architecture, contribution discipline
(TDD), and the validation framework.

## Licence

GPL
