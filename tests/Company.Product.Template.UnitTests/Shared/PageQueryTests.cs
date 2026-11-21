using Company.Product.Template.Shared;
using FluentAssertions;
using Xunit;

namespace Company.Product.Template.UnitTests.Shared;

public sealed class PageQueryTests {

    [Theory]
    [InlineData(null, null, 1, PageQuery.DefaultPageSize)]
    [InlineData(0, 0, 1, 1)]
    [InlineData(-5, 10_000, 1, PageQuery.MaxPageSize)]
    [InlineData(3, 20, 3, 20)]
    public void Create_Should_Clamp_Page_And_Size(int? page, int? pageSize, int expectedPage, int expectedSize) {
        var query = PageQuery.Create(page, pageSize);
        query.Page.Should().Be(expectedPage);
        query.PageSize.Should().Be(expectedSize);
    }

    /// <remarks>
    /// Sin tope, <c>(page - 1) * pageSize</c> desborda: con page = int.MaxValue sale un
    /// desplazamiento negativo, que PostgreSQL rechaza con un 500.
    /// </remarks>
    [Fact]
    public void Skip_Should_Not_Overflow_For_The_Largest_Page() {
        var query = PageQuery.Create(int.MaxValue, PageQuery.MaxPageSize);
        query.Page.Should().Be(PageQuery.MaxPage);
        query.Skip.Should().BeGreaterThanOrEqualTo(0);
        query.Skip.Should().Be((PageQuery.MaxPage - 1) * PageQuery.MaxPageSize);
    }

}