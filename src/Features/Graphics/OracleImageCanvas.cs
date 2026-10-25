using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

/// <summary>One RGBA8 composition with one CPU snapshot per source texture.</summary>
internal sealed class OracleImageCanvas
{
    private readonly int _width, _height;
    private readonly byte[] _pixels;
    private readonly Dictionary<Texture2D, (byte[] Pixels, int Width)> _sources = new();
    internal int Width => _width;
    internal int Height => _height;

    internal OracleImageCanvas(int width, int height)
    {
        _width = width;
        _height = height;
        _pixels = new byte[checked(width * height * 4)];
    }

    internal void Fill(Color color) => FillRect(new Rect2I(0, 0, _width, _height), color);

    internal void FillRect(Rect2I rectangle, Color color)
    {
        int right = Math.Min(_width, rectangle.End.X);
        int bottom = Math.Min(_height, rectangle.End.Y);
        for (int y = Math.Max(0, rectangle.Position.Y); y < bottom; y++)
        for (int x = Math.Max(0, rectangle.Position.X); x < right; x++)
            SetPixel(x, y, color);
    }

    internal Image ToImage() => Image.CreateFromData(_width, _height, false, Image.Format.Rgba8, _pixels);

    internal void BlitTexture(Texture2D texture, Rect2 target, Rect2 sourceRectangle, Color modulation)
    {
        if (!_sources.TryGetValue(texture, out var source))
        {
            // Texture readback can synchronize the GPU. Read each atlas only
            // once per redraw and release its native Image immediately.
            using Image image = texture.GetImage();
            if (image.GetFormat() != Image.Format.Rgba8)
                throw new InvalidOperationException("RGBA composition requires an RGBA8 source texture.");
            source = (image.GetData(), image.GetWidth());
            _sources.Add(texture, source);
        }
        for (int y = 0; y < (int)target.Size.Y; y++)
        for (int x = 0; x < (int)target.Size.X; x++)
        {
            int destinationX = (int)target.Position.X + x;
            int destinationY = (int)target.Position.Y + y;
            if ((uint)destinationX >= _width || (uint)destinationY >= _height) continue;
            int offset = (((int)sourceRectangle.Position.Y + y) * source.Width +
                (int)sourceRectangle.Position.X + x) * 4;
            Color ink = ReadPixel(source.Pixels, offset) * modulation;
            if (ink.A == 0) continue;
            Color background = ReadPixel(_pixels, (destinationY * _width + destinationX) * 4);
            float alpha = ink.A + background.A * (1 - ink.A);
            SetPixel(destinationX, destinationY, new Color(
                (ink.R * ink.A + background.R * background.A * (1 - ink.A)) / alpha,
                (ink.G * ink.A + background.G * background.A * (1 - ink.A)) / alpha,
                (ink.B * ink.A + background.B * background.A * (1 - ink.A)) / alpha, alpha));
        }
    }

    private static Color ReadPixel(byte[] pixels, int offset) => new(
        pixels[offset] / 255.0f, pixels[offset + 1] / 255.0f,
        pixels[offset + 2] / 255.0f, pixels[offset + 3] / 255.0f);

    internal void SetPixel(int x, int y, Color color)
    {
        // Match Image.SetPixel's RGBA8 truncation, including transparent RGB.
        int offset = (y * _width + x) * 4;
        _pixels[offset] = (byte)Math.Clamp(color.R * 255.0f, 0, 255);
        _pixels[offset + 1] = (byte)Math.Clamp(color.G * 255.0f, 0, 255);
        _pixels[offset + 2] = (byte)Math.Clamp(color.B * 255.0f, 0, 255);
        _pixels[offset + 3] = (byte)Math.Clamp(color.A * 255.0f, 0, 255);
    }
}
