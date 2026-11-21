using Company.Product.Template.Shared;
using FluentAssertions;
using Xunit;

namespace Company.Product.Template.UnitTests.Shared;

public sealed class ResultTests {

    [Fact]
    public void Match_Should_Run_Only_The_Branch_That_Applies() {

        Result<int>.Success(7).Match(value => $"ok:{value}", error => $"ko:{error.Code}").Should().Be("ok:7");
        Result<int>.Failure(Error.NotFound("gone")).Match(value => $"ok:{value}", error => $"ko:{error.Code}").Should().Be("ko:not_found");

        Result.Success().Match(() => "ok", error => error.Code).Should().Be("ok");
        Result.Failure(Error.Conflict("busy")).Match(() => "ok", error => error.Code).Should().Be("conflict");

    }

    [Fact]
    public void Value_Should_Not_Be_Readable_From_A_Failed_Result() {

        var result = Result<string>.Failure(Error.Validation("bad input"));

        var act = () => result.Value;

        act.Should().Throw<InvalidOperationException>().WithMessage("*validation_error*");

    }

    [Fact]
    public void A_Failed_Result_Should_Always_Carry_Its_Error() {

        var result = Result.Failure(Error.Forbidden("no"));

        if (!result.IsFailure) {
            throw new Xunit.Sdk.XunitException("Expected a failed result.");
        }

        // Sin `!`: el compilador sabe que Error no es nulo tras comprobar IsFailure.
        result.Error.Kind.Should().Be(ErrorKind.Forbidden);

    }

    [Fact]
    public void Validation_Errors_Should_Share_A_Code_Whether_Or_Not_They_Name_A_Field() {

        var general = Error.Validation("The password is too short.");
        var single = Error.Validation("password", "The password is too short.");
        var many = Error.Validation(new Dictionary<string, string[]> { ["a"] = ["x"], ["b"] = ["y", "z"] });

        general.Code.Should().Be(Error.ValidationCode);
        general.FieldErrors.Should().BeNull();

        single.Code.Should().Be(Error.ValidationCode);
        single.Message.Should().Be(Error.ValidationSummary);
        single.FieldErrors.Should().ContainKey("password").WhoseValue.Should().ContainSingle().Which.Should().Be("The password is too short.");

        many.FieldErrors.Should().HaveCount(2);
        many.FieldErrors!["b"].Should().HaveCount(2);

    }

}