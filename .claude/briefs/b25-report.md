B25 report (agent a103ef200e2d4fb98, Sonnet 5.5 medium, 2026-11-17 about 17:20 UTC): no change. The stall after a scripted `--place`
was `FarView.Stale` counting near-zone tiles that are never rebuilt (from B07c), fixed by B07f's 1049d6d2, merged as 2f995552 before
0d72b26b. Reproduced on B21's tip c7b08ce5 (stalled at `--wait 2`, killed at 500 s); the same line on 0d72b26b ran in 93 s, torch and
guide dialogue shown. B17b's branch lacked the fix too. Re-run on the base: r01-route 445 s, r40-inn 246 s, r42-camp 268 s (torch and glow
shown), the torch cave (torches shown with `--look 349,-45`). Notes: a streamed run's first frame about 160 to 170 s (a cold far view, two
workers); `limited.sh timeout N ...` (timeout inside); a capture needs the build under the repository or `--content`.
