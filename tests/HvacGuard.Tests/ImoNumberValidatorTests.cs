using HvacGuard.Domain.Common;

namespace HvacGuard.Tests;

public class ImoNumberValidatorTests
{
    [Theory]
    [InlineData("9074729")]
    [InlineData("IMO 9074729")]
    [InlineData("imo9074729")]
    [InlineData(" 9345128 ")]
    public void Valid_numbers_pass(string value) => Assert.True(ImoNumberValidator.IsValid(value));

    [Theory]
    [InlineData("9074728")]   // wrong check digit
    [InlineData("907472")]    // too short
    [InlineData("90747290")]  // too long
    [InlineData("90A4729")]   // not numeric
    [InlineData("")]
    [InlineData(null)]
    public void Invalid_numbers_fail(string? value) => Assert.False(ImoNumberValidator.IsValid(value));

    [Fact]
    public void Normalize_strips_prefix_and_whitespace() =>
        Assert.Equal("9074729", ImoNumberValidator.Normalize("IMO 907 4729"));
}
