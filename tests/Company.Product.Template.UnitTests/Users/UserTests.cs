using Company.Product.Template.Domain.Users;
using Company.Product.Template.Shared;
using FluentAssertions;
using Xunit;

namespace Company.Product.Template.UnitTests.Users;

public sealed class UserTests {

    private static readonly Guid UserId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ActorId = new("22222222-2222-2222-2222-222222222222");
    private static readonly DateTimeOffset Now = new(2026, 9, 12, 12, 0, 0, TimeSpan.Zero);

    private static User Create(string username = "johndoe", string email = "john@example.com", Guid? id = null) =>
        User.Create(new UserCreationArgs(id ?? UserId, username, email, ActorId), Now);

    [Fact]
    public void Create_Should_Return_An_Enabled_User_With_A_Normalized_Email() {
        var user = Create(email: "  JOHN@Example.com  ");
        user.Username.Should().Be("johndoe");
        user.Email.Value.Should().Be("john@example.com");
        user.IsEnabled.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_Should_Throw_When_Username_Is_Missing(string username) {
        var act = () => Create(username: username);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_Should_Throw_When_Username_Is_Too_Long() {
        var act = () => Create(username: new string('a', User.MaxUsernameLength + 1));
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
    public void Create_Should_Throw_When_Username_Has_Control_Or_Format_Characters(string username) {
        var act = () => Create(username: username);
        act.Should().Throw<DomainException>();
    }

    /// <remarks>
    /// Se construye en tiempo de ejecución: un surrogate suelto en un InlineData no
    /// sobrevive a la serialización de los casos de la teoría.
    /// </remarks>
    [Fact]
    public void Create_Should_Throw_When_Username_Has_A_Lone_Surrogate() {
        var act = () => Create(username: "john" + (char)0xD800 + "doe");
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_Should_Still_Trim_Surrounding_Whitespace() {
        Create(username: "\tjohndoe\r\n").Username.Should().Be("johndoe");
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
    public void Suspend_And_Reinstate_Should_Toggle_The_Status() {
        var user = Create();
        user.Suspend(ActorId, Now);
        user.IsEnabled.Should().BeFalse();
        user.Reinstate(ActorId, Now);
        user.IsEnabled.Should().BeTrue();
    }

}