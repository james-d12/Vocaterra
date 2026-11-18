# B40b report (WorkerPriorityTests flaky)

Branch `B40b-worker-priority-test`, head `25fc0b97` (fix `f1279abc`, then a merge of feature/world-next). Only
tests/Vocaterra.Game.Tests/Streaming/WorkerPriorityTests.cs: `NiceOf` no longer scans /proc/self/task; each test thread records its own
tid (from /proc/thread-self, no P/Invoke) and only those two stat files are read. Assertions unchanged (the raise by
`WorkerPriority.Raise` capped at 19, the other thread unmoved, the returned value equals the read one); the old `Assert.NotNull` on the
plain value is gone because a missing thread now throws. 30 consecutive runs: 30 passed. Fast 12,006 passed; full after the merge 12,682
passed, 0 failed, 9 skipped; build 0 warnings; format clean (once run outside limited.sh, then rerun through it).
