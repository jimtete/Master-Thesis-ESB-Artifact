namespace OlympusServiceBus.Utils.Contracts.Scheduling;

public enum ContractScheduleMode
{
    Manual = 0, // No automatic execution
    AdHoc = 1, // One specific DateTime
    Interval = 2, // Every X amount of time units
    Recurring = 3 // Calendar-based schedule
}