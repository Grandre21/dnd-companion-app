using DndCompanion.Services.Repositories;
using Xunit;

namespace DndCompanion.Tests;

// Generazione del codice invito (CampaignRepository.GenerateInviteCode):
// "DND-" + 8 caratteri da un alfabeto senza simboli ambigui (no 0/O/1/I/L).
public class CampaignRepositoryTests
{
    [Fact]
    public void GenerateInviteCode_has_expected_prefix_and_length()
    {
        var code = CampaignRepository.GenerateInviteCode();

        Assert.StartsWith("DND-", code);
        Assert.Equal(12, code.Length); // "DND-" (4) + 8 caratteri variabili
    }

    [Fact]
    public void GenerateInviteCode_variable_part_uses_only_allowed_alphabet()
    {
        // Su molte iterazioni: il rejection sampling non deve mai produrre caratteri fuori alfabeto.
        for (var i = 0; i < 500; i++)
        {
            var variable = CampaignRepository.GenerateInviteCode()["DND-".Length..];

            Assert.Equal(8, variable.Length);
            Assert.All(variable, c =>
                Assert.True(CampaignRepository.InviteCodeAlphabet.Contains(c),
                    $"carattere '{c}' fuori dall'alfabeto consentito"));
        }
    }

    [Fact]
    public void InviteCodeAlphabet_excludes_ambiguous_characters()
    {
        foreach (var ambiguous in "0O1IL")
        {
            Assert.False(CampaignRepository.InviteCodeAlphabet.Contains(ambiguous),
                $"l'alfabeto non dovrebbe contenere il carattere ambiguo '{ambiguous}'");
        }
    }
}
