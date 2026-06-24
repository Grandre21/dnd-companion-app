namespace DndCompanion.Services;

/// <summary>
/// Regola di autorizzazione lato client per la modifica delle risorse di campagna, speculare alla
/// RLS server-side: può modificare il <b>master</b> della campagna oppure il <b>proprietario</b>
/// della risorsa (<c>owner_id</c> / <c>added_by</c>). Funzione pura (nessuno stato/I/O), testabile.
/// È un controllo UX: la sicurezza vera resta nelle RLS del database.
/// </summary>
public static class AccessControl
{
    /// <param name="isMaster">L'utente è master della campagna attiva.</param>
    /// <param name="ownerId">Proprietario della risorsa (<c>owner_id</c> o <c>added_by</c>); può essere null/vuoto.</param>
    /// <param name="currentUserId">Id dell'utente corrente; null se non loggato.</param>
    public static bool CanEdit(bool isMaster, string? ownerId, string? currentUserId)
        => isMaster || ownerId == currentUserId;
}
