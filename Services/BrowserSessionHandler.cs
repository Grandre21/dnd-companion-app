using Microsoft.JSInterop;
using Newtonsoft.Json;
using Supabase.Gotrue;
using Supabase.Gotrue.Interfaces;

namespace DndCompanion.Services;

/// <summary>
/// Persistenza della sessione Gotrue su localStorage del browser.
/// L'interfaccia IGotrueSessionPersistence è SINCRONA, quindi in Blazor WASM
/// usiamo IJSInProcessRuntime (Invoke sincrono) invece di IJSRuntime (async).
/// La Session di Gotrue è serializzata con Newtonsoft.Json (i suoi attributi
/// [JsonProperty] sono Newtonsoft), coerentemente con la libreria.
/// </summary>
public class BrowserSessionHandler : IGotrueSessionPersistence<Session>
{
    private const string StorageKey = "supabase.session";
    private readonly IJSInProcessRuntime _js;

    public BrowserSessionHandler(IJSInProcessRuntime js) => _js = js;

    public void SaveSession(Session session)
        => _js.InvokeVoid("localStorage.setItem", StorageKey, JsonConvert.SerializeObject(session));

    public void DestroySession()
        => _js.InvokeVoid("localStorage.removeItem", StorageKey);

    public Session? LoadSession()
    {
        var json = _js.Invoke<string?>("localStorage.getItem", StorageKey);
        return string.IsNullOrEmpty(json) ? null : JsonConvert.DeserializeObject<Session>(json);
    }
}
