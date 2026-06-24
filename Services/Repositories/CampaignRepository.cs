using System.Security.Cryptography;
using DndCompanion.Models;

namespace DndCompanion.Services.Repositories;

public interface ICampaignRepository
{
    Task<List<Campaign>> GetUserCampaignsAsync(string userId);
    Task<Campaign> CreateCampaignAsync(string name, string ownerId);
    Task<Campaign?> JoinCampaignAsync(string inviteCode, string userId);
    Task<List<CampaignMember>> GetCampaignMembersAsync(string campaignId);
    Task<string?> GetUserRoleInCampaignAsync(string userId, string campaignId);
    Task DeleteCampaignAsync(string campaignId);
    Task LeaveCampaignAsync(string campaignId, string userId);
}

/// <summary>Accesso dati per campagne e membership (tabelle <c>campaigns</c> / <c>campaign_members</c>).</summary>
public class CampaignRepository : ICampaignRepository
{
    private readonly SupabaseService _supabase;

    public CampaignRepository(SupabaseService supabase) => _supabase = supabase;

    // Alfabeto senza caratteri ambigui (niente 0/O, 1/I/L) per i codici invito.
    internal const string InviteCodeAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    public async Task<List<Campaign>> GetUserCampaignsAsync(string userId)
    {
        var client = await _supabase.GetClientAsync();
        var members = await client.From<CampaignMember>()
            .Where(m => m.UserId == userId)
            .Get();

        var ids = members.Models.Select(m => m.CampaignId).Distinct().ToList();
        if (ids.Count == 0) return new List<Campaign>();

        var campaigns = await client.From<Campaign>()
            .Filter("id", Postgrest.Constants.Operator.In, ids.Cast<object>().ToList())
            .Get();

        return campaigns.Models
            .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<Campaign> CreateCampaignAsync(string name, string ownerId)
    {
        var client = await _supabase.GetClientAsync();
        var inviteCode = await GenerateUniqueInviteCodeAsync();

        var insert = await client.From<Campaign>().Insert(new Campaign
        {
            Name = name,
            OwnerId = ownerId,
            InviteCode = inviteCode,
            CreatedAt = DateTime.UtcNow
        });
        var campaign = insert.Models.First();

        // Il creatore diventa automaticamente master della campagna.
        await client.From<CampaignMember>().Insert(new CampaignMember
        {
            CampaignId = campaign.Id,
            UserId = ownerId,
            Role = "master",
            JoinedAt = DateTime.UtcNow
        });

        return campaign;
    }

    public async Task<Campaign?> JoinCampaignAsync(string inviteCode, string userId)
    {
        var client = await _supabase.GetClientAsync();
        var code = inviteCode.Trim().ToUpperInvariant();

        // join_campaign (SECURITY DEFINER) valida il codice e garantisce la membership "player"
        // server-side, bypassando la RLS. Ritorna l'id (UUID) della campagna o null se il codice
        // non è valido. Sostituisce find_campaign_by_invite_code + INSERT diretto: la policy
        // campaign_members_insert (solo owner-come-master) blocca l'inserimento di membership dal
        // client, quindi il join dei player deve passare da qui. userId è derivato server-side da
        // auth.uid(): il parametro resta per compatibilità con i chiamanti.
        var campaignId = await client.Rpc<string>(
            "join_campaign",
            new Dictionary<string, object> { { "p_code", code } });

        if (string.IsNullOrEmpty(campaignId)) return null; // codice non valido

        // Ora siamo membri: la RLS campaigns_select ci permette di leggere la campagna.
        var found = await client.From<Campaign>()
            .Where(c => c.Id == campaignId)
            .Get();
        return found.Models.FirstOrDefault();
    }

    public async Task<List<CampaignMember>> GetCampaignMembersAsync(string campaignId)
    {
        var client = await _supabase.GetClientAsync();
        var response = await client.From<CampaignMember>()
            .Where(m => m.CampaignId == campaignId)
            .Get();
        return response.Models;
    }

    public async Task<string?> GetUserRoleInCampaignAsync(string userId, string campaignId)
    {
        var client = await _supabase.GetClientAsync();
        var response = await client.From<CampaignMember>()
            .Where(m => m.UserId == userId && m.CampaignId == campaignId)
            .Get();
        return response.Models.FirstOrDefault()?.Role;
    }

    /// <summary>
    /// Elimina la campagna (azione del master). Si affida alle FK ON DELETE CASCADE verso
    /// campaigns per cancellare characters/notes/spells/monsters/races/classes/campaign_members
    /// (e a cascata character_spells/inventory via character). Se le FK non fossero CASCADE,
    /// questa Delete fallirebbe per violazione di vincolo o lascerebbe righe orfane.
    /// </summary>
    public async Task DeleteCampaignAsync(string campaignId)
    {
        var client = await _supabase.GetClientAsync();
        await client.From<Campaign>().Where(c => c.Id == campaignId).Delete();
    }

    /// <summary>
    /// L'utente esce dalla campagna: rimuove solo la sua riga in campaign_members.
    /// NON elimina la campagna né il suo personaggio (resta visibile al master).
    /// </summary>
    public async Task LeaveCampaignAsync(string campaignId, string userId)
    {
        var client = await _supabase.GetClientAsync();
        await client.From<CampaignMember>()
            .Where(m => m.CampaignId == campaignId && m.UserId == userId)
            .Delete();
    }

    private async Task<string> GenerateUniqueInviteCodeAsync()
    {
        var client = await _supabase.GetClientAsync();
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var code = GenerateInviteCode();
            var clash = await client.From<Campaign>().Where(c => c.InviteCode == code).Get();
            if (clash.Models.Count == 0) return code;
        }
        // Fallback estremamente improbabile: aggiunge entropia per evitare loop infiniti.
        return GenerateInviteCode() + Guid.NewGuid().ToString("N")[..2].ToUpperInvariant();
    }

    // Lunghezza del codice (parte variabile). Con alfabeto a 31 caratteri:
    // 8 char ≈ 39.7 bit di entropia → enumerazione impraticabile (il codice dà
    // accesso ai dati condivisi della campagna).
    private const int InviteCodeLength = 8;

    internal static string GenerateInviteCode()
    {
        var alphabetLen = InviteCodeAlphabet.Length;
        // Soglia di rejection sampling per eliminare il bias di modulo.
        var maxUnbiased = 256 - (256 % alphabetLen);

        var chars = new char[InviteCodeLength];
        Span<byte> buffer = stackalloc byte[1];
        var i = 0;
        while (i < chars.Length)
        {
            RandomNumberGenerator.Fill(buffer);
            if (buffer[0] >= maxUnbiased) continue; // scarta i valori che introdurrebbero bias
            chars[i++] = InviteCodeAlphabet[buffer[0] % alphabetLen];
        }
        return "DND-" + new string(chars);
    }
}
