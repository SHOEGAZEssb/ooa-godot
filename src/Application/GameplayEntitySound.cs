
namespace Oracles;

internal static class GameplayEntitySound
{
    internal static Action Bind(RoomEntityManager entities, OracleSoundEngine sound)
    {
        Func<int> previousActiveMusic = entities.NativeActiveMusicSource;
        Func<int> previousRoomMusic = entities.NativeRoomMusicSource;
        Func<int, byte> previousAllocationAlias = entities.NativeSoundAllocationAliasSource;
        Func<int> activeMusic = () => sound.NativeActiveMusic;
        Func<int> roomMusic = () => sound.NativeRoomMusic;
        Func<int, byte> allocationAlias = sound.ReadNativeAllocationAlias;
        entities.SoundRequested += sound.PlaySound;
        entities.NativeMusicSelectionRequested += sound.SetNativeMusicSelection;
        entities.NativeSoundRequested += sound.PlayNativeSound;
        entities.NativeActiveMusicWritten += sound.SetNativeActiveMusic;
        entities.NativeActiveMusicSource = activeMusic;
        entities.NativeRoomMusicSource = roomMusic;
        entities.NativeChannelVolumeWritten += sound.SetNativeChannelVolume;
        entities.NativeSoundAllocationAliasWritten += sound.WriteNativeAllocationAlias;
        entities.NativeSoundAllocationAliasSource = allocationAlias;
        entities.RoomMusicRequested += sound.PlayRoomMusic;
        return () =>
        {
            entities.SoundRequested -= sound.PlaySound;
            entities.NativeMusicSelectionRequested -= sound.SetNativeMusicSelection;
            entities.NativeSoundRequested -= sound.PlayNativeSound;
            entities.NativeActiveMusicWritten -= sound.SetNativeActiveMusic;
            entities.NativeChannelVolumeWritten -= sound.SetNativeChannelVolume;
            entities.NativeSoundAllocationAliasWritten -= sound.WriteNativeAllocationAlias;
            entities.RoomMusicRequested -= sound.PlayRoomMusic;
            if (ReferenceEquals(entities.NativeActiveMusicSource, activeMusic))
                entities.NativeActiveMusicSource = previousActiveMusic;
            if (ReferenceEquals(entities.NativeRoomMusicSource, roomMusic))
                entities.NativeRoomMusicSource = previousRoomMusic;
            if (ReferenceEquals(entities.NativeSoundAllocationAliasSource, allocationAlias))
                entities.NativeSoundAllocationAliasSource = previousAllocationAlias;
        };
    }
}
