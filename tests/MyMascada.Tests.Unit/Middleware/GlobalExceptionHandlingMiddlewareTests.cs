using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyMascada.WebAPI.Middleware;
using Npgsql;

namespace MyMascada.Tests.Unit.Middleware;

public class GlobalExceptionHandlingMiddlewareTests
{
    private readonly ILogger<GlobalExceptionHandlingMiddleware> _logger =
        Substitute.For<ILogger<GlobalExceptionHandlingMiddleware>>();
    private readonly IWebHostEnvironment _environment = Substitute.For<IWebHostEnvironment>();

    public GlobalExceptionHandlingMiddlewareTests()
    {
        _environment.EnvironmentName.Returns("Production");
    }

    private static async Task<(HttpContext context, string body)> InvokeWithException(
        GlobalExceptionHandlingMiddleware middleware)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body, Encoding.UTF8);
        var body = await reader.ReadToEndAsync();
        return (context, body);
    }

    [Fact]
    public async Task InvokeAsync_WhenSerializationFailure_ShouldReturn409WithRetryMessage()
    {
        // The safety net for serializable-transaction conflicts that controllers
        // don't translate themselves (e.g. future paths): 409, not a raw 500.
        var middleware = new GlobalExceptionHandlingMiddleware(
            _ => throw new DbUpdateException(
                "save failed",
                new PostgresException("could not serialize access due to concurrent update", "ERROR", "ERROR", "40001")),
            _logger,
            _environment);

        var (context, body) = await InvokeWithException(middleware);

        context.Response.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        body.Should().Contain("please retry");
    }

    [Fact]
    public async Task InvokeAsync_WhenUnclassifiedException_ShouldReturn500()
    {
        var middleware = new GlobalExceptionHandlingMiddleware(
            _ => throw new DbUpdateException(
                "save failed",
                new PostgresException("duplicate key value violates unique constraint", "ERROR", "ERROR", "23505")),
            _logger,
            _environment);

        var (context, _) = await InvokeWithException(middleware);

        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }
}
