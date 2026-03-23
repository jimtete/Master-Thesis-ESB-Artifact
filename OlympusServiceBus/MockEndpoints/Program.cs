using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// Swagger/OpenAPI (UI at /swagger)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "MockEndpoints",
        Version = "v1"
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "MockEndpoints v1");
        c.RoutePrefix = "swagger"; // default, explicit for clarity
    });
}

// If you only run HTTP locally and get the HTTPS warning, either remove this
// or configure https in launchSettings.json.
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

    var firstName = FirstNames[rnd.Next(FirstNames.Length)];
    var familyName = LastNames[rnd.Next(LastNames.Length)];

    var email = $"{firstName}.{familyName}@dtu.dk".ToLowerInvariant();

    var now = DateTimeOffset.Now;
    var dayStart = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, now.Offset);

    var dayDelta = rnd.Next(-15, 16);   // -15..+15
    var halfHourSlot = rnd.Next(18, 34); // 9...17
    var meetingDateTime = dayStart
        .AddDays(dayDelta)
        .AddMinutes(halfHourSlot * 30);

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
        RegisteredBy: "dte",
        MeetingDateTime: meetingDateTime,
        Duration: duration
    ));
})
.WithName("GetRandomGuestRegistration")
.WithTags("Mock Data")
.Produces<GuestRegistration>(StatusCodes.Status200OK)
.WithOpenApi();


// app.MapGet("/get-random-guest-registration-full-name", () =>
//     {
//         var meetingDateTime = new DateTimeOffset(2026, 3, 7, 10, 0, 0, TimeSpan.Zero);
//
//         return Results.Ok(new GuestRegistrationFullName(
//             FullName: "Dimitrios Damascus",
//             Email: "dimitrios.tetepoulidis@dtu.dk",
//             RegisteredBy: "dte",
//             MeetingDateTime: meetingDateTime,
//             Duration: 2
//         ));
//     })
//     .WithName("GetRandomGuestRegistrationFullName")
//     .WithTags("Mock Data")
//     .Produces<GuestRegistration>(StatusCodes.Status200OK)
//     .WithOpenApi();

app.MapGet("/get-random-guest-registration-full-name", () =>
    {
        var rnd = Random.Shared;

        var firstName = FirstNames[rnd.Next(FirstNames.Length)];
        var familyName = LastNames[rnd.Next(LastNames.Length)];
        
        var fullName = firstName + " " + familyName;

        var email = $"{firstName}.{familyName}@dtu.dk".ToLowerInvariant();

        var now = DateTimeOffset.Now;
        var dayStart = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, now.Offset);

        var dayDelta = rnd.Next(-15, 16);   // -15..+15
        var halfHourSlot = rnd.Next(18, 34); // 9...17
        var meetingDateTime = dayStart
            .AddDays(dayDelta)
            .AddMinutes(halfHourSlot * 30);

        int? duration = rnd.Next(0, 4) switch
        {
            0 => null,
            1 => 1,
            2 => 2,
            3 => 3,
            _ => null
        };

        return Results.Ok(new GuestRegistrationFullName(
            FullName: fullName,
            Email: email,
            RegisteredBy: "dte",
            MeetingDateTime: meetingDateTime,
            Duration: duration
        ));
    })
    .WithName("GetRandomGuestRegistrationFullName")
    .WithTags("Mock Data")
    .Produces<GuestRegistration>(StatusCodes.Status200OK)
    .WithOpenApi();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
   .WithTags("Health")
   .WithOpenApi();

app.MapPost("/guest-registration-status", (
        GuestRegistrationStatusCallback callback,
        ILogger<Program> logger) =>
    {
        logger.LogInformation(
            "Received guest registration status callback. EmailAddress: {EmailAddress}, MeetingTime: {MeetingTime}, BusinessKey: {BusinessKey}, ExecutionStatus: {ExecutionStatus}, Status: {Status}, ErrorMessage: {ErrorMessage}",
            callback.EmailAddress,
            callback.MeetingTime,
            callback.BusinessKey,
            callback.ExecutionStatus,
            callback.Status,
            callback.ErrorMessage);

        return Results.Ok(new
        {
            received = true,
            message = "Guest registration status callback accepted",
            callback.BusinessKey,
            callback.ExecutionStatus,
            receivedAtUtc = DateTime.UtcNow
        });
    })
    .WithName("GuestRegistrationStatus")
    .WithTags("Mock Callbacks")
    .Produces(StatusCodes.Status200OK)
    .WithOpenApi();



app.Run();

public record GuestRegistrationStatusCallback(
    string? Source,
    string? MessageType,
    string? EmailAddress,
    DateTimeOffset? MeetingTime,
    string? BusinessKey,
    string? SourceContractId,
    string? ExecutionStatus,
    string? ErrorMessage,
    string? ErrorCode,
    DateTimeOffset? CompletedAtUtc,
    string? Status
);


public record GuestRegistration(
    string FirstName,
    string FamilyName,
    string Email,
    string RegisteredBy,
    DateTimeOffset MeetingDateTime,
    int? Duration
);

public record GuestRegistrationFullName(
    string FullName,
    string Email,
    string RegisteredBy,
    DateTimeOffset MeetingDateTime,
    int? Duration
);
