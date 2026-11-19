# B46 report (intermittent exit 139 of game processes)

Branch `B46-exit-139`, head `17029800` (fix `55618252`, then feature/world-next at eccc71a8 merged). Agent's gate: build 0 warnings; full
12,721 passed, 0 failed, 9 skipped; format clean; validate.

Stacks: all 8 game core dumps crash in MonoGame's bundled SDL 2.32.10 (`libSDL2-2.0.so.0`) on the game thread, in two places:
- event pump, mid-run (2645884 and 2645885, today's two shots; 565926, 2026-11-17): `SDL_IBus_UpdateTextRect <- IBus_MessageHandler <-
  dbus_connection_dispatch <- SDL_IBus_PumpEvents <- X11_PumpEvents <- SDL_PollEvent`;
- shutdown after the screenshot (2824460, 2824019, 2780268 on 2026-11-16; 477467 on 2026-11-12; 886229 on 2026-11-16): `X11_HideWindow <-
  SDL_HideWindow <- SDL_DestroyWindow` from `Game.Dispose` at the end of `Main`.
The SIGABRTs 2106304 and 2335788 are not the game (WorldPreview and a scratch tool).

Cause: both frames read `->driverdata` from `SDL_GetDisplayForWindow`, which returns NULL when SDL has no display left: SDL 2.32 follows the
X server's RandR outputs and drops a display when its output goes away (GNOME on Wayland; Xwayland reported 0 outputs). The crashes line
up with monitor sleep and wake (`nvidia-modeset` HDMI warnings 1 to 3 minutes before the 11-16 08:52, 11-17 08:55 and 11-19 08:58 dumps;
the 11-16 22:16 and 22:19 dumps have no such warning). B07e's "far buffers at shutdown" was not the cause.

Reproduction: a real monitor transition cannot be made on demand (40 pairs of the test's command on the old binary: 0 failures). Forced
under gdb by calling SDL's `SDL_DelVideoDisplay(0)` after about 300 frames: the old binary SIGSEGVs in `X11_HideWindow` (the dumps' stack);
the new one exits 0 with the png. `SDL_VIDEO_X11_XRANDR=0` removes the RandR subscription (a break on `XRRSelectInput` hit twice by the old
game, never by the new in a screenshot run).

Fix (`src/Vocaterra.Game/Startup/SdlDisplays.cs`, called from `Program.cs`): a screenshot run (what the tests use) sets
`SDL_VIDEO_X11_XRANDR=0` through libc `setenv` (not overwriting) before SDL starts, so the display cannot vanish; any run, after
`game.Run()` returns, checks `SDL_GetNumVideoDisplays() < 1` and if so flushes and `Environment.Exit(game.ExitCode)` without destroying
the window (unload, save and logs have already run). Test: `SdlDisplaysTests` (2): the variable reaches the native environment SDL reads;
an existing value is not overwritten.

Left: a player's interactive window keeps SDL's RandR handling (needed for multi-monitor and mode lists), so if the last monitor goes away
during play SDL can still crash in the event pump (an SDL 2.32 bug). Possible later fixes: a newer bundled SDL, or `SDL_IM_MODULE` /
`XMODIFIERS` to avoid the IBus path (not tried). For findings: B07e's named cause was wrong (dump 2824460 is `SDL_DestroyWindow ->
X11_HideWindow` with a NULL display).
