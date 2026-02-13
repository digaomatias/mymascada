namespace MyMascada.Application.Common.Interfaces;

public interface IFinancialContextBuilder
{
    Task<string> BuildContextAsync(Guid userId);
}
