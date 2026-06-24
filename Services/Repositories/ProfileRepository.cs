using DndCompanion.Models;

namespace DndCompanion.Services.Repositories;

public interface IProfileRepository
{
    Task<List<Profile>> GetProfilesAsync();
    Task EnsureProfileAsync(string userId, string? displayName);
}

/// <summary>Accesso dati per i profili utente (tabella <c>profiles</c>).</summary>
public class ProfileRepository : IProfileRepository
{
    private readonly SupabaseService _supabase;

    public ProfileRepository(SupabaseService supabase) => _supabase = supabase;

    public async Task<List<Profile>> GetProfilesAsync()
    {
        var client = await _supabase.GetClientAsync();
        var response = await client.From<Profile>().Get();
        return response.Models;
    }

    /// <summary>Crea la riga profiles per l'utente se non esiste già. Idempotente.</summary>
    public async Task EnsureProfileAsync(string userId, string? displayName)
    {
        var client = await _supabase.GetClientAsync();
        var existing = await client.From<Profile>().Where(p => p.Id == userId).Get();
        if (existing.Models.Count > 0) return;

        await client.From<Profile>().Insert(new Profile
        {
            Id = userId,
            DisplayName = displayName,
            CreatedAt = DateTime.UtcNow
        });
    }
}
