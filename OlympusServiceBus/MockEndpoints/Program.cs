var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

var FirstNames = new[]
{
    "Oliver", "Simon", "Geralt", "Triss", "Vincent", "Ciri", "Panam", "Dimitrios"
};

var LastNames = new[]
{
    "Tetepoulidis", "Kjelberg", "Johnson", "Papoudari", "Viborg", "Nielsen", "Klein"
};

app.MapGet("/get-random-guest-registration", () =>
{
    var rnd = Random.Shared;

    // 1) Random first + last name
    var firstName = FirstNames[rnd.Next(FirstNames.Length)];
    var familyName = LastNames[rnd.Next(LastNames.Length)];

    // 2) Email: FirstName.LastName@dtu.dk (lowercase)
    var email = $"{firstName}.{familyName}@dtu.dk".ToLowerInvariant();

    // 3) Random MeetingDateTime from today +- 15 days, in half-hour intervals
    var now = DateTimeOffset.Now;
    var dayStart = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, now.Offset);

    var dayDelta = rnd.Next(-15, 16);        // -15..+15
    var halfHourSlot = rnd.Next(0, 48);      // 0..47 => 00:00, 00:30, ..., 23:30
    var meetingDateTime = dayStart
        .AddDays(dayDelta)
        .AddMinutes(halfHourSlot * 30);

    // 4) Duration: 1-3 or null
    int? duration = rnd.Next(0, 4) switch
    {
        0 => null,
        1 => 1,
        2 => 2,
        3 => 3,
        _ => null
    };

    return Results.Ok(new GuestRegistration(
        FirstName: firstName,
        FamilyName: familyName,
        Email: email,
        MeetingDateTime: meetingDateTime,
        Duration: duration
    ));
});

public record GuestRegistration(
    string FirstName,
    string FamilyName,
    string Email,
    DateTimeOffset MeetingDateTime,
    int? Duration
);