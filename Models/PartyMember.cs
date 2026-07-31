using Newtonsoft.Json;

namespace DndCompanion.Models;

/// <summary>
/// Riga sintetica del gruppo (esito della RPC <c>get_party_overview</c>), non una tabella: niente
/// PG altrui inventario/incantesimi/note/background, solo le stat che la pagina Party deve mostrare
/// a ogni membro della campagna.
///
/// ATTENZIONE (gotcha verificato leggendo postgrest-csharp 3.5.1, commit a14aac0 —
/// <c>Postgrest/Client.cs</c>, <c>Rpc&lt;TModeledResponse&gt;</c>): la chiamata RPC generica
/// deserializza con <c>JsonConvert.DeserializeObject&lt;T&gt;(response.Content)</c> SENZA passare le
/// <c>SerializerSettings</c> del client, quindi SENZA il <c>PostgrestContractResolver</c> che altrove
/// traduce <c>[Postgrest.Attributes.Column("snake_case")]</c> in nomi di proprietà PascalCase (quello
/// che fa <c>Table&lt;T&gt;()</c> per le query dirette, es. <see cref="Character"/>). Qui l'attributo
/// Postgrest Column NON avrebbe alcun effetto: la mappatura va fatta con
/// <see cref="Newtonsoft.Json.JsonPropertyAttribute"/>, che la deserializzazione di default rispetta
/// anche senza contract resolver custom.
/// </summary>
public class PartyMember
{
    [JsonProperty("character_id")]
    public string CharacterId { get; set; } = string.Empty;

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("race")]
    public string? Race { get; set; }

    [JsonProperty("class")]
    public string? Class { get; set; }

    [JsonProperty("level")]
    public int Level { get; set; }

    [JsonProperty("armor_class")]
    public int ArmorClass { get; set; }

    [JsonProperty("hit_points")]
    public int HitPoints { get; set; }

    [JsonProperty("max_hit_points")]
    public int MaxHitPoints { get; set; }

    /// <summary>Percezione passiva, già calcolata lato server (RPC): la riga non porta saggezza
    /// né competenze grezze, quindi qui non è ricalcolabile lato client (v. <see cref="Services.PartyOverviewCalculations"/>).</summary>
    [JsonProperty("passive_perception")]
    public int PassivePerception { get; set; }

    /// <summary>Velocità in metri, stessa unità di <see cref="Character.Speed"/>.</summary>
    [JsonProperty("speed")]
    public int Speed { get; set; }

    [JsonProperty("owner_id")]
    public string OwnerId { get; set; } = string.Empty;

    [JsonProperty("owner_nickname")]
    public string OwnerNickname { get; set; } = string.Empty;
}
