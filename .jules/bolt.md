## 2024-05-30 - [Kernel String Reading Optimization]
**Context:** src/SharpEmu.Libs/Kernel/KernelMemoryCompatExports.cs
**Learning:** Guest string reading operations (TryCompareStrings, Strchr, Strrchr, Memchr, TryCompareStringsCaseInsensitive) read character by character in loops, making a new TryReadCompat call (which grabs locks and searches trees in VirtualMemory) for every single byte. This creates huge overhead for long strings, which could be reduced with chunked reads or reading the whole thing using the existing CString functions.
**Action:** Use TryReadCString or chunked reads instead of 1-byte read loops.
