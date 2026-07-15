// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using SharpEmu.Libs.Gpu.Vulkan;
using SharpEmu.ShaderCompiler;
using SharpEmu.ShaderCompiler.Vulkan;

namespace SharpEmu.Libs.Gpu.NativeVulkan;

/// <summary>Native C++ Vulkan implementation of the guest-domain GPU seam.</summary>
internal sealed unsafe class NativeVulkanGuestGpuBackend : IGuestGpuBackend
{
    private readonly object _startGate = new();
    private readonly BlockingCollection<Action<nint>> _commands = new(new ConcurrentQueue<Action<nint>>(), 256);
    private readonly ManualResetEventSlim _ready = new(false);
    private Thread? _thread;
    private Exception? _startError;

    public void EnsureStarted(uint width, uint height)
    {
        if (width == 0 || height == 0) return;
        lock (_startGate)
        {
            if (_thread is null)
            {
                _thread = new Thread(() => Run(width, height))
                {
                    IsBackground = true,
                    Name = "SharpEmu native Vulkan",
                };
                _thread.Start();
            }
        }
        _ready.Wait();
        if (_startError is not null) throw new InvalidOperationException("Native Vulkan startup failed", _startError);
    }

    public bool TryCompileVertexShader(Gen5ShaderState state, Gen5ShaderEvaluation evaluation,
        out IGuestCompiledShader? shader, out string error, int globalBufferBase = 0,
        int totalGlobalBufferCount = -1, int imageBindingBase = 0, int scalarRegisterBufferIndex = -1)
    {
        shader = null;
        if (!Gen5SpirvTranslator.TryCompileVertexShader(state, evaluation, out var compiled, out error,
                globalBufferBase, totalGlobalBufferCount, imageBindingBase, scalarRegisterBufferIndex)) return false;
        shader = new VulkanCompiledGuestShader(compiled.Spirv); return true;
    }

    public bool TryCompilePixelShader(Gen5ShaderState state, Gen5ShaderEvaluation evaluation,
        IReadOnlyList<Gen5PixelOutputBinding> outputs, out IGuestCompiledShader? shader, out string error,
        int globalBufferBase = 0, int totalGlobalBufferCount = -1, int imageBindingBase = 0,
        int scalarRegisterBufferIndex = -1)
    {
        shader = null;
        if (!Gen5SpirvTranslator.TryCompilePixelShader(state, evaluation, outputs, out var compiled, out error,
                globalBufferBase, totalGlobalBufferCount, imageBindingBase, scalarRegisterBufferIndex)) return false;
        shader = new VulkanCompiledGuestShader(compiled.Spirv); return true;
    }

    public bool TryCompileComputeShader(Gen5ShaderState state, Gen5ShaderEvaluation evaluation,
        uint localSizeX, uint localSizeY, uint localSizeZ, out IGuestCompiledShader? shader, out string error)
    {
        shader = null;
        if (!Gen5SpirvTranslator.TryCompileComputeShader(state, evaluation, localSizeX, localSizeY, localSizeZ,
                out var compiled, out error)) return false;
        shader = new VulkanCompiledGuestShader(compiled.Spirv); return true;
    }

    public void HideSplashScreen() { }

    public void Submit(byte[] bgraFrame, uint width, uint height)
    {
        if (bgraFrame.Length != checked((int)(width * height * 4))) return;
        EnsureStarted(width, height);
        Enqueue(handle =>
        {
            fixed (byte* pixels = bgraFrame)
                Check(handle, NativeVulkanApi.PresentBgra(handle, pixels, (nuint)bgraFrame.Length, width, height, width * 4));
        });
    }

    public void SubmitGuestDraw(GuestDrawKind drawKind, uint width, uint height)
    {
        if (drawKind != GuestDrawKind.FullscreenBarycentric || width == 0 || height == 0) return;
        EnsureStarted(width, height);
        var pixel = new VulkanCompiledGuestShader(SpirvFixedShaders.CreateBarycentricFragment());
        Enqueue(handle => Check(handle, NativeGpuPacket.SubmitDraw(handle, pixel, [], [],
            width, height, 1, null, 3, 1, 4, null, null, null, null, false)));
    }

