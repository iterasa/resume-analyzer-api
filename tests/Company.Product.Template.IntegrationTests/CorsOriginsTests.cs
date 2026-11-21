using Company.Product.Template.Api.Http;
using FluentAssertions;
using Xunit;

namespace Company.Product.Template.IntegrationTests;

/// <summary>Normalización de orígenes: lo que se admite y en qué forma queda.</summary>
public sealed class CorsOriginsTests {

    [Theory]
    [InlineData("https://app.example.com", "https://app.example.com")]
    [InlineData("  HTTPS://App.Example.COM/  ", "https://app.example.com")]
    [InlineData("https://app.example.com:443", "https://app.example.com")]
    [InlineData("https://app.example.com:8443", "https://app.example.com:8443")]
    [InlineData("http://localhost:5173", "http://localhost:5173")]
    [InlineData("http://127.0.0.1:3000", "http://127.0.0.1:3000")]
    [InlineData("https://*.example.com", "https://*.example.com")]
    [InlineData("HTTPS://*.Tenant.Example.com:8443/", "https://*.tenant.example.com:8443")]
    public void Valid_Origins_Should_Be_Normalized(string raw, string expected) {
        CorsOrigins.TryNormalize(raw, out var normalized).Should().BeTrue();
        normalized.Should().Be(expected);
    }

    [Theory]
    [InlineData("*")]
    [InlineData("null")]
    [InlineData("")]
    [InlineData("app.example.com")]
    [InlineData("ftp://app.example.com")]
    [InlineData("https://app.example.com/api")]
    [InlineData("https://app.example.com?x=1")]
    [InlineData("https://user:pass@app.example.com")]
    [InlineData("https://*.com")]
    [InlineData("https://*")]
    [InlineData("https://app.*.example.com")]
    public void Invalid_Origins_Should_Be_Rejected(string raw) {
        CorsOrigins.TryNormalize(raw, out _).Should().BeFalse();
    }

}