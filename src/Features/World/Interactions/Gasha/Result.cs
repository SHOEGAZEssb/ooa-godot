using System;

namespace oracleofages;
internal readonly record struct Result(int RewardType, RewardRecord Reward, int Parameter, bool CompletesHeartContainer);