    public void SubmitTranslatedDraw(IGuestCompiledShader pixelShader, IReadOnlyList<GuestDrawTexture> textures,
        IReadOnlyList<GuestMemoryBuffer> globalMemoryBuffers, uint width, uint height, uint attributeCount,
        IGuestCompiledShader? vertexShader = null, uint vertexCount = 3, uint instanceCount = 1,
        uint primitiveType = 4, GuestIndexBuffer? indexBuffer = null,
        IReadOnlyList<GuestVertexBuffer>? vertexBuffers = null, GuestRenderState? renderState = null)
    {
        EnsureStarted(width, height);
        var ps = Spirv(pixelShader); var vs = vertexShader is null ? null : Spirv(vertexShader);
        var textureCopy = textures.ToArray(); var memoryCopy = globalMemoryBuffers.ToArray();
        var vertexCopy = vertexBuffers?.ToArray();
        Enqueue(handle => Check(handle, NativeGpuPacket.SubmitDraw(handle, ps, textureCopy, memoryCopy,
            width, height, attributeCount, vs, vertexCount, instanceCount, primitiveType, indexBuffer,
            vertexCopy, renderState, null, false)));
    }

    public void SubmitOffscreenTranslatedDraw(IGuestCompiledShader pixelShader,
        IReadOnlyList<GuestDrawTexture> textures, IReadOnlyList<GuestMemoryBuffer> globalMemoryBuffers,
        uint attributeCount, IReadOnlyList<GuestRenderTarget> targets, IGuestCompiledShader? vertexShader = null,
        uint vertexCount = 3, uint instanceCount = 1, uint primitiveType = 4,
        GuestIndexBuffer? indexBuffer = null, IReadOnlyList<GuestVertexBuffer>? vertexBuffers = null,
        GuestRenderState? renderState = null)
    {
        if (targets.Count == 0) return;
        EnsureStarted(targets[0].Width, targets[0].Height);
        var ps = Spirv(pixelShader); var vs = vertexShader is null ? null : Spirv(vertexShader);
        var textureCopy = textures.ToArray(); var memoryCopy = globalMemoryBuffers.ToArray();
        var targetCopy = targets.ToArray(); var vertexCopy = vertexBuffers?.ToArray();
        Enqueue(handle => Check(handle, NativeGpuPacket.SubmitDraw(handle, ps, textureCopy, memoryCopy,
            targetCopy[0].Width, targetCopy[0].Height, attributeCount, vs, vertexCount, instanceCount,
            primitiveType, indexBuffer, vertexCopy, renderState, targetCopy, true)));
    }

    public void SubmitStorageTranslatedDraw(IGuestCompiledShader pixelShader,
        IReadOnlyList<GuestDrawTexture> textures, IReadOnlyList<GuestMemoryBuffer> globalMemoryBuffers,
        uint attributeCount, uint width, uint height)
    {
        EnsureStarted(width, height); var ps = Spirv(pixelShader);
        var textureCopy = textures.ToArray(); var memoryCopy = globalMemoryBuffers.ToArray();
        GuestRenderTarget[] targets = [new(0, width, height, 12, 7)];
        Enqueue(handle => Check(handle, NativeGpuPacket.SubmitDraw(handle, ps, textureCopy, memoryCopy,
            width, height, attributeCount, null, 3, 1, 4, null, null, null, targets, false)));
    }

    public void SubmitComputeDispatch(ulong shaderAddress, IGuestCompiledShader computeShader,
        IReadOnlyList<GuestDrawTexture> textures, IReadOnlyList<GuestMemoryBuffer> globalMemoryBuffers,
        uint groupCountX, uint groupCountY, uint groupCountZ)
    {
        EnsureStarted(1280, 720); var shader = Spirv(computeShader);
        var textureCopy = textures.ToArray(); var memoryCopy = globalMemoryBuffers.ToArray();
        Enqueue(handle => Check(handle, NativeGpuPacket.SubmitCompute(handle, shaderAddress, shader,
            textureCopy, memoryCopy, groupCountX, groupCountY, groupCountZ)));
    }

    public bool TrySubmitGuestImage(ulong address, uint width, uint height, uint pitchInPixel)
    {
        EnsureStarted(width, height);
        return Invoke(handle => NativeVulkanApi.PresentGuestImage(handle, address, width, height, pitchInPixel)) ==
               NativeGpuResult.Success;
    }

    public void RegisterKnownDisplayBuffer(ulong address, uint guestFormat)
    {
        EnsureStarted(1280, 720);
        Enqueue(handle => Check(handle, NativeVulkanApi.RegisterDisplayBuffer(handle, address, guestFormat)));
    }

