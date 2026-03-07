using Microsoft.EntityFrameworkCore;
using OlympusServiceBus.RuntimeState.Models;

namespace OlympusServiceBus.RuntimeState.Repositories;

public class ContractExecutionStateRepository : IContractExecutionStateRepository
{
    private readonly RuntimeStateDbContext _context;

    public ContractExecutionStateRepository(RuntimeStateDbContext context)
    {
        _context = context;
    }

    public async Task<ContractExecutionStateEntity?> GetByContractIdAsync(string contractId, CancellationToken cancellationToken = default)
    {
        return await _context.ContractExecutionStates
            .FirstOrDefaultAsync(c => c.ContractId == contractId, cancellationToken);
    }

    public async Task AddAsync(ContractExecutionStateEntity entity, CancellationToken cancellationToken = default)
    {
        _context.ContractExecutionStates.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(ContractExecutionStateEntity entity, CancellationToken cancellationToken = default)
    {
        _context.ContractExecutionStates.Update(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }
}