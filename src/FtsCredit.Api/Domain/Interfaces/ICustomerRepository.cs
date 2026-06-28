using FtsCredit.Api.Domain.Entities;

namespace FtsCredit.Api.Domain.Interfaces;

public interface ICustomerRepository : IRepository<Customer>
{
    Task<Customer?> GetByDocumentAsync(string document, CancellationToken ct = default);
}
