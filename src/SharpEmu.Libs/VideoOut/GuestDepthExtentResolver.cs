// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using SharpEmu.Libs.Gpu;

namespace SharpEmu.Libs.VideoOut;

internal enum GuestDepthExtentResolutionKind
{
    Exact,
    TextureAlias,
    StaleOneByOne,
    Mismatch,
}

internal readonly record struct GuestDepthExtentResolution(
    GuestDepthExtentResolutionKind Kind,
    uint Width,
    uint Height)
{
    public bool IsUsable => Kind != GuestDepthExtentResolutionKind.Mismatch;
}

internal static class GuestDepthExtentResolver
{
    public static GuestDepthExtentResolution Resolve(
        GuestDepthTarget depth,
        uint colorWidth,
        uint colorHeight,
        IReadOnlyList<GuestDrawTexture> textures)
    {
        if (depth.Width >= colorWidth && depth.Height >= colorHeight)
        {
            return new GuestDepthExtentResolution(
                GuestDepthExtentResolutionKind.Exact,
                depth.Width,
                depth.Height);
        }
        GuestDrawTexture? matchingTexture = null;
        for (int i = 0; i < textures.Count; i++)
        {
            var texture = textures[i];
            if ((texture.Address == depth.Address ||
                 texture.Address == depth.ReadAddress ||
                 texture.Address == depth.WriteAddress) &&
                texture.Width >= colorWidth &&
                texture.Height >= colorHeight)
            {
                matchingTexture = texture;
                break;
            }
        }

        if (matchingTexture is not null)
        {
            return new GuestDepthExtentResolution(
                GuestDepthExtentResolutionKind.TextureAlias,
                matchingTexture.Width,
                matchingTexture.Height);
        }

        if (depth.Width == 1 && depth.Height == 1)
        {
            return new GuestDepthExtentResolution(
                GuestDepthExtentResolutionKind.StaleOneByOne,
                colorWidth,
                colorHeight);
        }

        return new GuestDepthExtentResolution(
            GuestDepthExtentResolutionKind.Mismatch,
            depth.Width,
            depth.Height);
    }
}
