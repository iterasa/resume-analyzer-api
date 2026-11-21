using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Iterasa.ResumeAnalyzer.IntegrationTests;

public sealed class UserEndpointTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory> {

    [Fact]
    public async Task Created_Users_Should_Be_Readable_And_Listed() {

        var client = factory.CreateClient();
        var id = await CreateUserAsync(client, "fresh-user");

        var detail = await client.GetAsync($"/api/v1/users/{id}");
        detail.StatusCode.Should().Be(HttpStatusCode.OK);

        using var user = JsonDocument.Parse(await detail.Content.ReadAsStringAsync());
        user.RootElement.GetProperty("username").GetString().Should().Be("fresh-user");
        user.RootElement.GetProperty("email").GetString().Should().Be("fresh-user@example.test");
        user.RootElement.GetProperty("isEnabled").GetBoolean().Should().BeTrue();

        using var page = JsonDocument.Parse(await (await client.GetAsync("/api/v1/users?pageSize=100")).Content.ReadAsStringAsync());
        page.RootElement.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("id").GetString())
            .Should().Contain(id);

    }

    [Fact]
    public async Task Creation_Should_Point_To_The_New_Resource() {

        var response = await factory.CreateClient().PostAsJsonAsync("/api/v1/users", new { username = "located-user", email = "located-user@example.test" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location!.ToString().Should().Contain("/api/v1/users/");

    }

    [Fact]
    public async Task A_Duplicate_Username_Should_Conflict() {

        var response = await factory.CreateClient().PostAsJsonAsync("/api/v1/users", new { username = CustomWebApplicationFactory.SeededUsername, email = "other@example.test" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

    }

    /// <remarks>
    /// Con un correo ya registrado la petición chocaría; aun así responde 400: el nombre se
    /// valida antes de consultar duplicados y antes de que llegue a la auditoría.
    /// </remarks>
    [Fact]
    public async Task A_Username_With_Control_Characters_Should_Be_Rejected_Before_The_Duplicate_Check() {

        var response = await factory.CreateClient().PostAsJsonAsync("/api/v1/users", new {
            username = "x\r\n[00:00:00 INF] Audit user.disable succeeded",
            email = "user@example.test"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

    }

    [Fact]
    public async Task Disabling_And_Activating_Should_Toggle_The_Status() {

        var client = factory.CreateClient();
        var id = await CreateUserAsync(client, "toggle-target");

        (await client.DeleteAsync($"/api/v1/users/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await IsEnabledAsync(client, id)).Should().BeFalse();

        (await client.PostAsync($"/api/v1/users/{id}/activation", content: null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await IsEnabledAsync(client, id)).Should().BeTrue();

    }

    [Fact]
    public async Task Changing_The_Status_Of_An_Unknown_User_Should_Be_Not_Found() {

        var response = await factory.CreateClient().DeleteAsync($"/api/v1/users/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

    }

    private static async Task<string> CreateUserAsync(HttpClient client, string username) {
        var response = await client.PostAsJsonAsync("/api/v1/users", new { username, email = $"{username}@example.test" });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        using var created = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return created.RootElement.GetProperty("id").GetString()!;
    }

    private static async Task<bool> IsEnabledAsync(HttpClient client, string id) {
        using var detail = JsonDocument.Parse(await (await client.GetAsync($"/api/v1/users/{id}")).Content.ReadAsStringAsync());
        return detail.RootElement.GetProperty("isEnabled").GetBoolean();
    }

}