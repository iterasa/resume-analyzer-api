using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Iterasa.ResumeAnalyzer.IntegrationTests;

/// <summary>Vacantes: alta, edición de requisitos, ciclo de vida y lectura.</summary>
public sealed class VacancyEndpointTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory> {

    private static readonly string ClosesOn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)).ToString("yyyy-MM-dd");

    /// <summary>Cuerpo de alta válido.</summary>
    public static object NewVacancy(Guid ownerId, string title = "Backend .NET") => new {
        title,
        modality = "Remote",
        ownerId,
        closesOn = ClosesOn,
        requirements = new object[] {
            new { text = "C# y .NET 8 o superior", kind = "Mandatory" },
            new { text = "Inglés intermedio", kind = "Desirable" },
        },
    };

    [Fact]
    public async Task Creating_A_Vacancy_Returns_Its_Detail_With_Default_Weights() {

        var admin = await factory.RegisterAsync();

        var response = await admin.Browser.PostAsJsonAsync("/api/v1/vacancies", NewVacancy(admin.UserId));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location!.AbsolutePath.Should().StartWith("/api/v1/vacancies/");

        var vacancy = await response.Content.ReadFromJsonAsync<JsonElement>();
        vacancy.GetProperty("status").GetString().Should().Be("Open");
        vacancy.GetProperty("closesOn").GetString().Should().Be(ClosesOn);
        vacancy.GetProperty("owner").GetProperty("name").GetString().Should().Be("Ana Admin");
        vacancy.GetProperty("area").GetString().Should().Be("General");
        vacancy.GetProperty("priority").GetString().Should().Be("Medium");
        vacancy.GetProperty("openDays").GetInt32().Should().Be(0);
        vacancy.GetProperty("retentionDays").GetInt32().Should().Be(90);
        vacancy.GetProperty("deletesOn").GetString().Should().Be(DateOnly.Parse(ClosesOn).AddDays(90).ToString("yyyy-MM-dd"));
        vacancy.GetProperty("sources").GetProperty("whatsapp").GetInt32().Should().Be(0);
        vacancy.GetProperty("whatsappLink").ValueKind.Should().Be(JsonValueKind.Null);
        vacancy.GetProperty("applyUrl").ValueKind.Should().Be(JsonValueKind.Null, "APP_PUBLIC_URL is not configured in tests");

        var requirements = vacancy.GetProperty("requirements").EnumerateArray().ToList();
        requirements.Select(r => r.GetProperty("weight").GetInt32()).Should().Equal(4, 2);
        requirements.Should().AllSatisfy(r => r.GetProperty("coverage").ValueKind.Should().Be(JsonValueKind.Null));

        var listed = await admin.Browser.GetFromJsonAsync<JsonElement>("/api/v1/vacancies?status=Open");
        listed.GetProperty("items").EnumerateArray().Select(v => v.GetProperty("id").GetString()).Should().Contain(vacancy.GetProperty("id").GetString());
        listed.GetProperty("totalCount").GetInt32().Should().Be(1);

    }

    [Fact]
    public async Task Editing_Requirements_Keeps_Ids_Changes_Weights_And_Removes_The_Missing_Ones() {

        var admin = await factory.RegisterAsync();
        var vacancy = await CreateAsync(admin);
        var id = vacancy.GetProperty("id").GetString();
        var original = vacancy.GetProperty("requirements").EnumerateArray().Select(r => r.GetProperty("id").GetString()).ToList();

        var response = await admin.Browser.PutAsJsonAsync($"/api/v1/vacancies/{id}/requirements", new {
            requirements = new object[] {
                new { id = original[0], text = "C# y .NET 9", kind = "Mandatory", weight = 5 },
                new { id = (string?)null, text = "PostgreSQL", kind = "Desirable", weight = 3 },
            },
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var requirements = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("requirements").EnumerateArray().ToList();

        requirements.Should().HaveCount(2);
        requirements[0].GetProperty("id").GetString().Should().Be(original[0], "a requirement keeps its id when its text changes");
        requirements[0].GetProperty("text").GetString().Should().Be("C# y .NET 9");
        requirements[0].GetProperty("weight").GetInt32().Should().Be(5);
        requirements[1].GetProperty("id").GetString().Should().NotBe(original[1]);
        requirements.Select(r => r.GetProperty("id").GetString()).Should().NotContain(original[1]);

    }

    [Fact]
    public async Task A_Requirement_From_Another_Vacancy_Is_Rejected() {

        var admin = await factory.RegisterAsync();
        var first = await CreateAsync(admin);
        var second = await CreateAsync(admin);
        var foreign = second.GetProperty("requirements")[0].GetProperty("id").GetString();

        var response = await admin.Browser.PutAsJsonAsync($"/api/v1/vacancies/{first.GetProperty("id").GetString()}/requirements", new {
            requirements = new object[] { new { id = foreign, text = "Robado", kind = "Mandatory", weight = 3 } },
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

    }

    [Fact]
    public async Task The_Life_Cycle_Follows_Open_Paused_Closed_And_Reopened() {

        var admin = await factory.RegisterAsync();
        var id = (await CreateAsync(admin)).GetProperty("id").GetString();

        (await StatusAfterAsync(admin.Browser.PostAsync($"/api/v1/vacancies/{id}/pause", null))).Should().Be("Paused");
        (await admin.Browser.PostAsync($"/api/v1/vacancies/{id}/pause", null)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await StatusAfterAsync(admin.Browser.PostAsync($"/api/v1/vacancies/{id}/resume", null))).Should().Be("Open");

        var closed = await admin.Browser.PostAsJsonAsync($"/api/v1/vacancies/{id}/close", new { reason = "Cancelled", notifyCandidates = false });
        closed.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await closed.Content.ReadFromJsonAsync<JsonElement>();
        detail.GetProperty("status").GetString().Should().Be("Closed");
        detail.GetProperty("closeReason").GetString().Should().Be("Cancelled");
        detail.GetProperty("deletesOn").GetString().Should().Be(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(90).ToString("yyyy-MM-dd"), "retention counts from the close");

        (await admin.Browser.PostAsJsonAsync($"/api/v1/vacancies/{id}/close", new { reason = "Filled", notifyCandidates = false })).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await admin.Browser.PostAsync($"/api/v1/vacancies/{id}/resume", null)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await StatusAfterAsync(admin.Browser.PostAsync($"/api/v1/vacancies/{id}/reopen", null))).Should().Be("Open");

    }

    [Fact]
    public async Task The_Owner_Must_Be_An_Active_Member_Of_The_Company() {

        var admin = await factory.RegisterAsync();
        var outsider = await factory.RegisterAsync();

        var response = await admin.Browser.PostAsJsonAsync("/api/v1/vacancies", NewVacancy(outsider.UserId));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var problem = await ProblemDetailsTests.ReadProblemAsync(response);
        problem.RootElement.GetProperty("errors").TryGetProperty("ownerId", out _).Should().BeTrue();

    }

    [Fact]
    public async Task Owner_Options_List_The_Team() {

        var admin = await factory.RegisterAsync();

        var options = await admin.Browser.GetFromJsonAsync<JsonElement>("/api/v1/vacancies/owner-options");

        options.EnumerateArray().Should().ContainSingle()
            .Which.GetProperty("id").GetString().Should().Be(admin.UserId.ToString());

    }

    [Fact]
    public async Task A_Vacancy_Needs_At_Least_One_Requirement() {

        var admin = await factory.RegisterAsync();

        var response = await admin.Browser.PostAsJsonAsync("/api/v1/vacancies", new {
            title = "Sin requisitos",
            modality = "OnSite",
            ownerId = admin.UserId,
            closesOn = ClosesOn,
            requirements = Array.Empty<object>(),
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

    }

    internal static async Task<JsonElement> CreateAsync(CustomWebApplicationFactory.SignedIn admin, string title = "Backend .NET") {
        var response = await admin.Browser.PostAsJsonAsync("/api/v1/vacancies", NewVacancy(admin.UserId, title));
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<string?> StatusAfterAsync(Task<HttpResponseMessage> request) {
        var response = await request;
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString();
    }

}