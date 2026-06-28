using FtsCredit.Api.Domain.Entities;

namespace FtsCredit.Api.Domain.Interfaces;

public interface ICreditRequestRepository : IRepository<CreditRequest>
{
    Task<CreditRequest?> GetWithDetailsAsync(Guid id, CancellationToken ct = default);
}
