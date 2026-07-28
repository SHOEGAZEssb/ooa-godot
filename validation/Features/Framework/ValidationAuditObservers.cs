using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace oracleofages;

internal sealed class ValidationSoundRequestAudit : IOracleSoundRequestObserver
{
    private readonly int[] _counts = new int[0x100];
    private readonly List<int> _requests = new();

    public IReadOnlyList<int> Requests => _requests;
    public int LastRequest => _requests.Count == 0 ? 0 : _requests[^1];

    public void OnSoundRequested(int soundId)
    {
        _counts[soundId & 0xff]++;
        _requests.Add(soundId);
    }

    public int RequestsFor(int soundId) => _counts[soundId & 0xff];

    public void Clear()
    {
        Array.Clear(_counts);
        _requests.Clear();
    }
}

internal static class ValidationSoundRequestExtensions
{
    private static readonly ConditionalWeakTable<
        OracleSoundEngine,
        ValidationSoundRequestAudit> Audits = new();

    public static ValidationSoundRequestAudit AttachPlayRequestAudit(
        this OracleSoundEngine sound)
    {
        if (!Audits.TryGetValue(sound, out ValidationSoundRequestAudit? audit))
        {
            audit = new ValidationSoundRequestAudit();
            Audits.Add(sound, audit);
        }
        audit.Clear();
        sound.SetRequestObserver(audit);
        return audit;
    }

    public static void ClearPlayRequestAudit(this OracleSoundEngine sound) =>
        Audit(sound).Clear();

    public static int PlayRequestsFor(
        this OracleSoundEngine sound,
        int soundId) =>
        Audit(sound).RequestsFor(soundId);

    public static int LastPlayRequestForValidation(
        this OracleSoundEngine sound) =>
        Audit(sound).LastRequest;

    private static ValidationSoundRequestAudit Audit(OracleSoundEngine sound)
    {
        if (!Audits.TryGetValue(sound, out ValidationSoundRequestAudit? audit))
        {
            throw new InvalidOperationException(
                "The validation sound-request observer was not attached.");
        }
        return audit;
    }
}

internal sealed class ValidationGraphicsCacheAudit :
    IOracleGraphicsCacheObserver
{
    private readonly List<OracleGraphicsCacheObservation> _observations = new();

    public IReadOnlyList<OracleGraphicsCacheObservation> Observations =>
        _observations;

    public void OnGraphicsCacheOperation(
        OracleGraphicsCacheObservation observation) =>
        _observations.Add(observation);

    public int Count(OracleGraphicsCacheOperation operation)
    {
        int count = 0;
        foreach (OracleGraphicsCacheObservation observation in _observations)
        {
            if (observation.Operation == operation)
                count++;
        }
        return count;
    }

    public void Clear() => _observations.Clear();

    public static ValidationGraphicsCacheAudit Attach()
    {
        var audit = new ValidationGraphicsCacheAudit();
        OracleGraphicsCache.SetObserver(audit);
        return audit;
    }
}

internal sealed class ValidationCombatEffectAudit : ICombatEffectObserver
{
    private readonly List<ClinkEffect> _clinkEffects = new();

    public int ClinkEffectsSpawned => _clinkEffects.Count;
    public ClinkEffect? LastClinkEffect =>
        _clinkEffects.Count == 0 ? null : _clinkEffects[^1];

    public void OnClinkEffectSpawned(ClinkEffect effect) =>
        _clinkEffects.Add(effect);

    public void Clear() => _clinkEffects.Clear();
}
