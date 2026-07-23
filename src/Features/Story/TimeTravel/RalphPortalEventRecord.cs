using System;
using System.Collections.Generic;

namespace oracleofages;
internal readonly record struct RalphPortalEventRecord(int Group, int Room, int InteractionId, int SubId, int EntryDirection, int IntroDelayFrames, int PostTextFrames, int MovementCounter, int FlickerFrames, int Speed, int Angle, int GlobalFlag, int TextId, string MovementAnimation, string PortalAnimation, string Text);
