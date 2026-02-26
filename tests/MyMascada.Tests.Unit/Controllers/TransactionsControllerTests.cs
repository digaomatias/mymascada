using MyMascada.Domain.Enums;
using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Transactions.Commands;
using MyMascada.Application.Features.Transactions.DTOs;
using MyMascada.Application.Features.Transactions.Queries;
using MyMascada.WebAPI.Controllers;

namespace MyMascada.Tests.Unit.Controllers;

public class TransactionsControllerTests
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUserService;
    private readonly TransactionsController _controller;
    private readonly Guid _userId = Guid.NewGuid();

    public TransactionsControllerTests()
    {
        _mediator = Substitute.For<IMediator>();
        _currentUserService = Substitute.For<ICurrentUserService>();
        _currentUserService.GetUserId().Returns(_userId);

        _controller = new TransactionsController(_mediator, _currentUserService);

        // Setup user claims
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

    [Fact]
    public async Task GetTransactions_WithDefaultParameters_ShouldReturnTransactions()
    {
        // Arrange
        var expectedResponse = new TransactionListResponse
        {
            Transactions = new List<TransactionDto>
            {
                new() { Id = 1, Description = "Test Transaction", Amount = -50.00m },
                new() { Id = 2, Description = "Another Transaction", Amount = -30.00m }
            },
            TotalCount = 2,
            Page = 1,
            PageSize = 50
        };

        _mediator.Send(Arg.Any<GetTransactionsQuery>())
            .Returns(expectedResponse);

        // Act
        var result = await _controller.GetTransactions();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<TransactionListResponse>().Subject;
        response.Transactions.Should().HaveCount(2);
        response.TotalCount.Should().Be(2);

        await _mediator.Received(1).Send(Arg.Is<GetTransactionsQuery>(q =>
            q.UserId == _userId &&
            q.Page == 1 &&
            q.PageSize == 50 &&
            q.SortBy == "TransactionDate" &&
            q.SortDirection == "desc"));
    }

    [Fact]
    public async Task GetTransactions_WithFilters_ShouldPassFiltersToQuery()
    {
        // Arrange
        var accountId = 1;
        var categoryId = 2;
        var startDate = new DateTime(2025, 1, 1);
        var endDate = new DateTime(2025, 12, 31);
        var minAmount = 10.00m;
        var maxAmount = 100.00m;
        var status = TransactionStatus.Cleared;
        var searchTerm = "test";

        _mediator.Send(Arg.Any<GetTransactionsQuery>())
            .Returns(new TransactionListResponse { Transactions = new List<TransactionDto>() });

        // Act
        await _controller.GetTransactions(
            page: 2,
            pageSize: 25,
            accountId: accountId,
            categoryId: categoryId,
            startDate: startDate,
            endDate: endDate,
            minAmount: minAmount,
            maxAmount: maxAmount,
            status: status,
            searchTerm: searchTerm,
            isReviewed: true,
            isExcluded: false);

        // Assert
        await _mediator.Received(1).Send(Arg.Is<GetTransactionsQuery>(q =>
            q.UserId == _userId &&
            q.Page == 2 &&
            q.PageSize == 25 &&
            q.AccountId == accountId &&
            q.CategoryId == categoryId &&
            q.StartDate == startDate &&
            q.EndDate == endDate &&
            q.MinAmount == minAmount &&
            q.MaxAmount == maxAmount &&
            q.Status == status &&
            q.SearchTerm == searchTerm &&
            q.IsReviewed == true &&
            q.IsExcluded == false));
    }

    [Fact]
    public async Task GetTransactions_WithLargePageSize_ShouldLimitPageSize()
    {
        // Arrange
        _mediator.Send(Arg.Any<GetTransactionsQuery>())
            .Returns(new TransactionListResponse { Transactions = new List<TransactionDto>() });

        // Act
        await _controller.GetTransactions(pageSize: 500);

        // Assert
        await _mediator.Received(1).Send(Arg.Is<GetTransactionsQuery>(q =>
            q.PageSize == 100));
    }

    [Fact]
    public async Task GetTransaction_WithValidId_ShouldReturnTransaction()
    {
        // Arrange
        var transactionId = 1;
        var expectedTransaction = new TransactionDto
        {
            Id = transactionId,
            Description = "Test Transaction",
            Amount = -50.00m
        };

        _mediator.Send(Arg.Any<GetTransactionQuery>())
            .Returns(expectedTransaction);

        // Act
        var result = await _controller.GetTransaction(transactionId);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var transaction = okResult.Value.Should().BeOfType<TransactionDto>().Subject;
        transaction.Id.Should().Be(transactionId);

        await _mediator.Received(1).Send(Arg.Is<GetTransactionQuery>(q =>
            q.UserId == _userId &&
            q.Id == transactionId));
    }

    [Fact]
    public async Task GetTransaction_WithInvalidId_ShouldReturnNotFound()
    {
        // Arrange
        var transactionId = 999;
        _mediator.Send(Arg.Any<GetTransactionQuery>())
            .Returns((TransactionDto?)null);

        // Act
        var result = await _controller.GetTransaction(transactionId);

        // Assert
        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task CreateTransaction_WithValidRequest_ShouldReturnCreated()
    {
        // Arrange
        var request = new CreateTransactionRequest
        {
            Amount = -50.00m,
            TransactionDate = DateTime.Today,
            Description = "Test Transaction",
            UserDescription = "User Description",
            Status = TransactionStatus.Cleared,
            Notes = "Test notes",
            Location = "Test Location",
            Tags = "tag1,tag2",
            AccountId = 1,
            CategoryId = 2
        };

        var expectedTransaction = new TransactionDto
        {
            Id = 1,
            Amount = request.Amount,
            Description = request.Description,
            UserDescription = request.UserDescription
        };

        _mediator.Send(Arg.Any<CreateTransactionCommand>())
            .Returns(expectedTransaction);

        // Act
        var result = await _controller.CreateTransaction(request);

        // Assert
        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.ActionName.Should().Be(nameof(TransactionsController.GetTransaction));
        createdResult.RouteValues!["id"].Should().Be(1);
        
        var transaction = createdResult.Value.Should().BeOfType<TransactionDto>().Subject;
        transaction.Id.Should().Be(1);

        await _mediator.Received(1).Send(Arg.Is<CreateTransactionCommand>(cmd =>
            cmd.UserId == _userId &&
            cmd.Amount == request.Amount &&
            cmd.TransactionDate == request.TransactionDate &&
            cmd.Description == request.Description &&
            cmd.UserDescription == request.UserDescription &&
            cmd.Status == request.Status &&
            cmd.Notes == request.Notes &&
            cmd.Location == request.Location &&
            cmd.Tags == request.Tags &&
            cmd.AccountId == request.AccountId &&
            cmd.CategoryId == request.CategoryId));
    }

    [Fact]
    public async Task CreateTransaction_WithInvalidData_ShouldReturnBadRequest()
    {
        // Arrange
        var request = new CreateTransactionRequest
        {
            Amount = -50.00m,
            AccountId = 999 // Invalid account ID
        };

        _mediator.Send(Arg.Any<CreateTransactionCommand>())
            .Returns(Task.FromException<TransactionDto>(new ArgumentException("Invalid account ID")));

        // Act
        var result = await _controller.CreateTransaction(request);

        // Assert
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var errorResponse = badRequestResult.Value.Should().BeAssignableTo<object>().Subject;
        
        var message = errorResponse.GetType().GetProperty("message")?.GetValue(errorResponse);
        message.Should().Be("Invalid account ID");
    }

    [Fact]
    public async Task UpdateTransaction_WithValidRequest_ShouldReturnUpdatedTransaction()
    {
        // Arrange
        var transactionId = 1;
        var request = new UpdateTransactionRequest
        {
            Id = transactionId,
            Amount = -75.00m,
            TransactionDate = DateTime.Today,
            Description = "Updated Transaction",
            CategoryId = 3
        };

        var expectedTransaction = new TransactionDto
        {
            Id = transactionId,
            Amount = request.Amount,
            Description = request.Description
        };

        _mediator.Send(Arg.Any<UpdateTransactionCommand>())
            .Returns(expectedTransaction);

        // Act
        var result = await _controller.UpdateTransaction(transactionId, request);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var transaction = okResult.Value.Should().BeOfType<TransactionDto>().Subject;
        transaction.Id.Should().Be(transactionId);

        await _mediator.Received(1).Send(Arg.Is<UpdateTransactionCommand>(cmd =>
            cmd.UserId == _userId &&
            cmd.Id == transactionId &&
            cmd.Amount == request.Amount));
    }

    [Fact]
    public async Task UpdateTransaction_WithMismatchedId_ShouldReturnBadRequest()
    {
        // Arrange
        var transactionId = 1;
        var request = new UpdateTransactionRequest
        {
            Id = 2, // Different ID
            Amount = -75.00m
        };

        // Act
        var result = await _controller.UpdateTransaction(transactionId, request);

        // Assert
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.Value.Should().Be("Transaction ID mismatch");
    }

    [Fact]
    public async Task UpdateTransaction_WithInvalidData_ShouldReturnBadRequest()
    {
        // Arrange
        var transactionId = 1;
        var request = new UpdateTransactionRequest
        {
            Id = transactionId,
            CategoryId = 999 // Invalid category ID
        };

        _mediator.Send(Arg.Any<UpdateTransactionCommand>())
            .Returns(Task.FromException<TransactionDto>(new ArgumentException("Invalid category ID")));

        // Act
        var result = await _controller.UpdateTransaction(transactionId, request);

        // Assert
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var errorResponse = badRequestResult.Value.Should().BeAssignableTo<object>().Subject;
        
        var message = errorResponse.GetType().GetProperty("message")?.GetValue(errorResponse);
        message.Should().Be("Invalid category ID");
    }

    [Fact]
    public async Task DeleteTransaction_WithValidId_ShouldReturnNoContent()
    {
        // Arrange
        var transactionId = 1;
        _mediator.Send(Arg.Any<DeleteTransactionCommand>())
            .Returns(true);

        // Act
        var result = await _controller.DeleteTransaction(transactionId);

        // Assert
        result.Should().BeOfType<NoContentResult>();

        await _mediator.Received(1).Send(Arg.Is<DeleteTransactionCommand>(cmd =>
            cmd.UserId == _userId &&
            cmd.Id == transactionId));
    }

    [Fact]
    public async Task DeleteTransaction_WithInvalidId_ShouldReturnNotFound()
    {
        // Arrange
        var transactionId = 999;
        _mediator.Send(Arg.Any<DeleteTransactionCommand>())
            .Returns(false);

        // Act
        var result = await _controller.DeleteTransaction(transactionId);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetRecentTransactions_WithDefaultCount_ShouldReturnRecentTransactions()
    {
        // Arrange
        var expectedResponse = new TransactionListResponse
        {
            Transactions = new List<TransactionDto>
            {
                new() { Id = 1, Description = "Recent Transaction 1" },
                new() { Id = 2, Description = "Recent Transaction 2" }
            }
        };

        _mediator.Send(Arg.Any<GetTransactionsQuery>())
            .Returns(expectedResponse);

        // Act
        var result = await _controller.GetRecentTransactions();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var transactions = okResult.Value.Should().BeAssignableTo<List<TransactionDto>>().Subject;
        transactions.Should().HaveCount(2);

        await _mediator.Received(1).Send(Arg.Is<GetTransactionsQuery>(q =>
            q.UserId == _userId &&
            q.PageSize == 10 &&
            q.SortBy == "TransactionDate" &&
            q.SortDirection == "desc"));
    }

    [Fact]
    public async Task GetRecentTransactions_WithCustomCount_ShouldLimitCount()
    {
        // Arrange
        _mediator.Send(Arg.Any<GetTransactionsQuery>())
            .Returns(new TransactionListResponse { Transactions = new List<TransactionDto>() });

        // Act
        await _controller.GetRecentTransactions(count: 100);

        // Assert
        await _mediator.Received(1).Send(Arg.Is<GetTransactionsQuery>(q =>
            q.PageSize == 50)); // Should be limited to 50
    }

    [Fact]
    public async Task ReviewTransaction_WithValidId_ShouldReturnNoContent()
    {
        // Arrange
        var transactionId = 1;
        _mediator.Send(Arg.Any<ReviewTransactionCommand>())
            .Returns(true);

        // Act
        var result = await _controller.ReviewTransaction(transactionId);

        // Assert
        result.Should().BeOfType<NoContentResult>();

        await _mediator.Received(1).Send(Arg.Is<ReviewTransactionCommand>(cmd =>
            cmd.UserId == _userId &&
            cmd.TransactionId == transactionId));
    }

    [Fact]
    public async Task ReviewTransaction_WithInvalidId_ShouldReturnNotFound()
    {
        // Arrange
        var transactionId = 999;
        _mediator.Send(Arg.Any<ReviewTransactionCommand>())
            .Returns(false);

        // Act
        var result = await _controller.ReviewTransaction(transactionId);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task ReviewTransaction_WithError_ShouldReturnBadRequest()
    {
        // Arrange
        var transactionId = 1;
        _mediator.Send(Arg.Any<ReviewTransactionCommand>())
            .Returns(Task.FromException<bool>(new ArgumentException("Transaction already reviewed")));

        // Act
        var result = await _controller.ReviewTransaction(transactionId);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var errorResponse = badRequestResult.Value.Should().BeAssignableTo<object>().Subject;
        
        var message = errorResponse.GetType().GetProperty("message")?.GetValue(errorResponse);
        message.Should().Be("Transaction already reviewed");
    }

    [Fact]
    public async Task ReviewAllTransactions_WithSuccess_ShouldReturnResult()
    {
        // Arrange
        var expectedResponse = new ReviewAllTransactionsResponse
        {
            Success = true,
            ReviewedCount = 5,
            Message = "5 transactions reviewed successfully"
        };

        _mediator.Send(Arg.Any<ReviewAllTransactionsCommand>())
            .Returns(expectedResponse);

        // Act
        var result = await _controller.ReviewAllTransactions();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ReviewAllTransactionsResponse>().Subject;
        response.Success.Should().BeTrue();
        response.ReviewedCount.Should().Be(5);

        await _mediator.Received(1).Send(Arg.Is<ReviewAllTransactionsCommand>(cmd =>
            cmd.UserId == _userId));
    }

    [Fact]
    public async Task ReviewAllTransactions_WithFailure_ShouldReturnBadRequest()
    {
        // Arrange
        var expectedResponse = new ReviewAllTransactionsResponse
        {
            Success = false,
            Message = "No transactions to review"
        };

        _mediator.Send(Arg.Any<ReviewAllTransactionsCommand>())
            .Returns(expectedResponse);

        // Act
        var result = await _controller.ReviewAllTransactions();

        // Assert
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var errorResponse = badRequestResult.Value.Should().BeAssignableTo<object>().Subject;
        
        var message = errorResponse.GetType().GetProperty("message")?.GetValue(errorResponse);
        message.Should().Be("No transactions to review");
    }

    [Fact]
    public async Task GetDescriptionSuggestions_WithDefaultParameters_ShouldReturnSuggestions()
    {
        // Arrange
        var expectedSuggestions = new List<string> { "Coffee Shop", "Gas Station", "Grocery Store" };
        _mediator.Send(Arg.Any<GetDescriptionSuggestionsQuery>())
            .Returns(expectedSuggestions);

        // Act
        var result = await _controller.GetDescriptionSuggestions();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var suggestions = okResult.Value.Should().BeAssignableTo<IEnumerable<string>>().Subject;
        suggestions.Should().HaveCount(3);

        await _mediator.Received(1).Send(Arg.Is<GetDescriptionSuggestionsQuery>(q =>
            q.UserId == _userId &&
            q.SearchTerm == null &&
            q.Limit == 10));
    }

    [Fact]
    public async Task GetDescriptionSuggestions_WithSearchTerm_ShouldPassSearchTerm()
    {
        // Arrange
        var searchTerm = "coffee";
        var limit = 5;
        _mediator.Send(Arg.Any<GetDescriptionSuggestionsQuery>())
            .Returns(new List<string>());

        // Act
        await _controller.GetDescriptionSuggestions(q: searchTerm, limit: limit);

        // Assert
        await _mediator.Received(1).Send(Arg.Is<GetDescriptionSuggestionsQuery>(q =>
            q.UserId == _userId &&
            q.SearchTerm == searchTerm &&
            q.Limit == limit));
    }

    [Fact]
    public async Task GetUserId_WithMissingClaim_ShouldThrowUnauthorizedAccessException()
    {
        // Arrange - Mock ICurrentUserService to throw when user is not authenticated
        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.GetUserId().Returns(_ => throw new UnauthorizedAccessException("Invalid user ID in token"));

        var controller = new TransactionsController(_mediator, currentUserService);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
        {
            await controller.GetTransactions();
        });

        exception.Message.Should().Be("Invalid user ID in token");
    }

    [Fact]
    public async Task GetUserId_WithInvalidClaim_ShouldThrowUnauthorizedAccessException()
    {
        // Arrange - Mock ICurrentUserService to throw when claims are invalid
        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.GetUserId().Returns(_ => throw new UnauthorizedAccessException("Invalid user ID in token"));

        var controller = new TransactionsController(_mediator, currentUserService);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
        {
            await controller.GetTransactions();
        });

        exception.Message.Should().Be("Invalid user ID in token");
    }
}