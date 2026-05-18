using Microsoft.EntityFrameworkCore;
using OlympusServiceBus.RuntimeState;

if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: RuntimeStateCleanup <dbPath> <contractId>");
    return 1;
}

var dbPath = args[0];
var contractId = args[1];

if (!File.Exists(dbPath))
{
    Console.Error.WriteLine($"Database file not found: {dbPath}");
    return 2;
}

var options = new DbContextOptionsBuilder<RuntimeStateDbContext>()
    .UseSqlite($"Data Source={dbPath}")
    .Options;

await using var db = new RuntimeStateDbContext(options);

var messageRows = await db.ContractMessageStates
    .Where(x => x.ContractId == contractId)
    .ToListAsync();

var executionRows = await db.ContractExecutionStates
    .Where(x => x.ContractId == contractId)
    .ToListAsync();

db.ContractMessageStates.RemoveRange(messageRows);
db.ContractExecutionStates.RemoveRange(executionRows);

await db.SaveChangesAsync();

Console.WriteLine($"Deleted ContractMessageState rows: {messageRows.Count}");
Console.WriteLine($"Deleted ContractExecutionState rows: {executionRows.Count}");

return 0;
