using MediatR;
using MyMascada.Application.Common.Interfaces;

namespace MyMascada.Application.Features.Categories.Commands;

public class DeleteCategoryCommand : IRequest<Unit>
{
    public int CategoryId { get; set; }
    public Guid UserId { get; set; }
}

public class DeleteCategoryCommandHandler : IRequestHandler<DeleteCategoryCommand, Unit>
{
    private readonly ICategoryRepository _categoryRepository;
    private readonly ITransactionRepository _transactionRepository;

    public DeleteCategoryCommandHandler(
        ICategoryRepository categoryRepository,
        ITransactionRepository transactionRepository)
    {
        _categoryRepository = categoryRepository;
        _transactionRepository = transactionRepository;
    }

    public async Task<Unit> Handle(DeleteCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = await _categoryRepository.GetByIdAsync(request.CategoryId);
        
        if (category == null)
        {
            throw new ArgumentException("Category not found.");
        }
        
        // Check if user has permission to delete this category
        if (category.IsSystemCategory || (category.UserId != request.UserId))
        {
            throw new UnauthorizedAccessException("You don't have permission to delete this category.");
        }
        
        // Check if category has transactions
        var transactions = await _transactionRepository.GetByCategoryIdAsync(request.CategoryId, request.UserId);
        if (transactions.Any())
        {
            throw new InvalidOperationException(
                "Cannot delete category that has transactions. " +
                "Please reassign or delete all transactions in this category first."
            );
        }
        
        // Check if category has subcategories
        var allUserCategories = await _categoryRepository.GetByUserIdAsync(request.UserId);
        var hasSubcategories = allUserCategories.Any(c => c.ParentCategoryId == request.CategoryId);
        
        if (hasSubcategories)
        {
            throw new InvalidOperationException(
                "Cannot delete category that has subcategories. " +
                "Please delete or reassign all subcategories first."
            );
        }

        // Perform soft delete
        await _categoryRepository.DeleteAsync(category);

        return Unit.Value;
    }
}