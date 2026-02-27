using System.Security.Claims;
using System.Text;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MyMascada.Application.Features.CsvImport.Commands;
using MyMascada.Application.Features.CsvImport.DTOs;
using MyMascada.Application.Features.CsvImport.Queries;
using MyMascada.Application.Features.Transactions.DTOs;
using MyMascada.WebAPI.Controllers;
using MyMascada.Application.Common.Interfaces;

namespace MyMascada.Tests.Unit.Controllers;

public class CsvImportControllerTests
{
    private readonly IMediator _mediator;
    private readonly IAICsvAnalysisService _aiAnalysisService;
    private readonly CsvImportController _controller;
    private readonly Guid _userId = Guid.NewGuid();

    private readonly ICurrentUserService _currentUserService;

    public CsvImportControllerTests()
    {
        _mediator = Substitute.For<IMediator>();
        _aiAnalysisService = Substitute.For<IAICsvAnalysisService>();
        _currentUserService = Substitute.For<ICurrentUserService>();
        _currentUserService.GetUserId().Returns(_userId);

        _controller = new CsvImportController(_mediator, _aiAnalysisService, _currentUserService);

        SetupUserClaims();
    }

    private void SetupUserClaims()
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, _userId.ToString())
        };

        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = principal
            }
        };
    }

    private IFormFile CreateMockFormFile(string fileName, string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var stream = new MemoryStream(bytes);
        
        var formFile = Substitute.For<IFormFile>();
        formFile.FileName.Returns(fileName);
        formFile.Length.Returns(bytes.Length);
        formFile.ContentType.Returns("text/csv");
        formFile.OpenReadStream().Returns(stream);

        return formFile;
    }

    [Fact]
    public async Task UploadCsv_WithValidFile_ShouldReturnSuccess()
    {
        // Arrange
        var csvContent = "Date,Description,Amount\n2025-06-18,Test Transaction,-50.00";
        var file = CreateMockFormFile("test.csv", csvContent);
        var accountId = 1;

        var expectedResponse = new CsvImportResponse
        {
            IsSuccess = true,
            ProcessedRows = 1,
            TotalRows = 2,
            ImportedTransactions = new List<ImportedTransactionDto>
            {
                new() { Id = 1, Description = "Test Transaction", Amount = -50.00m, IsNew = true }
            }
        };

        _mediator.Send(Arg.Any<ImportCsvTransactionsCommand>())
            .Returns(expectedResponse);

        // Act
        var result = await _controller.UploadCsv(file, accountId);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<CsvImportResponse>().Subject;
        response.IsSuccess.Should().BeTrue();
        response.ProcessedRows.Should().Be(1);

        await _mediator.Received(1).Send(Arg.Is<ImportCsvTransactionsCommand>(cmd =>
            cmd.UserId == _userId &&
            cmd.AccountId == accountId &&
            cmd.FileName == "test.csv" &&
            cmd.Format == CsvFormat.Generic &&
            cmd.HasHeader == true &&
            cmd.SkipDuplicates == true &&
            cmd.AutoCategorize == true));
    }

    [Fact]
    public async Task UploadCsv_WithCustomParameters_ShouldPassParametersCorrectly()
    {
        // Arrange
        var csvContent = "Description,Amount\nTest,-50.00";
        var file = CreateMockFormFile("test.csv", csvContent);
        var accountId = 2;

        _mediator.Send(Arg.Any<ImportCsvTransactionsCommand>())
            .Returns(new CsvImportResponse { IsSuccess = true });

        // Act
        await _controller.UploadCsv(
            file, 
            accountId, 
            format: CsvFormat.Chase, 
            hasHeader: false, 
            skipDuplicates: false, 
            autoCategorize: false);

        // Assert
        await _mediator.Received(1).Send(Arg.Is<ImportCsvTransactionsCommand>(cmd =>
            cmd.AccountId == accountId &&
            cmd.Format == CsvFormat.Chase &&
            cmd.HasHeader == false &&
            cmd.SkipDuplicates == false &&
            cmd.AutoCategorize == false));
    }

    [Fact]
    public async Task UploadCsv_WithNoFile_ShouldReturnBadRequest()
    {
        // Act
        var result = await _controller.UploadCsv(null!, 1);

        // Assert
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var errorResponse = badRequestResult.Value.Should().BeAssignableTo<object>().Subject;
        
        var message = errorResponse.GetType().GetProperty("message")?.GetValue(errorResponse);
        message.Should().Be("No file uploaded");

        await _mediator.DidNotReceive().Send(Arg.Any<ImportCsvTransactionsCommand>());
    }

    [Fact]
    public async Task UploadCsv_WithEmptyFile_ShouldReturnBadRequest()
    {
        // Arrange
        var file = CreateMockFormFile("test.csv", "");
        
        // Mock empty file
        var emptyFile = Substitute.For<IFormFile>();
        emptyFile.FileName.Returns("test.csv");
        emptyFile.Length.Returns(0);

        // Act
        var result = await _controller.UploadCsv(emptyFile, 1);

        // Assert
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var errorResponse = badRequestResult.Value.Should().BeAssignableTo<object>().Subject;
        
        var message = errorResponse.GetType().GetProperty("message")?.GetValue(errorResponse);
        message.Should().Be("No file uploaded");
    }

    [Fact]
    public async Task UploadCsv_WithLargeFile_ShouldReturnBadRequest()
    {
        // Arrange
        var largeFile = Substitute.For<IFormFile>();
        largeFile.FileName.Returns("large.csv");
        largeFile.Length.Returns(11 * 1024 * 1024); // 11MB > 10MB limit

        // Act
        var result = await _controller.UploadCsv(largeFile, 1);

        // Assert
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var errorResponse = badRequestResult.Value.Should().BeAssignableTo<object>().Subject;
        
        var message = errorResponse.GetType().GetProperty("message")?.GetValue(errorResponse);
        message.Should().Be("File size exceeds 10MB limit");
    }

    [Fact]
    public async Task UploadCsv_WithNonCsvFile_ShouldReturnBadRequest()
    {
        // Arrange
        var txtFile = CreateMockFormFile("test.txt", "some content");

        // Act
        var result = await _controller.UploadCsv(txtFile, 1);

        // Assert
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var errorResponse = badRequestResult.Value.Should().BeAssignableTo<object>().Subject;
        
        var message = errorResponse.GetType().GetProperty("message")?.GetValue(errorResponse);
        message.Should().Be("Only CSV files are allowed");
    }

    [Fact]
    public async Task UploadCsv_WithFailedImport_ShouldReturnBadRequest()
    {
        // Arrange
        var file = CreateMockFormFile("test.csv", "Date,Description,Amount\n2025-06-18,Test,-50.00");
        var failedResponse = new CsvImportResponse
        {
            IsSuccess = false,
            Errors = new List<string> { "Invalid account ID" }
        };

        _mediator.Send(Arg.Any<ImportCsvTransactionsCommand>())
            .Returns(failedResponse);

        // Act
        var result = await _controller.UploadCsv(file, 999);

        // Assert
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = badRequestResult.Value.Should().BeOfType<CsvImportResponse>().Subject;
        response.IsSuccess.Should().BeFalse();
        response.Errors.Should().Contain("Invalid account ID");
    }

    [Fact]
    public async Task UploadCsv_WithException_ShouldReturnInternalServerError()
    {
        // Arrange
        var file = CreateMockFormFile("test.csv", "Date,Description,Amount\n2025-06-18,Test,-50.00");
        
        _mediator.Send(Arg.Any<ImportCsvTransactionsCommand>())
            .Returns(Task.FromException<CsvImportResponse>(new InvalidOperationException("Database error")));

        // Act
        var result = await _controller.UploadCsv(file, 1);

        // Assert
        var statusCodeResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        statusCodeResult.StatusCode.Should().Be(500);
        
        var errorResponse = statusCodeResult.Value.Should().BeAssignableTo<object>().Subject;
        var message = errorResponse.GetType().GetProperty("message")?.GetValue(errorResponse);

        message.Should().Be("An error occurred while processing the file");
    }

    [Fact]
    public void GetSupportedFormats_ShouldReturnAllFormats()
    {
        // Act
        var result = _controller.GetSupportedFormats();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var formats = okResult.Value.Should().BeAssignableTo<Dictionary<string, object>>().Subject;
        
        formats.Should().ContainKeys("Generic", "Chase", "WellsFargo", "BankOfAmerica", "Mint", "Quicken", "ANZ");
        formats.Should().HaveCount(7);
    }

    [Fact]
    public void DownloadTemplate_WithGenericFormat_ShouldReturnCsvFile()
    {
        // Act
        var result = _controller.DownloadTemplate(CsvFormat.Generic);

        // Assert
        var fileResult = result.Should().BeOfType<FileContentResult>().Subject;
        fileResult.ContentType.Should().Be("text/csv");
        fileResult.FileDownloadName.Should().Be("template_generic.csv");
        
        var content = Encoding.UTF8.GetString(fileResult.FileContents);
        content.Should().Contain("Date,Description,Amount,Reference");
        content.Should().Contain("2025-01-15,Coffee at Starbucks,-5.25,TXN001");
    }

    [Fact]
    public void DownloadTemplate_WithChaseFormat_ShouldReturnChaseTemplate()
    {
        // Act
        var result = _controller.DownloadTemplate(CsvFormat.Chase);

        // Assert
        var fileResult = result.Should().BeOfType<FileContentResult>().Subject;
        fileResult.FileDownloadName.Should().Be("template_chase.csv");
        
        var content = Encoding.UTF8.GetString(fileResult.FileContents);
        content.Should().Contain("Transaction Date,Post Date,Description,Amount");
        content.Should().Contain("01/15/2025,01/16/2025,STARBUCKS,-5.25");
    }

    [Fact]
    public void DownloadTemplate_WithWellsFargoFormat_ShouldReturnWellsFargoTemplate()
    {
        // Act
        var result = _controller.DownloadTemplate(CsvFormat.WellsFargo);

        // Assert
        var fileResult = result.Should().BeOfType<FileContentResult>().Subject;
        fileResult.FileDownloadName.Should().Be("template_wellsfargo.csv");
        
        var content = Encoding.UTF8.GetString(fileResult.FileContents);
        content.Should().Contain("Date,Amount,Memo,Description");
        content.Should().Contain("01/15/2025,-5.25,DEBIT,STARBUCKS PURCHASE");
    }

    [Fact]
    public async Task PreviewCsv_WithValidFile_ShouldReturnPreviewResult()
    {
        // Arrange
        var file = CreateMockFormFile("test.csv", "Date,Description,Amount\n2025-06-18,Test,-50.00");

        // Act
        var result = await _controller.PreviewCsv(file);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var parseResult = okResult.Value.Should().BeOfType<CsvParseResult>().Subject;
        
        parseResult.IsSuccess.Should().BeTrue();
        parseResult.Warnings.Should().Contain("Preview functionality not yet implemented");
    }

    [Fact]
    public async Task PreviewCsv_WithNoFile_ShouldReturnBadRequest()
    {
        // Act
        var result = await _controller.PreviewCsv(null!);

        // Assert
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var errorResponse = badRequestResult.Value.Should().BeAssignableTo<object>().Subject;
        
        var message = errorResponse.GetType().GetProperty("message")?.GetValue(errorResponse);
        message.Should().Be("No file uploaded");
    }

    [Fact]
    public async Task PreviewCsv_WithNonCsvFile_ShouldReturnBadRequest()
    {
        // Arrange
        var txtFile = CreateMockFormFile("test.txt", "some content");

        // Act
        var result = await _controller.PreviewCsv(txtFile);

        // Assert
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var errorResponse = badRequestResult.Value.Should().BeAssignableTo<object>().Subject;
        
        var message = errorResponse.GetType().GetProperty("message")?.GetValue(errorResponse);
        message.Should().Be("Only CSV files are allowed");
    }

    [Fact]
    public async Task DetectTransfers_WithValidRequest_ShouldReturnDetectionResult()
    {
        // Arrange
        var request = new DetectTransfersRequest
        {
            Transactions = new List<TransactionDto>
            {
                new() { Id = 1, Amount = -100m, Description = "Transfer" },
                new() { Id = 2, Amount = 100m, Description = "Transfer" }
            }
        };

        var expectedResult = new TransferDetectionResult
        {
            Candidates = new List<TransferCandidate>
            {
                new() 
                { 
                    DebitTransaction = new TransactionDto { Id = 1, Amount = -100m },
                    CreditTransaction = new TransactionDto { Id = 2, Amount = 100m },
                    ConfidenceScore = 0.95m,
                    Amount = 100m
                }
            }
        };

        _mediator.Send(Arg.Any<DetectTransfersQuery>())
            .Returns(expectedResult);

        // Act
        var result = await _controller.DetectTransfers(request);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var detectionResult = okResult.Value.Should().BeOfType<TransferDetectionResult>().Subject;
        
        detectionResult.Candidates.Should().HaveCount(1);
        detectionResult.Candidates.First().ConfidenceScore.Should().Be(0.95m);

        await _mediator.Received(1).Send(Arg.Any<DetectTransfersQuery>());
    }

    [Fact]
    public async Task DetectTransfers_WithException_ShouldReturnInternalServerError()
    {
        // Arrange
        var request = new DetectTransfersRequest();
        
        _mediator.Send(Arg.Any<DetectTransfersQuery>())
            .Returns(Task.FromException<TransferDetectionResult>(new InvalidOperationException("Detection failed")));

        // Act
        var result = await _controller.DetectTransfers(request);

        // Assert
        var statusCodeResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        statusCodeResult.StatusCode.Should().Be(500);
        
        var errorResponse = statusCodeResult.Value.Should().BeAssignableTo<object>().Subject;
        var message = errorResponse.GetType().GetProperty("message")?.GetValue(errorResponse);

        message.Should().Be("An error occurred while detecting transfers");
    }

    [Fact]
    public async Task ConfirmTransfer_WithValidRequest_ShouldReturnSuccess()
    {
        // Arrange
        var request = new ConfirmTransferCandidateRequest
        {
            DebitTransactionId = 1,
            CreditTransactionId = 2,
            IsConfirmed = true,
            Description = "Transfer between accounts"
        };

        _mediator.Send(Arg.Any<ConfirmTransferCandidateCommand>())
            .Returns(true);

        // Act
        var result = await _controller.ConfirmTransfer(request);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var success = okResult.Value.Should().Be(true);

        await _mediator.Received(1).Send(Arg.Is<ConfirmTransferCandidateCommand>(cmd =>
            cmd.DebitTransactionId == 1 &&
            cmd.CreditTransactionId == 2 &&
            cmd.IsConfirmed == true &&
            cmd.Description == "Transfer between accounts" &&
            cmd.UserId == _userId));
    }

    [Fact]
    public async Task ConfirmTransfer_WithException_ShouldReturnInternalServerError()
    {
        // Arrange
        var request = new ConfirmTransferCandidateRequest();
        
        _mediator.Send(Arg.Any<ConfirmTransferCandidateCommand>())
            .Returns(Task.FromException<bool>(new InvalidOperationException("Confirmation failed")));

        // Act
        var result = await _controller.ConfirmTransfer(request);

        // Assert
        var statusCodeResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        statusCodeResult.StatusCode.Should().Be(500);
        
        var errorResponse = statusCodeResult.Value.Should().BeAssignableTo<object>().Subject;
        var message = errorResponse.GetType().GetProperty("message")?.GetValue(errorResponse);

        message.Should().Be("An error occurred while confirming transfer");
    }
}