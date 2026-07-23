using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
internal readonly record struct MakuTreeCutsceneRecord(int Group, int Room, int InteractionId, int SubId, int InitialPaletteHeader, int InputIdleFrames, int InputRightFrames, int InputStopFrames, int InputUpFrames, int InputTailFrames, int IntroDelayFrames, int PostIntroFrames, int FrownFrames, int DisappearanceFrames, int PostAhhFrames, int FinishDelayFrames, int SourceTransition, int DestinationGroup, int DestinationRoom, int DestinationPosition, int DestinationParameter, int DestinationTransition, string Animation0, string Animation1, string Animation2, string Animation3, string Animation4, string ExtraSprite, int TextboxPosition, string IntroText, string AhhText, string HelpText);
