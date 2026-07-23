using Godot;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace oracleofages;
internal readonly record struct TextGlyph(int Code, FontSource Source, int ColorIndex, int CharacterSound, int SoundEffect);
