using System;

namespace oracleofages;

/// <summary>
/// Resolves the native Gasha reward branches while consuming the shared game
/// RNG only at the two source call sites: distribution selection and ring-tier
/// selection.
/// </summary>
internal static class GashaRewardResolver
{
    internal static Result Give(
        GashaSpotDatabase database,
        SpotRecord spot,
        OracleSaveData save,
        InventoryState inventory,
        Func<byte> nextRandom)
    {
        int rewardType;
        if (!save.HasHarvestedFirstGashaNut)
        {
            save.SetGashaHarvestFlag(0);
            rewardType = 4;
        }
        else
        {
            int maturityClass = database.MaturityClass(save.GashaMaturity);
            rewardType = database.SelectRewardType(
                spot.Rank, maturityClass, nextRandom());
            if (rewardType == 6 &&
                inventory.HasTreasure(TreasureId.Potion))
            {
                inventory.RefillHealth();
            }
            if (rewardType == 0)
            {
                if (save.HasHarvestedGashaHeartPiece)
                    rewardType = 1;
                save.SetGashaHarvestFlag(1);
            }
            save.SubtractGashaMaturity(database.HarvestMaturityCost);
        }

        RewardRecord reward = database.GetReward(rewardType);
        int parameter = reward.Parameter;
        if (reward.TreasureId == TreasureId.Ring)
            parameter = database.SelectRing(parameter, nextRandom());
        int previousHeartPieces = inventory.HeartPieces;
        inventory.GiveTreasure(reward.TreasureId, parameter);
        return new Result(
            rewardType,
            reward,
            parameter,
            reward.TreasureId == TreasureId.HeartPiece &&
                previousHeartPieces == 3);
    }
}

internal readonly record struct Result(int RewardType, RewardRecord Reward, int Parameter, bool CompletesHeartContainer);
