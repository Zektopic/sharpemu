## 2024-05-18 - LINQ Allocation Overheads in Hot Paths
**Context:** `VulkanVideoPresenter.cs` and `AgcExports.cs`
**Learning:** Guest enqueue paths generate significant memory pressure and trigger frequent garbage collections due to nested LINQ expressions evaluating dynamically evaluated queries (`.Where()`, `.Select()`, `.Concat()`, `.Distinct()`) every single frame for image writers and buffer bindings. Enumerable allocations happen inside the main lock, exacerbating thread contention.
**Action:** Replace LINQ chains inside `RecordGuestImageWritersLocked` with manual `foreach` or standard indexed `for` loops combined with stack-allocated or rented spans to achieve zero-allocation hot paths.
