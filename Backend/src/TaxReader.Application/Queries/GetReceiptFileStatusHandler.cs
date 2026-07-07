using Microsoft.EntityFrameworkCore;
using TaxReader.Application.DTOs;
using TaxReader.Application.Interfaces;
using TaxReader.Domain.Common;
using TaxReader.Domain.Enums;

namespace TaxReader.Application.Queries;

public class GetReceiptFileStatusHandler(
    IAppDbContext dbContext,
    ICurrentUser currentUser)
{
    public async Task<Result<ReceiptFileStatusDto>> HandleAsync(
        GetReceiptFileStatusQuery query,
        CancellationToken cancellationToken)
    {
        var file = await dbContext.ReceiptFiles
            .AsNoTracking()
            .Where(f => f.Id == query.ReceiptFileId && f.UserId == currentUser.UserId)
            .Select(f => new { f.Id, f.UploadedAt, ReceiptId = f.Receipt != null ? f.Receipt.Id : (Guid?)null })
            .FirstOrDefaultAsync(cancellationToken);

        if (file is null)
            return Result<ReceiptFileStatusDto>.Failure("NotFound");

        var run = await dbContext.ProcessingRuns
            .AsNoTracking()
            .Where(r => r.ReceiptFileId == file.Id)
            .OrderByDescending(r => r.StartedAt)
            .Select(r => new { r.Status, r.CompletedAt, r.StartedAt, r.ErrorCode, r.ErrorMessage })
            .FirstOrDefaultAsync(cancellationToken);

        if (run is null)
            return Result<ReceiptFileStatusDto>.Success(
                new ReceiptFileStatusDto(ProcessingStatus.Pending, file.UploadedAt, null, null, file.ReceiptId));

        return Result<ReceiptFileStatusDto>.Success(new ReceiptFileStatusDto(
            run.Status,
            run.CompletedAt ?? run.StartedAt,
            run.ErrorCode,
            run.ErrorMessage,
            file.ReceiptId));
    }
}
