using Microsoft.EntityFrameworkCore;
using MyMascada.WebAPI.Extensions;
using Npgsql;

namespace MyMascada.Tests.Unit.Extensions;

public class ExceptionExtensionsTests
{
    private static PostgresException SerializationFailure() =>
        new("could not serialize access due to concurrent update", "ERROR", "ERROR", "40001");

    [Fact]
    public void IsPostgresSerializationFailure_WithBarePostgresException40001_ShouldBeTrue()
    {
        SerializationFailure().IsPostgresSerializationFailure().Should().BeTrue();
    }

    [Fact]
    public void IsPostgresSerializationFailure_WhenWrappedInDbUpdateException_ShouldBeTrue()
    {
        var wrapped = new DbUpdateException("save failed", SerializationFailure());

        wrapped.IsPostgresSerializationFailure().Should().BeTrue();
    }

    [Fact]
    public void IsPostgresSerializationFailure_WhenNestedTwoLevelsDeep_ShouldBeTrue()
    {
        var nested = new InvalidOperationException(
            "outer",
            new DbUpdateException("save failed", SerializationFailure()));

        nested.IsPostgresSerializationFailure().Should().BeTrue();
    }

    [Fact]
    public void IsPostgresSerializationFailure_WithOtherSqlState_ShouldBeFalse()
    {
        var uniqueViolation = new DbUpdateException(
            "save failed",
            new PostgresException("duplicate key value violates unique constraint", "ERROR", "ERROR", "23505"));

        uniqueViolation.IsPostgresSerializationFailure().Should().BeFalse();
    }

    [Fact]
    public void IsPostgresSerializationFailure_WithUnrelatedException_ShouldBeFalse()
    {
        new InvalidOperationException("nope").IsPostgresSerializationFailure().Should().BeFalse();
    }
}
