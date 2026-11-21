using FluentAssertions;
using Iterasa.ResumeAnalyzer.Application.Abstractions;
using Iterasa.ResumeAnalyzer.Domain.Tenants;
using Iterasa.ResumeAnalyzer.Domain.Users;
using Iterasa.ResumeAnalyzer.Infrastructure.Persistence;
using Iterasa.ResumeAnalyzer.Infrastructure.Security.FieldEncryption;
using Iterasa.ResumeAnalyzer.Shared.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Iterasa.ResumeAnalyzer.IntegrationTests;

/// <summary>
/// Aislamiento entre empresas (ADR 0018) y permisos por rol: lo de una empresa no existe para
/// otra, y cada rol hace solo lo que su catálogo permite.
/// </summary>
public sealed class TenantIsolationTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory> {

    [Fact]
    public async Task A_Company_Cannot_See_Or_Change_Another_Companys_Vacancies() {

        var acme = await factory.RegisterAsync(companyName: "Acme");
        var globex = await factory.RegisterAsync(companyName: "Globex");
        var id = (await VacancyEndpointTests.CreateAsync(acme)).GetProperty("id").GetString();

        (await globex.Browser.GetAsync($"/api/v1/vacancies/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await globex.Browser.PostAsync($"/api/v1/vacancies/{id}/pause", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await globex.Browser.PutAsJsonAsync($"/api/v1/vacancies/{id}", VacancyEndpointTests.NewVacancy(globex.UserId))).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var listed = await globex.Browser.GetFromJsonAsync<JsonElement>("/api/v1/vacancies");
        listed.GetProperty("totalCount").GetInt32().Should().Be(0);

        (await acme.Browser.GetFromJsonAsync<JsonElement>($"/api/v1/vacancies/{id}")).GetProperty("status").GetString().Should().Be("Open");

    }

    [Fact]
    public async Task Without_A_Tenant_Scope_No_Tenant_Rows_Are_Visible_And_None_Can_Be_Written() {

        var acme = await factory.RegisterAsync();
        await VacancyEndpointTests.CreateAsync(acme);

        await factory.InScopeAsync(async services => {

            var context = services.GetRequiredService<AppDbContext>();

            (await context.Vacancies.CountAsync()).Should().Be(0, "outside a tenant scope the filter shows nothing");
            (await context.Vacancies.IgnoreQueryFilters().CountAsync()).Should().BeGreaterThan(0);

            context.Memberships.Add(Membership.Create(Guid.NewGuid(), acme.TenantId, Guid.NewGuid(), MemberRole.Admin, false, Guid.Empty, DateTimeOffset.UtcNow));
            var write = () => context.SaveChangesAsync();
            await write.Should().ThrowAsync<InvalidOperationException>().WithMessage("*no tenant*");

        });

    }

    [Fact]
    public async Task A_Read_Only_Member_Can_Read_Vacancies_But_Not_Manage_Them() {

        var admin = await factory.RegisterAsync();
        var id = (await VacancyEndpointTests.CreateAsync(admin)).GetProperty("id").GetString();
        var reader = await AddMemberAsync(admin.TenantId, MemberRole.ReadOnly);

        (await reader.GetAsync($"/api/v1/vacancies/{id}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await reader.GetAsync("/api/v1/vacancies")).StatusCode.Should().Be(HttpStatusCode.OK);

        (await reader.PostAsJsonAsync("/api/v1/vacancies", VacancyEndpointTests.NewVacancy(admin.UserId))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await reader.PostAsync($"/api/v1/vacancies/{id}/pause", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await reader.GetAsync("/api/v1/vacancies/owner-options")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var session = await reader.GetFromJsonAsync<JsonElement>("/api/v1/me");
        session.GetProperty("role").GetString().Should().Be("ReadOnly");
        session.GetProperty("anonymousReview").GetBoolean().Should().BeTrue("a role that cannot reveal always reviews anonymously");
        session.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()).Should().Equal("applications.viewShortlist");

    }

    /// <summary>Siembra un miembro con contraseña (las invitaciones llegan con Equipo) e inicia su sesión.</summary>
    private async Task<HttpClient> AddMemberAsync(Guid tenantId, MemberRole role) {

        var email = $"member-{Guid.NewGuid():N}@example.test";

        await factory.InScopeAsync(async services => {
            var hasher = services.GetRequiredService<IPasswordHasher>();
            var context = services.GetRequiredService<AppDbContext>();
            var now = DateTimeOffset.UtcNow;
            var user = User.Create(new UserCreationArgs(Guid.NewGuid(), "Rita Lectora", email, hasher.Hash(CustomWebApplicationFactory.Password), Guid.Empty), now);
            context.Users.Add(user);
            using var _ = TenantCryptoScope.Enter(tenantId);
            context.Memberships.Add(Membership.Create(Guid.NewGuid(), tenantId, user.Id, role, anonymousReview: false, Guid.Empty, now));
            await context.SaveChangesAsync();
        });

        var browser = factory.CreateBrowser();
        var response = await browser.PostAsJsonAsync("/api/v1/auth/login", new { email, password = CustomWebApplicationFactory.Password, rememberMe = false });
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return browser;

    }

}