using System;
using System.Collections.Generic;

namespace oracleofages;
internal readonly record struct EnterPastEventRecord(int Group, int Room, int InteractionId, int SubId, int IntroWaitFrames, int PreJumpWaitFrames, int PostJumpWaitFrames, int PostTextWaitFrames, int JumpSpeedZ, int JumpGravity, int FastSpeed, int SlowSpeed, int FirstDownCounter, int RightCounter, int SecondDownCounter, int SlowDownCounter, int FinalDownCounter, int GlobalFlag, int TextId, string RightAnimation, string DownAnimation, string Text, int JumpSound, int ExpectedArrivalCounter);
