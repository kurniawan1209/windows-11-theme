using System;
using CustomDesktop.Core.Models;

namespace CustomDesktop.Services;

public interface ISettingsService
{
    AppSettings CurrentSettings { get; }
    void Load();
    void Save();
    void UpdateSettings(Action<AppSettings> updateAction);
    event Action<AppSettings>? SettingsChanged;
}
