## 2024-05-30 - [Kernel String Reading Optimization]
**Context:** src/SharpEmu.Libs/Kernel/KernelMemoryCompatExports.cs
**Learning:** Guest string reading operations (TryCompareStrings, Strchr, Strrchr, Memchr, TryCompareStringsCaseInsensitive) read character by character in loops, making a new TryReadCompat call (which grabs locks and searches trees in VirtualMemory) for every single byte. This creates huge overhead for long strings, which could be reduced with chunked reads or reading the whole thing using the existing CString functions.
**Action:** Use TryReadCString or chunked reads instead of 1-byte read loops.
## 2026-07-26 - Testing Roslyn Incremental Generator Equality Models

**Context:** When working with C# Source Generators, `IIncrementalGenerator` caching relies heavily on the `Equals` and `GetHashCode` implementations of intermediate data models. If these methods are incomplete or flawed, Roslyn will incorrectly cache or regenerate output, leading to performance issues or stale state. In the context of `SysAbiExportGenerator`, the `ExportModel` is a private, nested type that was largely untested for equality logic.

**Learning:** To guarantee 100% line and branch coverage on these critical internal data models, the most direct and reliable approach is to use reflection within standard xUnit tests. This circumvents the need to construct complex `CSharpCompilation` trees and invoke `CSharpGeneratorDriver` incrementally just to trigger `IEquatable` calls, reducing test brittleness and focusing on structural equality.

**Action:** Created `ExportModelEqualityTests` in `SysAbiExportGeneratorTests.cs` using `GetNestedType` and `Activator.CreateInstance` to thoroughly test all branches of `ExportModel` equality logic, improving code reliability for the generator pipeline.
