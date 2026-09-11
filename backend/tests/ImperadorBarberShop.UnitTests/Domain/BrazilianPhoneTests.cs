using FluentAssertions;
using ImperadorBarberShop.Domain.ValueObjects;

namespace ImperadorBarberShop.UnitTests.Domain;

public class BrazilianPhoneTests
{
    [Theory]
    // Com e sem o 55 do país
    [InlineData("+5511999990000")]
    [InlineData("5511999990000")]
    [InlineData("11999990000")]
    // Máscaras
    [InlineData("11 9 9999-0000")]
    [InlineData("(11) 99999-0000")]
    [InlineData("+55 (11) 99999-0000")]
    [InlineData("+55 11 99999.0000")]
    // Sem o nono dígito
    [InlineData("11 9999-0000")]
    [InlineData("+55 11 9999-0000")]
    [InlineData("551199990000")]
    // Zero de discagem, antes ou depois do 55, e prefixo internacional
    [InlineData("011 99999-0000")]
    [InlineData("0 11 9999-0000")]
    [InlineData("+55 (011) 99999-0000")]
    [InlineData("0055 11 99999-0000")]
    public void Parse_AnyWayOfTypingTheSameMobile_GivesOneCanonicalFormAndKey(string input)
    {
        var phone = BrazilianPhone.Parse(input);

        phone.Canonical.Should().Be("+5511999990000");
        phone.MatchKey.Should().Be("1199990000");
    }

    [Fact]
    public void Parse_TheThreeFormatsFromTheBrief_ResolveToTheSameClient()
    {
        var keys = new[] { "+5511999990000", "11 9 9999-0000", "11 9999-0000" }
            .Select(p => BrazilianPhone.Parse(p).MatchKey);

        keys.Distinct().Should().ContainSingle().Which.Should().Be("1199990000");
    }

    [Theory]
    // DDD 55 (RS) não é confundido com o código do país
    [InlineData("55 99999-0000", "+5555999990000", "5599990000")]
    [InlineData("+55 55 99999-0000", "+5555999990000", "5599990000")]
    [InlineData("55 9999-0000", "+5555999990000", "5599990000")]
    // Celular antigo de 8 dígitos começando em 6–9 ganha o 9
    [InlineData("21 8888-7777", "+5521988887777", "2188887777")]
    [InlineData("31 6123-4567", "+5531961234567", "3161234567")]
    // Com 11 dígitos o número é mantido como veio
    [InlineData("11 61234-5678", "+5511612345678", "1112345678")]
    public void Parse_OtherValidNumbers(string input, string canonical, string matchKey)
    {
        var phone = BrazilianPhone.Parse(input);

        phone.Canonical.Should().Be(canonical);
        phone.MatchKey.Should().Be(matchKey);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("telefone")]
    [InlineData("99999-0000")]          // sem DDD
    [InlineData("11 99999-00")]         // curto
    [InlineData("119999900001")]        // 12 dígitos sem o 55 na frente
    [InlineData("+1 555 123 4567")]     // estrangeiro
    [InlineData("10 99999-0000")]       // DDD inexistente
    [InlineData("11 3333-4444")]        // fixo: ganhar um 9 viraria o número de outra pessoa
    public void TryParse_UnparseableInput_IsRejected(string? input)
    {
        BrazilianPhone.TryParse(input, out var phone).Should().BeFalse();
        phone.Should().BeNull();
    }

    [Fact]
    public void Parse_UnparseableInput_ThrowsWithTheUserFacingMessage()
    {
        var act = () => BrazilianPhone.Parse("123");

        act.Should().Throw<FormatException>().WithMessage(BrazilianPhone.InvalidMessage);
    }
}
