using Postgrest.Attributes;
using Postgrest.Models;

namespace DndCompanion.Models;

[Table("classes")]
public class CharacterClass : BaseModel
{
    [PrimaryKey("id", false)]
    public string Id { get; set; } = string.Empty;

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("description")]
    public string Description { get; set; } = string.Empty;

    [Column("hit_die")]
    public string HitDie { get; set; } = string.Empty;

    [Column("primary_ability")]
    public string PrimaryAbility { get; set; } = string.Empty;

    [Column("saving_throws")]
    public string SavingThrows { get; set; } = string.Empty;

    [Column("armor_proficiencies")]
    public string ArmorProficiencies { get; set; } = string.Empty;

    [Column("weapon_proficiencies")]
    public string WeaponProficiencies { get; set; } = string.Empty;

    [Column("skill_choices")]
    public string SkillChoices { get; set; } = string.Empty;

    [Column("features")]
    public string Features { get; set; } = string.Empty;

    [Column("added_by")]
    public string? AddedBy { get; set; }

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }
}
