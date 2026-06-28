using FtsCredit.Api.Domain.Interfaces;

namespace FtsCredit.Api.Infrastructure.Persistence;

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;

    public UnitOfWork(AppDbContext context) => _context = context;

    public Task<int> CommitAsync(CancellationToken ct = default) => _context.SaveChangesAsync(ct);

    public void Dispose() => _context.Dispose();
}
