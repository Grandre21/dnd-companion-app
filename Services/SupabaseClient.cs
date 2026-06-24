using Supabase.Gotrue;

namespace DndCompanion.Services;

/// <summary>
/// Facade che preserva la superficie del vecchio meta-client (<c>From</c>/<c>Rpc</c>/<c>Auth</c>),
/// così repository e AuthStateService restano invariati dopo lo split standalone
/// (postgrest-csharp + gotrue-csharp al posto di supabase-csharp).
/// </summary>
public sealed class SupabaseClient
{
    private readonly Postgrest.Client _postgrest;

    public SupabaseClient(Client auth, Postgrest.Client postgrest)
    {
        Auth = auth;
        _postgrest = postgrest;
    }

    /// <summary>Il client Gotrue (auth): CurrentSession, SignIn, SignOut, GetSessionFromUrl, ...</summary>
    public Client Auth { get; }

    /// <summary>Tabella tipizzata (Postgrest). Sostituisce il vecchio <c>Supabase.Client.From&lt;T&gt;()</c>.</summary>
    // postgrest-csharp 3.5.1: Client.Table&lt;T&gt;() dichiara il ritorno come IPostgrestTable&lt;T&gt;,
    // ma l'istanza concreta È sempre Postgrest.Table&lt;T&gt; (verificato a runtime) e i metodi fluent
    // dell'interfaccia ritornano comunque Table&lt;T&gt;. Manteniamo la firma pubblica Table&lt;T&gt; (i repo
    // ci concatenano Where/Get/Insert/...) facendo il cast esplicito.
    public Postgrest.Table<T> From<T>() where T : Postgrest.Models.BaseModel, new()
        => (Postgrest.Table<T>)_postgrest.Table<T>();

    /// <summary>Chiamata RPC (stored procedure). Usata da CampaignRepository.join_campaign.</summary>
    public Task<T?> Rpc<T>(string procedureName, Dictionary<string, object> parameters)
        => _postgrest.Rpc<T>(procedureName, parameters);
}
