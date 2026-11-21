using FluentAssertions;
using Iterasa.ResumeAnalyzer.Domain.Tenants;
using Iterasa.ResumeAnalyzer.Shared;
using Iterasa.ResumeAnalyzer.Shared.Tenancy;
using Xunit;

namespace Iterasa.ResumeAnalyzer.UnitTests.Tenants;

public sealed class TenantTests {

    private static readonly DateTimeOffset Now = new(2026, 11, 21, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_New_Company_Starts_In_Trial_With_Anonymous_Review_And_Default_Retention() {
        var tenant = Tenant.Create(Guid.NewGuid(), "  Altamira  ", "Panamá", Guid.Empty, Now);
        tenant.Name.Should().Be("Altamira");
        tenant.PlanStatus.Should().Be(PlanStatus.Trial);
        tenant.TrialEndsAtUtc.Should().Be(Now.AddDays(Tenant.TrialDays));
        tenant.AnonymousReviewByDefault.Should().BeTrue();
        tenant.RetentionDays.Should().Be(90);
    }

    [Theory]
    [InlineData("México")]
    [InlineData("")]
    [InlineData("guatemala")]
    public void Only_Supported_Countries_Are_Accepted(string country) {
        var act = () => Tenant.Create(Guid.NewGuid(), "Acme", country, Guid.Empty, Now);
        act.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData(MemberRole.HiringManager)]
    [InlineData(MemberRole.ReadOnly)]
    public void A_Role_That_Cannot_Reveal_Always_Reviews_Anonymously(MemberRole role) {
        Membership.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), role, anonymousReview: false, Guid.Empty, Now)
            .AnonymousReview.Should().BeTrue();
    }

    [Fact]
    public void Roles_Grant_The_Catalogue_The_Web_Expects() {
        RolePermissions.Of(MemberRole.Admin).Should().Equal(Permissions.All);
        RolePermissions.Of(MemberRole.Recruiter).Should().Contain(Permissions.VacanciesManage).And.NotContain(Permissions.TeamManage);
        RolePermissions.Of(MemberRole.HiringManager).Should().Equal(Permissions.ApplicationsViewShortlist, Permissions.ApplicationsMove, Permissions.NotesWrite);
        RolePermissions.Of(MemberRole.ReadOnly).Should().Equal(Permissions.ApplicationsViewShortlist);
    }

}