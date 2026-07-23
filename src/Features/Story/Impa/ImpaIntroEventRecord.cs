using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
public readonly record struct ImpaIntroEventRecord(int Group, int Room, int InteractionId, int SubId, int RoomFlag, int LinkWaitFrames, int TargetX, int CenterWaitFrames, int ApproachFrames, int LinkSpeed, int ImpaWaitFrames, int TextId, int PostTextFrames, int ImpaSpeed, int ImpaMoveFrames, int FollowLag, string UpAnimation, string RightAnimation, string DownAnimation, string LeftAnimation, string Text, int LinkedTextId, string LinkedText);
