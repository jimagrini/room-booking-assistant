using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using RoomBooking.Api.Assistant;
using RoomBooking.Api.Authentication;
using RoomBooking.Api.Errors;
using RoomBooking.Application.Abstractions;
using RoomBooking.Application.Bookings;
using RoomBooking.Infrastructure;
using RoomBooking.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration
    .GetConnectionString("RoomBooking")
    ?? throw new InvalidOperationException(
        "Connection string 'RoomBooking' is required.");

var jwtOptions = builder.Configuration
    .GetSection(JwtOptions.SectionName)
    .Get<JwtOptions>()
    ?? throw new InvalidOperationException(
        "JWT configuration is required.");
var challengeUserOptions = builder.Configuration
    .GetSection(ChallengeUserOptions.SectionName)
    .Get<ChallengeUserOptions>()
    ?? throw new InvalidOperationException(
        "Challenge user configuration is required.");

if (string.IsNullOrWhiteSpace(jwtOptions.SigningKey)
    || jwtOptions.SigningKey.Length < 32)
{
    throw new InvalidOperationException(
        "JWT signing key must contain at least 32 characters.");
}
if (string.IsNullOrWhiteSpace(jwtOptions.Issuer)
    || string.IsNullOrWhiteSpace(jwtOptions.Audience)
    || jwtOptions.LifetimeMinutes <= 0)
{
    throw new InvalidOperationException(
        "JWT issuer, audience, and a positive lifetime are required.");
}
if (string.IsNullOrWhiteSpace(challengeUserOptions.Password))
{
    throw new InvalidOperationException(
        "Challenge user password is required.");
}

var roomSeeds = builder.Configuration
    .GetSection("RoomSeeds")
    .Get<RoomSeedDefinition[]>()
    ?? throw new InvalidOperationException(
        "Room seed configuration is required.");

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddInfrastructure(connectionString);
builder.Services.AddScoped<IBookingService, BookingService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<
    IReadOnlyCollection<RoomSeedDefinition>>(roomSeeds);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddSingleton<
    IPasswordHasher<ChallengeUser>,
    PasswordHasher<ChallengeUser>>();
builder.Services.AddSingleton<IChallengeUserStore, ChallengeUserStore>();
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();
builder.Services.Configure<JwtOptions>(
    builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<ChallengeUserOptions>(
    builder.Configuration.GetSection(
        ChallengeUserOptions.SectionName));

builder.Services.Configure<OpenAiOptions>(
    builder.Configuration.GetSection(OpenAiOptions.SectionName));
builder.Services.AddHttpClient<
        IOpenAiResponsesClient,
        OpenAiResponsesClient>()
    .ConfigureHttpClient(client =>
        client.Timeout = TimeSpan.FromSeconds(60));
builder.Services.AddSingleton<AssistantConversationStore>();
builder.Services.AddScoped<
    IAssistantToolExecutor,
    AssistantToolExecutor>();
builder.Services.AddScoped<IAssistantService, AssistantService>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtOptions.Issuer,
                ValidateAudience = true,
                ValidAudience = jwtOptions.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30)
            };
    });
builder.Services.AddAuthorization();

var allowedOrigin = builder.Configuration["Cors:AllowedOrigin"]
    ?? "http://localhost:5173";
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(allowedOrigin)
            .AllowAnyHeader()
            .AllowAnyMethod());
});

var app = builder.Build();
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet(
        "/health",
        () => Results.Ok(new { status = "healthy" }))
    .AllowAnonymous();

await app.Services.InitializeDatabaseAsync(roomSeeds);
app.Run();

public partial class Program;
