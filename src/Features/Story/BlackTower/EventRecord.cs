using System;
using System.Collections.Generic;

namespace oracleofages;
internal readonly record struct EventRecord(int Group, int Room, int MakuSeedTreasure, int CompletionFlag, int RalphEnteredFlag, int ClinkSound, int Gravity, int RalphId, int RalphSubId, int ImpaId, int ImpaUnlinkedSubId, int ImpaLinkedSubId, int NayruId, int NayruLinkedSubId, int NayruSpawnedSubId, int ZeldaId, int ZeldaSubId, int EffectId, int EffectSubId, string EffectSprite, int EffectTileBase, int EffectPalette, string EffectAnimation);
