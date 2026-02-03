using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.UserData.DTOs;

namespace MyMascada.Application.Features.UserData.Queries;

public class ExportUserDataQuery : IRequest<UserDataExportDto>
{
    public Guid UserId { get; set; }
}

public class ExportUserDataQueryHandler : IRequestHandler<ExportUserDataQuery, UserDataExportDto>
{
    private readonly IUserDataExportService _userDataExportService;

    public ExportUserDataQueryHandler(IUserDataExportService userDataExportService)
    {
        _userDataExportService = userDataExportService;
    }

    public async Task<UserDataExportDto> Handle(ExportUserDataQuery request, CancellationToken cancellationToken)
    {
        if (request.UserId == Guid.Empty)
            throw new ArgumentException("UserId cannot be empty", nameof(request.UserId));

        return await _userDataExportService.ExportUserDataAsync(request.UserId, cancellationToken);
    }
}
