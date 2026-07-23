using System;
using System.Collections.Generic;

namespace oracleofages;
internal readonly record struct ItemRecord(int SubId, int Order, int Y, int X, int PriceTile, int Price, int TreasureId, int Parameter, int PromptTextId, int ItemTextId, string Sprite, int TileBase, int Palette, int AnimationIndex, string Animation, int ReplacementAddress, int ReplacementMask, int ReplacementSubId, int ReplacementXOffset);
