using FluentAssertions;
using Iterasa.ResumeAnalyzer.Domain.Vacancies;
using Iterasa.ResumeAnalyzer.Shared;
using Iterasa.ResumeAnalyzer.Shared.Vacancies;
using Xunit;

namespace Iterasa.ResumeAnalyzer.UnitTests.Vacancies;

public sealed class VacancyTests {

    private static readonly DateTimeOffset Now = new(2026, 11, 21, 18, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 11, 21);
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid OwnerId = Guid.NewGuid();

    private static VacancyDetails Details(string title = "Backend .NET", DateOnly? closesOn = null) =>
        new(title, Modality.Remote, OwnerId, closesOn ?? Today.AddDays(30));

    private static Vacancy Create(params RequirementDraft[] requirements) =>
        Vacancy.Create(new VacancyCreationArgs(
            Guid.NewGuid(), TenantId, Details(),
            requirements.Length == 0 ? [new(null, "C#", RequirementKind.Mandatory), new(null, "Inglés", RequirementKind.Desirable)] : requirements,
            "apply-token", OwnerId), Now);

    [Fact]
    public void A_New_Vacancy_Is_Open_With_Default_Weights_In_Order() {
        var vacancy = Create();
        vacancy.Status.Should().Be(VacancyStatus.Open);
        vacancy.OpenedOn.Should().Be(Today);
        vacancy.Requirements.Select(r => (r.Text, r.Weight, r.Position)).Should().Equal(("C#", 4, 0), ("Inglés", 2, 1));
        vacancy.Requirements.Should().AllSatisfy(r => r.TenantId.Should().Be(TenantId));
    }

    [Fact]
    public void A_Vacancy_Needs_Between_One_And_Thirty_Distinct_Requirements() {
        var none = () => Vacancy.Create(new VacancyCreationArgs(Guid.NewGuid(), TenantId, Details(), [], "t", OwnerId), Now);
        var many = () => Create([.. Enumerable.Range(0, 31).Select(i => new RequirementDraft(null, $"R{i}", RequirementKind.Desirable))]);
        var twice = () => Create(new RequirementDraft(null, "C#", RequirementKind.Mandatory), new RequirementDraft(null, " c# ", RequirementKind.Desirable));
        none.Should().Throw<DomainException>();
        many.Should().Throw<DomainException>();
        twice.Should().Throw<DomainException>();
    }

    [Fact]
    public void The_Planned_Close_Cannot_Be_In_The_Past_Or_More_Than_A_Year_Away() {
        var past = () => Vacancy.Create(new VacancyCreationArgs(Guid.NewGuid(), TenantId, Details(closesOn: Today.AddDays(-2)), [new(null, "C#", RequirementKind.Mandatory)], "t", OwnerId), Now);
        var far = () => Vacancy.Create(new VacancyCreationArgs(Guid.NewGuid(), TenantId, Details(closesOn: Today.AddDays(400)), [new(null, "C#", RequirementKind.Mandatory)], "t", OwnerId), Now);
        past.Should().Throw<DomainException>();
        far.Should().Throw<DomainException>();
    }

    [Fact]
    public void Editing_A_Vacancy_Whose_Planned_Close_Has_Passed_Does_Not_Require_Another_Date() {

        var vacancy = Create();
        var later = Now.AddDays(60);

        vacancy.Update(Details(title: "Backend .NET Sr"), [new(vacancy.Requirements[0].Id, "C#", RequirementKind.Mandatory)], OwnerId, later);
        vacancy.Title.Should().Be("Backend .NET Sr");

        var moveToPast = () => vacancy.Update(Details(closesOn: Today.AddDays(31)), [new(vacancy.Requirements[0].Id, "C#", RequirementKind.Mandatory)], OwnerId, later);
        moveToPast.Should().Throw<DomainException>("a changed date is still checked");

    }

    [Fact]
    public void Reopening_Needs_A_Planned_Close_That_Has_Not_Passed() {

        var vacancy = Create();
        vacancy.Close(CloseReason.Cancelled, OwnerId, Now);
        var later = Now.AddDays(60);

        vacancy.CanReopenUntil(vacancy.ClosesOn, later).Should().BeFalse();
        var keepOldDate = () => vacancy.Apply(VacancyTransition.Reopen, OwnerId, later);
        keepOldDate.Should().Throw<DomainException>();

        vacancy.Apply(VacancyTransition.Reopen, OwnerId, later, closesOn: Today.AddDays(90));
        vacancy.Status.Should().Be(VacancyStatus.Open);
        vacancy.ClosesOn.Should().Be(Today.AddDays(90));

    }

