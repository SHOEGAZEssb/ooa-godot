using System;

namespace Oracles;

public partial class GameRoot
{
    private Action _releaseActiveGameplay = null!;
    private Action<OracleSaveData> _restoreCheckpointGameplay = null!;
    private SaveResult SaveActiveFile()
    {
        _saveWriteRequests++;
        return _persistSaveData ? StoreFileSlot(_activeSaveSlot, _saveData) : SaveResult.Succeeded;
    }

    internal void BeginGameOver() => _gameplayMenus!.BeginGameOver();

    private void RestartGameplayAfterDeath()
    {
        OracleSaveData liveSave = _saveData;
        Action release = _releaseActiveGameplay;
        Action<OracleSaveData> restore = _restoreCheckpointGameplay;
        release();
        _sound.RestartSound();
        liveSave.AdvancePlaytime();
        restore(liveSave);
    }
}
