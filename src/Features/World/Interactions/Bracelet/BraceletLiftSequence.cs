using System;

namespace oracleofages;

/// <summary>
/// Shared ITEM_BRACELET/ITEM_BOMB state-2 pickup animation.
/// </summary>
internal static class BraceletLiftSequence
{
    internal static bool Advance(
        Player player,
        ref int counter,
        int lowFrames,
        int middleFrames,
        int highFrames,
        Action<int> setLiftOffset)
    {
        counter++;
        int middleBoundary = lowFrames + middleFrames;
        int finishedBoundary = middleBoundary + highFrames;
        if (counter <= lowFrames)
        {
            player.SetBraceletActionPose(BraceletActionPose.PullStrain);
            setLiftOffset(0);
            return false;
        }
        if (counter <= middleBoundary)
        {
            player.SetBraceletActionPose(BraceletActionPose.Pull);
            setLiftOffset(1);
            return false;
        }

        setLiftOffset(2);
        if (counter < finishedBoundary)
            return false;

        player.ClearBraceletActionPose();
        return true;
    }
}