    [Fact]
    public void Changing_A_Text_Keeps_The_Id_And_Asks_To_Evaluate_It_Again() {

        var vacancy = Create();
        var (csharp, english) = (vacancy.Requirements[0], vacancy.Requirements[1]);

        var changed = vacancy.ReplaceRequirements([
            new(csharp.Id, "C# avanzado", RequirementKind.Mandatory, 5),
            new(english.Id, "Inglés", RequirementKind.Mandatory, 3),
            new(null, "PostgreSQL", RequirementKind.Desirable, null),
        ], OwnerId, Now);

        changed.Should().HaveCount(2).And.Contain(csharp.Id, "its text changed").And.NotContain(english.Id, "only kind and weight changed");
        vacancy.Requirements.Select(r => (r.Text, r.Kind, r.Weight)).Should().Equal(
            ("C# avanzado", RequirementKind.Mandatory, 5), ("Inglés", RequirementKind.Mandatory, 3), ("PostgreSQL", RequirementKind.Desirable, 2));

    }

    [Fact]
    public void Requirements_Left_Out_Are_Removed_And_Foreign_Ids_Are_Rejected() {
        var vacancy = Create();
        vacancy.ReplaceRequirements([new(vacancy.Requirements[1].Id, "Inglés", RequirementKind.Desirable)], OwnerId, Now);
        vacancy.Requirements.Should().ContainSingle().Which.Text.Should().Be("Inglés");
        var foreign = () => vacancy.ReplaceRequirements([new(Guid.NewGuid(), "Otro", RequirementKind.Desirable)], OwnerId, Now);
        foreign.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void Weights_Go_From_One_To_Five(int weight) {
        var vacancy = Create();
        var act = () => vacancy.ReplaceRequirements([new(vacancy.Requirements[0].Id, "C#", RequirementKind.Mandatory, weight)], OwnerId, Now);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void The_Life_Cycle_Allows_Only_Its_Transitions() {

        var vacancy = Create();

        vacancy.Allows(VacancyTransition.Resume).Should().BeFalse();
        vacancy.Allows(VacancyTransition.Reopen).Should().BeFalse();
        vacancy.Apply(VacancyTransition.Pause, OwnerId, Now);
        vacancy.Status.Should().Be(VacancyStatus.Paused);
        vacancy.Allows(VacancyTransition.Pause).Should().BeFalse();

        vacancy.Close(CloseReason.Filled, OwnerId, Now.AddDays(3));
        vacancy.Status.Should().Be(VacancyStatus.Closed);
        vacancy.ClosedOn.Should().Be(Today.AddDays(3));
        vacancy.CanClose.Should().BeFalse();
        var resume = () => vacancy.Apply(VacancyTransition.Resume, OwnerId, Now);
        resume.Should().Throw<DomainException>();

        vacancy.Apply(VacancyTransition.Reopen, OwnerId, Now.AddDays(5));
        vacancy.Status.Should().Be(VacancyStatus.Open);
        vacancy.CloseReason.Should().BeNull();
        vacancy.ClosedOn.Should().BeNull();
        vacancy.OpenedOn.Should().Be(Today.AddDays(5));

    }

    [Fact]
    public void Retention_Counts_From_The_Close_Or_From_The_Planned_Close() {
        Vacancy.DeletesOn(closesOn: Today.AddDays(30), closedOn: null, retentionDays: 90).Should().Be(Today.AddDays(120));
        Vacancy.DeletesOn(closesOn: Today.AddDays(30), closedOn: Today.AddDays(2), retentionDays: 90).Should().Be(Today.AddDays(92));
        Vacancy.OpenDays(Today, closedOn: null, today: Today.AddDays(7)).Should().Be(7);
        Vacancy.OpenDays(Today, closedOn: Today.AddDays(3), today: Today.AddDays(7)).Should().Be(3);
    }

}