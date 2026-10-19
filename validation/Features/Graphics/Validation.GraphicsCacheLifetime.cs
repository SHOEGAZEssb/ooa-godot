using Godot;
using System;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateGraphicsCacheLifetime()
    {
        const string path = "res://assets/oracle/gfx/gfx_font.png";
        Image source = OracleGraphicsCache.LoadImage(path);
        Texture2D font = OracleGraphicsCache.LoadMonochromeFont(path);
        using Image original = OracleGraphicsCache.LoadRawPngForValidation(path);
        ulong expected = OracleGraphicsCache.PixelHash(original);
        for (int repeat = 0; repeat < 8; repeat++)
        {
            var nested = new FrontendValidationRoot();
            AddChild(nested);
            nested.Free(); // The surrounding application still owns the shared cache.
            CollectReleasedSessions();
            Image retained = OracleGraphicsCache.LoadImage(path);
            Texture2D retainedFont = OracleGraphicsCache.LoadMonochromeFont(path);
            FailIf(!ReferenceEquals(source, retained) || !ReferenceEquals(font, retainedFont),
                "Exiting a temporary application root invalidated another live root's shared graphics cache.");
            FailIf(!GodotObject.IsInstanceValid(source) || !GodotObject.IsInstanceValid(font) ||
                OracleGraphicsCache.PixelHash(retained) != expected,
                "Shared source graphics changed or were released while an application root remained active.");
        }
        GD.Print("Validated shared graphics and font lifetime across eight nested-root exits and forced collections.");
    }
}
