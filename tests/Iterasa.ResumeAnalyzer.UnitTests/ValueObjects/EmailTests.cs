using FluentAssertions;
using Iterasa.ResumeAnalyzer.Domain.ValueObjects;
using Iterasa.ResumeAnalyzer.Shared;
using Xunit;

namespace Iterasa.ResumeAnalyzer.UnitTests.ValueObjects;

public sealed class EmailTests {

    [Fact]
    public void Create_Should_Normalize_Email() {
        var email = Email.Create("  USER@Example.com ");
        email.Value.Should().Be("user@example.com");
    }

    [Fact]
    public void Create_Should_Throw_When_Invalid() {
        var act = () => Email.Create("invalid-email");
        act.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData("us\0er@example.com")]
    [InlineData("us\u001ber@example.com")]
    [InlineData("us\u200Ber@example.com")]
    [InlineData("us\u202Eer@example.com")]
    [InlineData("us\uFEFFer@example.com")]
    [InlineData("us\U000E0041er@example.com")]
    public void Create_Should_Throw_When_It_Has_Control_Or_Format_Characters(string value) {
        var act = () => Email.Create(value);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_Should_Accept_Non_Ascii_Letters() {
        Email.Create("josé@example.com").Value.Should().Be("josé@example.com");
    }

}