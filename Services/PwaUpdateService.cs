using Microsoft.JSInterop;

namespace DndCompanion.Services;

/// <summary>
/// Gestisce la notifica di aggiornamento PWA (nuovo service worker in stato "waiting").
/// Lo script in index.html (window.pwaUpdate) rileva l'update e invoca via JS interop
/// <see cref="NotifyUpdateAvailable"/>; il banner si sottoscrive a <see cref="OnUpdateAvailable"/>.
/// On-demand: l'aggiornamento viene applicato SOLO su azione utente (<see cref="ApplyUpdateAsync"/>).
/// DEVE restare registrato come Singleton e vivo per tutta l'app: <see cref="InitializeAsync"/> è
/// idempotente e il <see cref="DotNetObjectReference{T}"/> non va disposto finché l'app è attiva
/// (lato JS window.pwaUpdate mantiene il riferimento).
/// </summary>
public class PwaUpdateService : IDisposable
{
    private readonly IJSRuntime _js;
    private DotNetObjectReference<PwaUpdateService>? _selfRef;
    private bool _initialized;

    public PwaUpdateService(IJSRuntime js)
    {
        _js = js;
    }

    /// <summary>True se è stato rilevato un service worker aggiornato in attesa.</summary>
    public bool UpdateAvailable { get; private set; }

    /// <summary>Notifica i componenti (es. il banner) quando è disponibile un aggiornamento.</summary>
    public event Action? OnUpdateAvailable;

    /// <summary>Registra il callback .NET presso window.pwaUpdate. Idempotente.</summary>
    public async Task InitializeAsync()
    {
        if (_initialized) return;
        _initialized = true;

        _selfRef = DotNetObjectReference.Create(this);
        await _js.InvokeVoidAsync("pwaUpdate.registerCallback", _selfRef);
    }

    /// <summary>Invocato da JS quando un nuovo service worker è in waiting.</summary>
    [JSInvokable]
    public void NotifyUpdateAvailable()
    {
        if (UpdateAvailable) return;
        UpdateAvailable = true;
        OnUpdateAvailable?.Invoke();
    }

    /// <summary>Applica l'update: skipWaiting sul SW in attesa (poi reload via 'controllerchange').</summary>
    public async Task ApplyUpdateAsync()
    {
        await _js.InvokeVoidAsync("pwaUpdate.applyUpdate");
    }

    public void Dispose() => _selfRef?.Dispose();
}