    public bool IsGpuGuestImageAvailable(ulong address, uint format, uint numberType)
    {
        EnsureStarted(1280, 720);
        return Invoke(handle => NativeVulkanApi.HasGuestImage(handle, address, format, numberType)) ==
               NativeGpuResult.Success;
    }

    public bool TrySubmitGuestImageBlit(ulong sourceAddress, uint sourceWidth, uint sourceHeight,
        uint sourceFormat, ulong destinationAddress, uint destinationWidth, uint destinationHeight,
        uint destinationFormat)
    {
        EnsureStarted(destinationWidth, destinationHeight);
        return Invoke(handle => NativeVulkanApi.BlitGuestImage(handle, sourceAddress, sourceWidth, sourceHeight,
            sourceFormat, destinationAddress, destinationWidth, destinationHeight, destinationFormat)) ==
               NativeGpuResult.Success;
    }

    public bool TryGetRenderTargetOutputKind(uint dataFormat, uint numberType,
        out Gen5PixelOutputKind outputKind)
    {
        var result = NativeVulkanApi.RenderTargetOutputKind(dataFormat, numberType, out var nativeKind);
        outputKind = (Gen5PixelOutputKind)nativeKind; return result == NativeGpuResult.Success;
    }

    private void Run(uint width, uint height)
    {
        nint backend = 0;
        try
        {
            if (NativeVulkanApi.GetAbiVersion() != NativeVulkanApi.AbiVersion)
                throw new InvalidOperationException("Native Vulkan ABI version mismatch");
            var title = Marshal.StringToCoTaskMemUTF8("SharpEmu");
            try
            {
                var info = new NativeVulkanApi.CreateInfo
                {
                    StructSize = (uint)sizeof(NativeVulkanApi.CreateInfo), AbiVersion = NativeVulkanApi.AbiVersion,
                    Width = width, Height = height, TitleUtf8 = (byte*)title,
                    EnableValidation = Environment.GetEnvironmentVariable("SHARPEMU_VK_VALIDATION") == "1" ? 1u : 0u,
                };
                var result = NativeVulkanApi.Create(&info, out backend);
                if (result != NativeGpuResult.Success)
                    throw new InvalidOperationException($"se_gpu_create failed with {result}: {NativeVulkanApi.GetError(0)}");
            }
            finally { Marshal.FreeCoTaskMem(title); }
            _ready.Set();
            NativeGpuInputSource.Instance.Attach();
            while (true)
            {
                if (_commands.TryTake(out var command, 8))
                {
                    command(backend);
                    for (var drained = 1; drained < 128 && _commands.TryTake(out command); ++drained)
                        command(backend);
                }
                var result = NativeVulkanApi.Poll(backend, out var shouldClose);
                if (result != NativeGpuResult.Success || shouldClose != 0) break;
                var input = new NativeVulkanApi.Input { StructSize = (uint)sizeof(NativeVulkanApi.Input) };
                if (NativeVulkanApi.InputSnapshot(backend, &input) == NativeGpuResult.Success)
                    NativeGpuInputSource.Instance.Update(&input);
            }
        }
        catch (Exception exception)
        {
            _startError ??= exception;
            Console.Error.WriteLine($"[LOADER][ERROR] Native Vulkan backend failed: {exception}");
        }
        finally
        {
            _ready.Set();
            if (backend != 0) NativeVulkanApi.Destroy(backend);
        }
    }

    private void Enqueue(Action<nint> command)
    {
        if (!_commands.TryAdd(command)) Console.Error.WriteLine("[LOADER][WARN] Native GPU queue is full; dropping work");
    }

    private NativeGpuResult Invoke(Func<nint, NativeGpuResult> operation)
    {
        var completion = new TaskCompletionSource<NativeGpuResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        Enqueue(handle =>
        {
            try { completion.SetResult(operation(handle)); }
            catch (Exception exception) { completion.SetException(exception); }
        });
        return completion.Task.GetAwaiter().GetResult();
    }

    private static void Check(nint backend, NativeGpuResult result)
    {
        if (result is NativeGpuResult.Success or NativeGpuResult.NotReady) return;
        Console.Error.WriteLine($"[LOADER][ERROR] Native GPU operation failed: {result}: {NativeVulkanApi.GetError(backend)}");
    }

    private static VulkanCompiledGuestShader Spirv(IGuestCompiledShader shader) =>
        shader as VulkanCompiledGuestShader ?? throw new InvalidOperationException(
            $"Shader type {shader.GetType().Name} was not compiled by the native Vulkan backend");
}
