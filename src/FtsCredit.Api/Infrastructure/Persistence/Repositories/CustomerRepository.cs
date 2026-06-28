using FtsCredit.Api.Domain.Entities;
using FtsCredit.Api.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FtsCredit.Api.Infrastructure.Persistence.Repositories;

public class CustomerRepository : BaseRepository<Customer>, ICustomerRepository
{
    public CustomerRepository(AppDbContext context) : base(context) { }

    public async Task<Customer?> GetByDocumentAsync(string document, CancellationToken ct = default) =>
        await Context.Customers.FirstOrDefaultAsync(x => x.Document == document, ct);
}
