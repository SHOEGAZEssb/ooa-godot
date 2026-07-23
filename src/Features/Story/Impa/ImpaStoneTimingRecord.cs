using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
public readonly record struct ImpaStoneTimingRecord(int SpotHoldFrames, int SpotJumpSpeedZ, int Gravity, int FirstLandingWait, int FirstTextPostFrames, int ApproachSpeed, int StoneWaitFrames, int SecondHoldFrames, int SecondJumpSpeedZ, int SecondLandingWait, int SignTextPostFrames, int LinkAxisWaitFrames, int LinkTargetWaitFrames, int LinkFaceWaitFrames, int LinkSpeed, int RequestLeadFrames, int RequestPostFrames, int BackAwaySpeed, int FirstBackAwayFrames, int BetweenFirstBackAwayFrames, int HesitationPostFrames, int SecondBackAwayFrames, int BetweenSecondBackAwayFrames, int FailurePostFrames, int PushHoldFrames, int StoneMoveFrames, int StoneSpeed, int LinkPushSpeed, int ReactionLeadFrames, int LeftCorrectionFrames, int LeftCorrectionSpeed, int RightBranchWaitFrames, int CommonWaitFrames, int ResponseRightFrames, int ResponseRightSpeed, int ResponseWait1Frames, int ResponseUpFrames, int ResponseWait2Frames, int PoseWaitFrames, int ThanksPostFrames, int FinalSpeed, int FinalMoveFrames);
