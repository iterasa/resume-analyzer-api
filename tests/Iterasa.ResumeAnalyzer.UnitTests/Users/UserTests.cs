using FluentAssertions;
using Iterasa.ResumeAnalyzer.Domain.Users;
using Iterasa.ResumeAnalyzer.Shared;
using Xunit;

namespace Iterasa.ResumeAnalyzer.UnitTests.Users;

public sealed class UserTests {

    private static readonly Guid UserId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ActorId = new("22222222-2222-2222-2222-222222222222");
    private static readonly DateTimeOffset Now = new(2026, 9, 12, 12, 0, 0, TimeSpan.Zero);

    private static User Create(string name = "John Doe", string email = "john@example.com", Guid? id = null, string? passwordHash = "$argon2id$v=19$m=19456,t=2,p=1$c2FsdA$aGFzaA") =>
        User.Create(new UserCreationArgs(id ?? UserId, name, email, passwordHash, ActorId), Now);

    [Fact]
    public void Create_Should_Return_An_Enabled_User_With_A_Normalized_Email() {
        var user = Create(email: "  JOHN@Example.com  ");
        user.Name.Should().Be("John Doe");
        user.Email.Value.Should().Be("john@example.com");
        user.IsEnabled.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_Should_Throw_When_Name_Is_Missing(string name) {
        var act = () => Create(name: name);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_Should_Throw_When_Name_Is_Too_Long() {
        var act = () => Create(name: new string('a', User.MaxNameLength + 1));
        act.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData("john\r\ndoe")]
    [InlineData("john\u001b[2Kdoe")]
    [InlineData("john\0doe")]
    [InlineData("john​doe")]
    [InlineData("john‮doe")]
    [InlineData("john\u2028doe")]
    [InlineData("john\u2029doe")]
    [InlineData("john\U000E0041doe")]
    public void Create_Should_Throw_When_Name_Has_Control_Or_Format_Characters(string name) {
        var act = () => Create(name: name);
        act.Should().Throw<DomainException>();
    }

    /// <remarks>
    /// Se construye en tiempo de ejecución: un surrogate suelto en un InlineData no
    /// sobrevive a la serialización de los casos de la teoría.
    /// </remarks>
    [Fact]
    public void Create_Should_Throw_When_Name_Has_A_Lone_Surrogate() {
        var act = () => Create(name: "john" + (char)0xD800 + "doe");
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_Should_Still_Trim_Surrounding_Whitespace() {
        Create(name: "\tJohn Doe\r\n").Name.Should().Be("John Doe");
    }

    [Fact]
    public void Create_Should_Throw_When_Email_Is_Invalid() {
        var act = () => Create(email: "not-an-email");
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Suspend_Should_Throw_When_A_User_Disables_Itself() {
        var user = Create();
        var act = () => user.Suspend(UserId, Now);
        act.Should().Throw<DomainException>();
        user.IsEnabled.Should().BeTrue();
    }

    /// <remarks>
    /// El dominio no consulta el reloj: toda marca temporal sale del instante que recibe
    /// el verbo, de modo que una prueba puede fijar la hora sin esperar ni simular nada.
    /// </remarks>
    [Fact]
    public void Every_Verb_Should_Stamp_The_Instant_It_Receives() {

        var user = Create();
        user.CreatedAtUtc.Should().Be(Now);
        user.UpdatedAtUtc.Should().Be(Now);

        var later = Now.AddHours(1);
        user.Suspend(ActorId, later);
        user.UpdatedAtUtc.Should().Be(later);
        user.UpdatedBy.Should().Be(ActorId);
        user.CreatedAtUtc.Should().Be(Now, "creation is stamped once");

    }

    [Fact]
    public void A_User_Without_Password_Or_Disabled_Cannot_Authenticate() {
        Create(passwordHash: null).CanAuthenticate(Now).Should().BeFalse();
        var user = Create();
        user.CanAuthenticate(Now).Should().BeTrue();
        user.Suspend(ActorId, Now);
        user.CanAuthenticate(Now).Should().BeFalse();
    }

    [Fact]
    public void Suspend_And_Reinstate_Should_Toggle_The_Status() {
        var user = Create();
        user.Suspend(ActorId, Now);
        user.IsEnabled.Should().BeFalse();
        user.Reinstate(ActorId, Now);
        user.IsEnabled.Should().BeTrue();
    }

}