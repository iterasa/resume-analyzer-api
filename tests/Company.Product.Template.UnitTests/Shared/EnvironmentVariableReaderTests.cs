using Company.Product.Template.Shared.Configuration;
using FluentAssertions;
using Xunit;

namespace Company.Product.Template.UnitTests.Shared;

/// <remarks>
/// Cada prueba usa una variable propia: el entorno es global al proceso y no debe tocar
/// las que lee la aplicación.
/// </remarks>
public sealed class EnvironmentVariableReaderTests {

    [Theory]
    [InlineData("true", true)]
    [InlineData(" ON ", true)]
    [InlineData("0", false)]
    [InlineData("no", false)]
    public void GetBool_Should_Accept_The_Documented_Values(string raw, bool expected) {
        var name = $"TEMPLATE_TEST_BOOL_{Guid.NewGuid():N}";
        Environment.SetEnvironmentVariable(name, raw);
        try {
            EnvironmentVariableReader.GetBool(name, !expected).Should().Be(expected);
        } finally {
            Environment.SetEnvironmentVariable(name, null);
        }
    }

    [Fact]
    public void GetBool_Should_Use_The_Default_When_Unset() {
        EnvironmentVariableReader.GetBool($"TEMPLATE_TEST_BOOL_{Guid.NewGuid():N}", true).Should().BeTrue();
    }

    [Theory]
    [InlineData("\"false\"")]
    [InlineData("flase")]
    [InlineData("disabled")]
    public void GetBool_Should_Reject_An_Unrecognized_Value(string raw) {
        var name = $"TEMPLATE_TEST_BOOL_{Guid.NewGuid():N}";
        Environment.SetEnvironmentVariable(name, raw);
        try {
            var act = () => EnvironmentVariableReader.GetBool(name, true);
            act.Should().Throw<InvalidOperationException>().WithMessage($"*{name}*");
        } finally {
            Environment.SetEnvironmentVariable(name, null);
        }
    }

}