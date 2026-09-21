1. **Optimize `CollectAbandonedGuestImageVersions` in `VulkanVideoPresenter.cs`:**
   - Eliminate the `.ToArray()` allocation on `_guestImageVersions` and the `.ToHashSet()` allocation on `referencedVersions`.
   - Replace the LINQ queries for building `referencedVersions` with manual iteration over `_pendingGuestImagePresentations`, collecting up to 16 active versions into a `stackalloc long[16]` buffer (protected by the `_gate` lock).
   - Use `ArrayPool<long>.Shared` (if count > 64) or `stackalloc long[64]` to build a list of keys to remove from `_guestImageVersions`.
   - Iterate over the `_guestImageVersions` dictionary safely, check against the `referencedVersions` buffer using a standard `for` loop, and populate the `versionsToRemove` buffer.
   - Iterate over the `versionsToRemove` buffer to safely remove elements and enqueue them for destruction.
2. **Build and Test the changes:**
   - Run `dotnet build src/SharpEmu.Libs/SharpEmu.Libs.csproj` to ensure there are no compilation errors.
3. **Complete pre-commit steps:**
   - Complete pre-commit steps to ensure proper testing, verification, review, and reflection are done.
4. **Submit PR:**
   - Commit the changes and submit the PR with proper formatting, performance rationale, and measurements (via a custom benchmark if needed, or reasoned Big-O / allocation reduction).
