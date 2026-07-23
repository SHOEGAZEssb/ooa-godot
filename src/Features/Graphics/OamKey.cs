using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
internal readonly record struct OamKey(ulong SourceImageId, ulong SourceHash, string EncodedOam, int TileBase, int BasePalette, bool HasPaletteOverride, ulong PaletteColors01, ulong PaletteColors23, string PaletteOverrides, bool SourceGrayscaleInverted, CompositionMode Composition);
