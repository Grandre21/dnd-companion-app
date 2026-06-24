using DndCompanion.Services;
using Xunit;

namespace DndCompanion.Tests;

// Regola di autorizzazione lato client (AccessControl.CanEdit): master OPPURE proprietario.
public class AccessControlTests
{
    [Fact]
    public void Master_can_edit_any_resource()
        => Assert.True(AccessControl.CanEdit(isMaster: true, ownerId: "someone-else", currentUserId: "me"));

    [Fact]
    public void Owner_can_edit_own_resource()
        => Assert.True(AccessControl.CanEdit(isMaster: false, ownerId: "me", currentUserId: "me"));

    [Fact]
    public void Non_master_non_owner_cannot_edit()
        => Assert.False(AccessControl.CanEdit(isMaster: false, ownerId: "someone-else", currentUserId: "me"));

    [Fact]
    public void Master_can_edit_even_when_user_unknown()
        => Assert.True(AccessControl.CanEdit(isMaster: true, ownerId: "x", currentUserId: null));

    [Fact]
    public void Non_master_with_null_user_cannot_edit_owned_resource()
        => Assert.False(AccessControl.CanEdit(isMaster: false, ownerId: "x", currentUserId: null));

    [Fact]
    public void Non_master_with_empty_owner_cannot_edit()
        => Assert.False(AccessControl.CanEdit(isMaster: false, ownerId: "", currentUserId: "me"));
}
