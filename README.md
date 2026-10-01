# Lumia Shooter

A very simple top-down space shooter for **Windows 10 Mobile** (made for a Nokia Lumia 735:
ARM, 1 GB RAM, 720x1280, Windows 10 Mobile 1607 / build 14393). It also runs on any
Windows 10 PC for testing. White shapes on black, no external assets, no extra libraries.

## Project facts

| Item | Value |
|---|---|
| Solution | `LumiaShooter.sln` → `LumiaShooter\LumiaShooter.csproj` (classic UWP C# project) |
| Target version (SDK used to compile) | **10.0.15063.0** (Windows 10 Creators Update SDK) |
| Minimum version (oldest Windows that can run it) | **10.0.10586.0** → installs on build 14393 |
| NuGet | `Microsoft.NETCore.UniversalWindowsPlatform` **5.4.7** (.NET Native 1.7) — the only package. **Do not update it to 6.x**: 6.x needs build 16299, which the Lumia 735 can never get. |
| Configurations | Debug / Release × ARM / x86 / x64. Release uses .NET Native. |
| Signing | `LumiaShooter_TemporaryKey.pfx` — self-signed test certificate, subject `CN=LumiaShooter` (same as the manifest Publisher), Code Signing EKU, **no password**, valid until 2036. |
| Language | C# (only C# 6 features; builds with VS2017's C# 7.0 compiler) + XAML |

## Requirements

* A Windows 10 PC.
* **Visual Studio 2017** (15.9 recommended; Community is fine) with the
  **"Universal Windows Platform development"** workload. VS2017 is the safest choice for
  Windows 10 Mobile. VS2019 can also build this and deploy to ARM phones, but has less phone tooling.
* **Windows 10 SDK 10.0.15063.0**: in the Visual Studio Installer, open *Individual components*
  and tick *Windows 10 SDK (10.0.15063.0)*. If you only have a different SDK (for example 14393 or
  16299), open *Project → Properties → Application* and set **Target version** to the SDK you
  have. Leave **Min version** at 10586 (build 10586).
* For deploying over USB: the *USB Device Connectivity* optional component of the UWP workload
  (adds the "IP over USB" service). Use a USB cable that carries data, not just power.

## CI build (GitHub Actions) - no Windows PC needed

Every push (and *Actions → Build ARM sideload package → Run workflow*) builds a signed
**Release | ARM** sideload package on a GitHub-hosted `windows-2022` runner
(see `.github/workflows/build.yml`):

* Visual Studio 2022 MSBuild with the runner's UWP workload and .NET Native. The .NET Native 1.7
  compiler comes from the NuGet package.
* The runner has no Windows SDK 15063, so CI compiles against **10.0.17763.0**
  (`/p:TargetPlatformVersion=10.0.17763.0`). **Min version stays 10.0.10586.0** and the NuGet package
  stays 5.4.7, so the package still installs on Windows 10 Mobile 1607 (14393).
* The package is signed with the included `LumiaShooter_TemporaryKey.pfx` (`CN=LumiaShooter`).
* The artifact **LumiaShooter-ARM-sideload** contains `LumiaShooter_1.0.0.0_ARM.appx`, its `.cer`, and
  `Dependencies/ARM/` (`Microsoft.VCLibs.ARM.14.00.appx`, `Microsoft.NET.Native.Runtime.1.7.appx`,
  `Microsoft.NET.Native.Framework.1.7.appx`).
* A ready-to-install bundle for the phone (with Lithuanian install instructions) is published under
  **Releases** (`LumiaShooter-Lumia735.zip`, plus the same files one by one, so the phone can
  download them directly).

## Open and build

1. Unzip, double-click `LumiaShooter.sln`.
2. NuGet restores the UWP package automatically on the first build (needs internet).
3. In the toolbar choose the configuration and platform:
   * phone: **Release | ARM** (or Debug | ARM while developing)
   * PC test: **Debug | x86** or **Debug | x64**
4. *Build → Build Solution*. The first **Release** build runs the .NET Native compiler and can take a few minutes.

## Run on the Lumia (USB, simplest)

1. On the phone: **Settings → Update & security → For developers → Developer mode**.
   Turn on **Device discovery** on the same page too (you need it for pairing/WinAppDeployCmd, and it doesn't hurt).
2. Plug the phone into the PC over USB and **unlock the screen** (keep it unlocked during deploy).
3. In Visual Studio pick **Release | ARM**. In the run target drop-down (next to the green ▶)
   choose **Device** (not "Remote Machine", not an emulator).
4. Press **Ctrl+F5** to deploy and start without the debugger (or **F5** to debug).
   The app then stays on the phone ("Lumia Shooter" in the app list) and works without the PC.

## Alternative: build an app package and install it by hand

1. *Project → Store → Create App Packages…* (in VS2019: *Project → Publish → Create App Packages…*).
2. Choose **sideloading** ("No" to the Store question), version 1.0.0.0, architecture **ARM**,
   configuration **Release (ARM)**. Generating an app bundle is fine.
3. The output folder `AppPackages\LumiaShooter_1.0.0.0_Test\` contains the `.appxbundle`, the
   certificate (`.cer`), and a `Dependencies\ARM\` folder (`Microsoft.NET.Native.Framework.1.7`,
   `Microsoft.NET.Native.Runtime.1.7`, `Microsoft.VCLibs…14.00`). Install the dependencies too.

**Option A – WinAppDeployCmd** (comes with the Windows 10 SDK, in
`C:\Program Files (x86)\Windows Kits\10\bin\<version>\x86\`):

```
WinAppDeployCmd devices
WinAppDeployCmd install -file LumiaShooter_1.0.0.0_ARM.appxbundle -ip <phone-IP> -pin <PIN> ^
    -dependency Dependencies\ARM\Microsoft.NET.Native.Framework.1.7.appx ^
                Dependencies\ARM\Microsoft.NET.Native.Runtime.1.7.appx ^
                Dependencies\ARM\Microsoft.VCLibs.ARM.14.00.appx
```

Over Wi-Fi, use the phone's IP and get the PIN from *For developers → Device discovery → Pair*.
Over USB the device usually shows up as `127.0.0.1`. The exact dependency file names may be
slightly different; use the ones in your `Dependencies\ARM` folder.

**Option B – Device Portal**: on the phone, turn on *For developers → Device Portal*, then open the
address it shows in a PC browser (over USB this is usually `http://127.0.0.1:10080`). Go to
**Apps → Install app**, pick the `.appxbundle`, add each dependency `.appx`, and click *Go*.

**About the certificate.** The package is signed with the included test certificate
(`CN=LumiaShooter`). A phone in Developer mode accepts packages installed through Visual Studio,
WinAppDeployCmd or Device Portal. If Device Portal asks for a certificate, give it the `.cer`
from the package folder. To sideload the package on a **PC** without Visual Studio, first install
the `.cer` into *Local Machine → Trusted People*, or run the `Add-AppDevPackage.ps1` that the wizard
creates.

If Visual Studio ever rejects the included certificate (for example a signing error when creating
packages), make a new one: open `Package.appxmanifest` → **Packaging** tab → **Choose Certificate…**
→ **Create test certificate…** (publisher `CN=LumiaShooter`, no password). VS2019 shows the same
option in the *Create App Packages* wizard. Building and running with F5/Ctrl+F5 doesn't need the
certificate at all.

## Test on a PC

Pick **Debug | x86** (or x64) and target **Local Machine**, then press F5. The window opens
phone-shaped; any window size works because the game area scales and gets black bars.

* **Mouse**: the ship follows the mouse pointer (no click needed, no finger offset). Click = tap.
* **Esc** = Back button (pause → main menu), **P** = pause/continue, **Enter/Space** = tap.
* The title-bar back arrow (shown during a game) works like the phone Back button.

## Controls (phone)

* **Drag a finger anywhere** on the screen. The ship glides to a spot about 1 cm **above** your
  finger, so your finger doesn't cover it.
* The ship **shoots by itself**.
* **Tap** to start, to continue after a pause, and to play again (GAME OVER/YOU WIN ignore taps for
  the first second, so frantic tapping doesn't skip the score).
* **Back button**: during a game → pause. While paused / on GAME OVER / YOU WIN → main menu.
  On the main menu → leaves the app (normal phone behaviour).
* The game also pauses by itself when the app is hidden, suspended or loses focus (a call,
  the notification centre, the Start button). The screen stays on while you play.

## Gameplay

* **Screen**: progress bar at the top (how far through the level you are), score (left),
  `LEVEL n/5` (middle), lives (right), active power-ups with seconds left (below the score).
* **5 lives**, up to 9. After you're hit you **blink for 2.5 s** and can't be hit again. Enemy bullets also
  disappear when you are hit. Enemies that fly past the bottom don't cost anything.
* **Five levels**. After each one you see "LEVEL n CLEAR!" and get a bonus (level × 100), then "LEVEL n+1".
  1. Slow square enemies only. Nobody shoots.
  2. Faster, plus wiggling **diamond** enemies and small V groups.
  3. **Arrow** enemies that shoot slow bullets at you.
  4. Big hollow **"tank"** squares that need 5 hits.
  5. Everything, faster. At the end a **BOSS** appears (health bar at the top, 100 hits, shoots fans
     of bullets with gaps). You can't fly into the boss, so touching it can't cost you lives. Beat it → **YOU WIN!**
* **Power-ups** (white boxes with a letter, easy to grab: just get close):
  * `2` double shot (12 s) · `3` triple shot (12 s) · `S` shield (10 s, you can't be hurt and you
    smash into enemies; it blinks when it's about to run out) · `+` extra life · `$` +100 points.
  * Destroyed enemies sometimes drop them, and one more floats down every 9–12 s.
* **Score**: square 10, diamond 20, arrow 30, tank 50, boss 1000. The **high score** is saved
  on the device (`ApplicationData.Current.LocalSettings`).
* A full game takes about 5 minutes.

## How to tweak difficulty

Every setting is in **`LumiaShooter\Game\LevelConfig.cs`**, with comments:

* `LevelConfig.Levels[0..4]`: one entry per level. `Duration` (seconds), `SpawnInterval`
  (smaller = more enemies), `EnemySpeed`, `Weight…` (which enemy types appear; 0 = none),
  `FormationChance`, `EnemyFireInterval` (0 = no shooting), `EnemyBulletSpeed`, `DropChance`,
  `PowerUpInterval`, `HasBoss`.
* `GameTuning`: `StartLives`, `MaxLives`, fire rate, `TouchOffsetY` (how far above the finger the
  ship sits), `InvulnerableTime`, power-up durations, `PickupRadius`, `BossHealth`, and more.
* Enemy hit points and scores: `GameEngine.SpawnEnemy`. Power-up mix: `GameEngine.PickPowerUpType`.

Rebuild and deploy again after changing anything.

## Code layout

```
LumiaShooter.sln
LumiaShooter/
  App.xaml(.cs)            start-up, portrait lock, phone-shaped window on PC
  MainPage.xaml(.cs)       game loop (CompositionTarget.Rendering), input, Back button,
                           pause on suspend/hide, HUD and messages
  Game/                    pure C# game logic (no UWP code)
    GameEngine.cs          states, movement, spawning, collisions, power-ups, score
    Entities.cs            Player, Enemy, Bullet, PowerUp, Particle, Star
    LevelConfig.cs         the 5 levels + global tuning   <-- tweak here
  Rendering/GameRenderer.cs  pooled XAML shapes (no per-frame allocations)
  Services/HighScoreStore.cs LocalSettings high score
  Services/PlatformHelper.cs phone full screen (hides status bar), PC window size
  Assets/                  generated logo/splash PNGs
  Package.appxmanifest     portrait only, MinVersion 10.0.10586.0
  LumiaShooter_TemporaryKey.pfx  test signing certificate (CN=LumiaShooter, no password)
```

The status bar is hidden with `ApplicationView.TryEnterFullScreenMode()` (phone only), so the
project needs **no** reference to the "Windows Mobile Extensions for the UWP" SDK. Every Windows API
the game uses was already in build 10240.
