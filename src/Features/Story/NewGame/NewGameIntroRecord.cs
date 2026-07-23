using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;
public readonly record struct NewGameIntroRecord(int InitialWaitFrames, int VoiceWaitFrames, int PostVanishWaitFrames, int SummonFrames, int LinkX, int LinkY, int LinkSummonedFlag, int PregameIntroDoneFlag, int TextPosition, int TextId, int SpinFrameDuration, int[] SpinGraphics, int[] VanishDurations, int[] VanishGraphics, int[] DescendOscillation, int[] HoverOscillation, string Text);
