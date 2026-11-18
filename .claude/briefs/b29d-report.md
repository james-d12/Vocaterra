# B29d report (agent, 2026-11-18, about 16:05 UTC; its first hand-back at 15:40 was premature)

Branch `B29d-cpu-timing`, head `3fe7d30c`, one commit on `9f2f3a31`; three test classes.
- ChunkIndexTests: digest after one edit, mean of 200 edits, fastest of 3: budget 0.6 ms (was 100 ms wall; measured 0.215 to 0.283).
- RegionTimeTests: beside the valley 70 ms (was 3,000 wall; 26 to 34), at the corner 100 ms (was 3,000; 32 to 50); reported figures in CPU time.
- ProceduralTimingTests: 256 × 256 area 50 ms (was 3,000; 20 to 25); one chunk column 0.4 ms over 1,000 repeats (was 100; 0.13 to 0.19).
- ProceduralChunkTimeTests unchanged (asserts already CpuTime; wall medians report-only).
Budgets twice the highest measured run. `test.sh timing` 3 of 3 (23); full 12,631; Release build 0 warnings; format clean;
WallClockRulesTests passes. Left: WaterAtRestTests reads a Stopwatch only to report (allow-listed).
