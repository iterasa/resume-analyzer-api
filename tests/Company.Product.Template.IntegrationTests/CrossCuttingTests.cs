using FluentAssertions;
using Xunit;

namespace Company.Product.Template.IntegrationTests;

/// <summary>
/// Infraestructura transversal: correlación.
/// </summary>
public sealed class CrossCuttingTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory> {

    [Fact]
    public async Task Responses_Should_Echo_The_Correlation_Id_Sent_By_The_Client() {

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Correlation-Id", "test-correlation-42");

        var response = await client.GetAsync("/health");

        response.Headers.GetValues("X-Correlation-Id").Should().ContainSingle().Which.Should().Be("test-correlation-42");

    }

    [Fact]
    public async Task Responses_Should_Generate_A_Correlation_Id_When_None_Is_Sent() {

        var response = await factory.CreateClient().GetAsync("/health");

        response.Headers.GetValues("X-Correlation-Id").Should().ContainSingle().Which.Should().NotBeNullOrWhiteSpace();

    }

}