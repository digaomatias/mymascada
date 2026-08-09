using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using MyMascada.Application.Common.Configuration;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Infrastructure.Services.Email.Templates;

namespace MyMascada.Tests.Unit.Services;

public class EmailTemplateServiceTests : IDisposable
{
    private readonly string _contentRoot;
    private readonly EmailTemplateService _sut;

    public EmailTemplateServiceTests()
    {
        // Real template files on disk so path resolution is exercised end-to-end
        _contentRoot = Path.Combine(Path.GetTempPath(), $"email-template-tests-{Guid.NewGuid():N}");
        var templateDir = Path.Combine(_contentRoot, "EmailTemplates", "en-US");
        Directory.CreateDirectory(templateDir);
        File.WriteAllText(Path.Combine(templateDir, "welcome.subject.txt"), "Hello {{ name }}");
        File.WriteAllText(Path.Combine(templateDir, "welcome.body.html"), "<p>Hi {{ name }}</p>");

        // A file outside the template root that a traversal attack would try to reach
        File.WriteAllText(Path.Combine(_contentRoot, "secret.txt"), "top-secret");

        var env = Substitute.For<IWebHostEnvironment>();
        env.ContentRootPath.Returns(_contentRoot);

        _sut = new EmailTemplateService(
            Options.Create(new EmailOptions { TemplateDirectory = "EmailTemplates" }),
            env,
            Substitute.For<IApplicationLogger<EmailTemplateService>>());
    }

    public void Dispose()
    {
        try { Directory.Delete(_contentRoot, recursive: true); } catch { /* best effort */ }
    }

    [Fact]
    public async Task RenderAsync_WithValidTemplate_RendersSubjectAndBody()
    {
        var (subject, body) = await _sut.RenderAsync(
            "welcome",
            new Dictionary<string, object> { ["name"] = "Rodrigo" },
            "en-US");

        subject.Should().Be("Hello Rodrigo");
        body.Should().Contain("Hi Rodrigo");
    }

    [Theory]
    [InlineData("../secret")]
    [InlineData("..\\secret")]
    [InlineData("../../etc/passwd")]
    [InlineData("/etc/passwd")]
    [InlineData("en-US/../../secret")]
    [InlineData("welcome.body.html/..")]
    public async Task RenderAsync_WithPathTraversalTemplateName_ThrowsArgumentException(string templateName)
    {
        var act = () => _sut.RenderAsync(templateName, new Dictionary<string, object>());

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData("../en-US")]
    [InlineData("..")]
    [InlineData("en-US/../../other")]
    [InlineData("a/b")]
    public async Task RenderAsync_WithMaliciousLocale_FallsBackToDefaultLocale(string locale)
    {
        // Malicious locales are discarded, so the default-locale template renders
        var (subject, _) = await _sut.RenderAsync(
            "welcome",
            new Dictionary<string, object> { ["name"] = "x" },
            locale);

        subject.Should().Be("Hello x");
    }

    [Theory]
    [InlineData("../secret")]
    [InlineData("/etc/passwd")]
    [InlineData("..")]
    public void TemplateExists_WithPathTraversalTemplateName_ReturnsFalse(string templateName)
    {
        _sut.TemplateExists(templateName).Should().BeFalse();
    }

    [Fact]
    public void TemplateExists_WithValidTemplate_ReturnsTrue()
    {
        _sut.TemplateExists("welcome", "en-US").Should().BeTrue();
    }
}
