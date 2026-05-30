using Postgrest.Attributes;
using Postgrest.Models;

namespace DndCompanion.Models;

[Table("profiles")]
public class Profile : BaseModel
{
    // shouldInsert: true → l'id (= auth.users.id) DEVE essere inviato in insert,
    // non è generato dal DB (a differenza delle altre tabelle con PK uuid auto).
    [PrimaryKey("id", true)]
    public string Id { get; set; } = string.Empty;

    [Column("display_name")]
    public string? DisplayName { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}
