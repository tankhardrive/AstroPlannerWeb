using Blazored.LocalStorage;
using AstroPlannerWeb.Models;

namespace AstroPlannerWeb.Services;

public class SettingsService
{
    private const string Key = "astroplanner-settings";
    private readonly ILocalStorageService _storage;
    private AppSettings? _cached;

    public SettingsService(ILocalStorageService storage) { _storage = storage; }

    public async Task<AppSettings> LoadAsync()
    {
        if (_cached != null) return _cached;
        _cached = await _storage.GetItemAsync<AppSettings>(Key) ?? new AppSettings();
        _cached.MigrateIfNeeded();
        return _cached;
    }

    public async Task SaveAsync(AppSettings settings)
    {
        _cached = settings;
        await _storage.SetItemAsync(Key, settings);
    }

    /// <summary>Save the currently-cached settings. No-op if nothing has been loaded yet.</summary>
    public async Task SaveAsync()
    {
        if (_cached != null)
            await _storage.SetItemAsync(Key, _cached);
    }
}
