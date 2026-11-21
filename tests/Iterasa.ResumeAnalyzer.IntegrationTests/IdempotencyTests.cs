using FluentAssertions;
using Iterasa.ResumeAnalyzer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Iterasa.ResumeAnalyzer.IntegrationTests;

/// <summary>
/// <c>Idempotency-Key</c> (ADR 0007): un reintento recibe la misma respuesta sin repetir la
/// operación; la clave se acota a la empresa y al usuario.
/// </summary>
public sealed class IdempotencyTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory> {

    [Fact]
    public async Task A_Retry_With_The_Same_Key_Replays_The_First_Response_Without_Creating_Twice() {

        var admin = await factory.RegisterAsync();
        var key = Guid.NewGuid().ToString();

        var first = await SendAsync(admin, key, VacancyEndpointTests.NewVacancy(admin.UserId));
        var retry = await SendAsync(admin, key, VacancyEndpointTests.NewVacancy(admin.UserId));

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        retry.StatusCode.Should().Be(HttpStatusCode.Created);
        retry.Headers.GetValues("Idempotency-Replayed").Should().ContainSingle().Which.Should().Be("true");
        retry.Headers.Location.Should().Be(first.Headers.Location);
        (await retry.Content.ReadAsStringAsync()).Should().Be(await first.Content.ReadAsStringAsync());

        (await admin.Browser.GetFromJsonAsync<JsonElement>("/api/v1/vacancies")).GetProperty("totalCount").GetInt32().Should().Be(1);

        // La respuesta guardada lleva el nombre del responsable: se guarda cifrada.
        await factory.InScopeAsync(async services => {
            var stored = await services.GetRequiredService<AppDbContext>().Database
                .SqlQueryRaw<string>("SELECT response_body AS \"Value\" FROM idempotency_key").ToListAsync();
            stored.Should().NotBeEmpty().And.AllSatisfy(body => body.Should().StartWith("enc1.").And.NotContain("Ana"));
        });

    }

    [Fact]
    public async Task The_Same_Key_With_Another_Body_Is_Rejected() {

        var admin = await factory.RegisterAsync();
        var key = Guid.NewGuid().ToString();

        await SendAsync(admin, key, VacancyEndpointTests.NewVacancy(admin.UserId, "Primera"));
        var reused = await SendAsync(admin, key, VacancyEndpointTests.NewVacancy(admin.UserId, "Segunda"));

        reused.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using var problem = await ProblemDetailsTests.ReadProblemAsync(reused);
        problem.RootElement.GetProperty("type").GetString().Should().Be("urn:problem-type:idempotency_key_reused");

    }

    [Fact]
    public async Task Keys_Do_Not_Cross_Companies() {

        var acme = await factory.RegisterAsync();
        var globex = await factory.RegisterAsync();
        var key = Guid.NewGuid().ToString();

        var fromAcme = await SendAsync(acme, key, VacancyEndpointTests.NewVacancy(acme.UserId));
        var fromGlobex = await SendAsync(globex, key, VacancyEndpointTests.NewVacancy(globex.UserId));

        fromGlobex.StatusCode.Should().Be(HttpStatusCode.Created);
        fromGlobex.Headers.Contains("Idempotency-Replayed").Should().BeFalse();
        fromGlobex.Headers.Location.Should().NotBe(fromAcme.Headers.Location);

    }

    private static async Task<HttpResponseMessage> SendAsync(CustomWebApplicationFactory.SignedIn admin, string key, object body) {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/vacancies") { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", key);
        return await admin.Browser.SendAsync(request);
    }

}