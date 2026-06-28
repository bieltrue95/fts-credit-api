using FtsCredit.Api.Domain.Entities;
using FtsCredit.Api.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FtsCredit.Api.Infrastructure.Persistence.Repositories;

public class CreditRequestRepository : BaseRepository<CreditRequest>, ICreditRequestRepository
{
    public CreditRequestRepository(AppDbContext context) : base(context) { }

    public async Task<CreditRequest?> GetWithDetailsAsync(Guid id, CancellationToken ct = default) =>
        await Context.CreditRequests
            .Include(x => x.Customer)
            .Include(x => x.RiskAnalysis)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
}
