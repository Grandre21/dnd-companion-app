using DndCompanion.Models;

namespace DndCompanion.Services.Repositories;

public interface INoteRepository
{
    Task<List<Note>> GetNotesForCampaignAsync(string campaignId, string userId);
    Task<Note?> CreateNoteAsync(Note note);
    Task<Note?> UpdateNoteAsync(Note note);
    Task DeleteNoteAsync(string id);
}

/// <summary>Accesso dati per le note di campagna (tabella <c>notes</c>).</summary>
public class NoteRepository : INoteRepository
{
    private readonly SupabaseService _supabase;

    public NoteRepository(SupabaseService supabase) => _supabase = supabase;

    public async Task<List<Note>> GetNotesForCampaignAsync(string campaignId, string userId)
    {
        var client = await _supabase.GetClientAsync();
        var response = await client.From<Note>()
            .Where(n => n.CampaignId == campaignId)
            .Get();
        // Visibili nella campagna: le condivise + le proprie note private.
        return response.Models
            .Where(n => n.IsShared || n.OwnerId == userId)
            .OrderByDescending(n => n.UpdatedAt ?? n.CreatedAt ?? DateTime.MinValue)
            .ToList();
    }

    public async Task<Note?> CreateNoteAsync(Note note)
    {
        var now = DateTime.UtcNow;
        note.CreatedAt = now;
        note.UpdatedAt = now;
        var client = await _supabase.GetClientAsync();
        var response = await client.From<Note>().Insert(note);
        return response.Models.FirstOrDefault();
    }

    public async Task<Note?> UpdateNoteAsync(Note note)
    {
        note.UpdatedAt = DateTime.UtcNow;
        var client = await _supabase.GetClientAsync();
        var response = await client.From<Note>().Update(note);
        return response.Models.FirstOrDefault();
    }

    public async Task DeleteNoteAsync(string id)
    {
        var client = await _supabase.GetClientAsync();
        await client.From<Note>().Where(n => n.Id == id).Delete();
    }
}
