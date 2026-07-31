using DndCompanion.Services;
using Xunit;
using static DndCompanion.Services.HomeOnboardingLogic;

namespace DndCompanion.Tests;

// Passi del primo avvio guidato in Home (HomeOnboardingLogic.BuildSteps / IsSetupComplete).
public class HomeOnboardingLogicTests
{
    [Fact]
    public void Without_active_campaign_only_first_step_appears()
    {
        var steps = BuildSteps(hasActiveCampaign: false, isMaster: true, hasOwnCharacter: false, hasOtherMembers: false);

        var step = Assert.Single(steps);
        Assert.False(step.IsDone);
    }

    [Fact]
    public void Without_active_campaign_setup_is_not_complete()
    {
        var steps = BuildSteps(hasActiveCampaign: false, isMaster: true, hasOwnCharacter: false, hasOtherMembers: false);
        Assert.False(IsSetupComplete(steps));
    }

    [Fact]
    public void Master_gets_four_steps_including_invite()
    {
        var steps = BuildSteps(hasActiveCampaign: true, isMaster: true, hasOwnCharacter: false, hasOtherMembers: false);
        Assert.Equal(4, steps.Count);
        Assert.Contains(steps, s => s.Title.Contains("Invita"));
    }

    [Fact]
    public void Player_gets_three_steps_without_invite()
    {
        var steps = BuildSteps(hasActiveCampaign: true, isMaster: false, hasOwnCharacter: false, hasOtherMembers: false);
        Assert.Equal(3, steps.Count);
        Assert.DoesNotContain(steps, s => s.Title.Contains("Invita"));
    }

    [Fact]
    public void First_step_is_done_whenever_a_campaign_is_active()
    {
        var steps = BuildSteps(hasActiveCampaign: true, isMaster: false, hasOwnCharacter: false, hasOtherMembers: false);
        Assert.True(steps[0].IsDone);
    }

    [Fact]
    public void Character_step_reflects_ownership_and_points_to_the_wizard_route()
    {
        var notDone = BuildSteps(hasActiveCampaign: true, isMaster: false, hasOwnCharacter: false, hasOtherMembers: false);
        var done = BuildSteps(hasActiveCampaign: true, isMaster: false, hasOwnCharacter: true, hasOtherMembers: false);

        Assert.False(notDone[1].IsDone);
        Assert.Equal("characters/nuovo", notDone[1].Route);
        Assert.Equal(StepAction.Navigate, notDone[1].Action);
        Assert.True(done[1].IsDone);
    }

    [Fact]
    public void Invite_step_reflects_other_members_and_has_no_route()
    {
        var steps = BuildSteps(hasActiveCampaign: true, isMaster: true, hasOwnCharacter: true, hasOtherMembers: true);
        var invite = steps[2];

        Assert.True(invite.IsDone);
        Assert.Null(invite.Route);
        Assert.Equal(StepAction.CopyInviteCode, invite.Action);
    }

    [Fact]
    public void Last_step_points_to_combat_and_is_never_marked_done()
    {
        var masterSteps = BuildSteps(hasActiveCampaign: true, isMaster: true, hasOwnCharacter: true, hasOtherMembers: true);
        var playerSteps = BuildSteps(hasActiveCampaign: true, isMaster: false, hasOwnCharacter: true, hasOtherMembers: false);

        Assert.Equal("combat", masterSteps[^1].Route);
        Assert.False(masterSteps[^1].IsDone);
        Assert.Equal("combat", playerSteps[^1].Route);
        Assert.False(playerSteps[^1].IsDone);
    }

    [Fact]
    public void Master_wording_differs_from_player_wording_on_last_step()
    {
        var masterSteps = BuildSteps(hasActiveCampaign: true, isMaster: true, hasOwnCharacter: true, hasOtherMembers: true);
        var playerSteps = BuildSteps(hasActiveCampaign: true, isMaster: false, hasOwnCharacter: true, hasOtherMembers: false);

        Assert.NotEqual(masterSteps[^1].Title, playerSteps[^1].Title);
    }

    [Fact]
    public void Setup_not_complete_until_character_created()
    {
        var steps = BuildSteps(hasActiveCampaign: true, isMaster: false, hasOwnCharacter: false, hasOtherMembers: false);
        Assert.False(IsSetupComplete(steps));
    }

    [Fact]
    public void Player_setup_complete_as_soon_as_character_exists_no_invite_needed()
    {
        var steps = BuildSteps(hasActiveCampaign: true, isMaster: false, hasOwnCharacter: true, hasOtherMembers: false);
        Assert.True(IsSetupComplete(steps));
    }

    [Fact]
    public void Master_setup_not_complete_until_someone_else_joined_even_with_a_character()
    {
        var steps = BuildSteps(hasActiveCampaign: true, isMaster: true, hasOwnCharacter: true, hasOtherMembers: false);
        Assert.False(IsSetupComplete(steps));
    }

    [Fact]
    public void Master_setup_complete_once_character_created_and_someone_joined()
    {
        var steps = BuildSteps(hasActiveCampaign: true, isMaster: true, hasOwnCharacter: true, hasOtherMembers: true);
        Assert.True(IsSetupComplete(steps));
    }

    [Fact]
    public void Combat_step_being_undone_never_blocks_completion()
    {
        // Il passo finale non è tracciabile (v. commento su BuildSteps): deve restare fuori dal
        // conto di IsSetupComplete, altrimenti il percorso guidato non sparirebbe mai.
        var steps = BuildSteps(hasActiveCampaign: true, isMaster: false, hasOwnCharacter: true, hasOtherMembers: false);
        Assert.False(steps[^1].IsDone);
        Assert.True(IsSetupComplete(steps));
    }
}
