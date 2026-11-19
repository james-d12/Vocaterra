# B48 report (the crash during play when the last monitor goes away)

Branch `B48-sdl-display-loss`, head `85b93bb3` (fix `5168d7ae`, feature/world-next merged). Agent's gate: build 0 warnings; full 12,733
passed, 0 failed, 9 skipped (12 new); format clean; validate; no package changed. **The monitor was asleep the whole session** (`xrandr`:
Screen 0 current 0 x 0, no outputs; XTEST input did not reach the game), so no live monitor transition, full-screen, F11 or typing check
was possible.

Options: (a) a newer SDL2: rejected, 2.32.10 is the last SDL2 release and the SDL2 branch tip has the same unchecked
`SDL_GetDisplayForWindow(...)->driverdata` reads (SDL_ibus.c, SDL_fcitx.c, SDL_x11window.c); MonoGame.Library.SDL 2.32.10.3 is the same SDL.
(b) native Wayland (`SDL_VIDEODRIVER=wayland`): rejected, `NoSuitableGraphicsDeviceException` (glGetString null; unclear whether GL on
Wayland or the missing outputs), and SDL's Wayland driver also deletes a display when an output goes. (c) avoiding IBus: rejected, fcitx has
the same bug, the owner runs ibus-typing-booster (its preedit messages call `SDL_IBus_UpdateTextRect`), and it would not cover
`X11_HideWindow` or the full-screen setters. (d) **RandR off: chosen.** The only X11 code that deletes a display is the RandR
output-change handler; with `SDL_VIDEO_X11_XRANDR=0` SDL uses the one X-screen display for the whole run. Costs: no second monitor or mode
lists in SDL; the game reads display 0's size once at start and full screen is borderless at desktop mode; on a multi-monitor desktop the
start size is the whole X screen. Applied to scripted runs (B46) and Wayland sessions; a plain X11 session keeps RandR; Windows and macOS
untouched (Linux-only code).

Implementation: `SdlDisplays.ShouldIgnoreRandR(scripted, sessionType, waylandDisplay)` and `IgnoreRandRChangesIfNeeded`
(`XDG_SESSION_TYPE` wayland or x11 decides, else a set `WAYLAND_DISPLAY` means Wayland); an interactive Wayland run prints `display: SDL does
not follow the X server's outputs (SDL_VIDEO_X11_XRANDR=0; a Wayland session, B48)`; an existing value wins, so a player can set
`SDL_VIDEO_X11_XRANDR=1`. B46's exit guard unchanged.

gdb: before, `SDL_DelVideoDisplay(0)` then a stand-in preedit call died in `SDL_IBus_UpdateTextRect` (the dumps' frame). After: the forced
delete bypasses the fix (it prevents the loss instead), so no survival proof; checked that the new build's display is `Generic X11 Display`,
`XRRSelectInput` is never called, and a 30-frame run exits 0. Tests: `SdlDisplaysTests` +12 (an 11-case decision table; the native
environment holds `0` before SDL starts, X11 leaves it unset, the player's `1` wins).

**For the owner to check with the monitor awake:** full screen starts at the right size (the X screen size could differ under fractional
scaling); F11 toggles; typing `ñ` in the F8 report works. Left: a plain X11 session still follows RandR (unplugging its last output could
still crash; rare); starting the game with the monitor asleep gives a 0x0 desktop size, as before.

For running.md: "In a Wayland session the game keeps SDL off RandR (it sets `SDL_VIDEO_X11_XRANDR=0` before SDL starts, and prints a
`display:` line saying so), so SDL's display cannot vanish when the monitor sleeps (B48). SDL then sees one display, the whole X screen,
and no mode lists; full screen, F11 and typing are unchanged. Set `SDL_VIDEO_X11_XRANDR=1` to turn this off."
