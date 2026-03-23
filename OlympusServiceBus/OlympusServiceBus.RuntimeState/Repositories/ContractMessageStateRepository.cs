using Microsoft.EntityFrameworkCore;
using OlympusServiceBus.RuntimeState.Models;

namespace OlympusServiceBus.RuntimeState.Repositories;

public class ContractMessageStateRepository : IContractMessageStateRepository
{
    private readonly RuntimeStateDbContext _dbContext;

    public ContractMessageStateRepository(RuntimeStateDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ContractMessageStateEntity?> GetByContractAndBusinessKeyAsync(
        string contractId,
        string businessKey,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.ContractMessageStates
            .FirstOrDefaultAsync(
                x => x.ContractId == contractId &&
                     x.BusinessKey == businessKey,
                cancellationToken);
    }

    public async Task<List<ContractMessageStateEntity>> GetPendingByContractAsync(
        string contractId,
        CancellationToken cancellationToken = default)
    {
        var items = await _dbContext.ContractMessageStates
            .Where(x =>
                x.ContractId == contractId &&
                x.PublishStatus != "Published")
            .ToListAsync(cancellationToken);

        return items
            .OrderBy(x => x.FirstSeenAt)
            .ToList();
    }

    public async Task AddAsync(
        ContractMessageStateEntity entity,
        CancellationToken cancellationToken = default)
    {
        _dbContext.ContractMessageStates.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(
        ContractMessageStateEntity entity,
        CancellationToken cancellationToken = default)
    {
        _dbContext.ContractMessageStates.Update(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}