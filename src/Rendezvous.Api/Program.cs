using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Rendezvous.Api.Authentication;
using Rendezvous.Api.Email;
using Rendezvous.Api.Services;
using Rendezvous.Api.Swagger;
using Rendezvous.Infrastructure.Identity;
using Rendezvous.Infrastructure.Persistence;
using Resend;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not found.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));
builder.Services
    .AddIdentityCore<ApplicationUser>(options =>
    {
        options.Password.RequiredLength = 8;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = false;
        options.Password.RequireDigit = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequiredUniqueChars = 1;
    })
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<AppDbContext>();
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection("Email"));
builder.Services.Configure<BusinessPhotoStorageOptions>(builder.Configuration.GetSection("Media"));
builder.Services.Configure<ResendClientOptions>(options =>
{
    options.ApiToken = builder.Configuration["Email:Resend:ApiKey"] ?? string.Empty;
});
builder.Services.AddHttpClient<ResendClient>();
builder.Services.AddTransient<IResend, ResendClient>();
if (builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddSingleton<InMemoryEmailSender>();
    builder.Services.AddSingleton<IEmailSender>(provider =>
        provider.GetRequiredService<InMemoryEmailSender>());
}
else if (string.Equals(
    builder.Configuration["Email:Provider"],
    "Resend",
    StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddScoped<IEmailSender, ResendEmailSender>();
}
else
{
    builder.Services.AddScoped<IEmailSender, DisabledEmailSender>();
}
builder.Services.AddScoped<AuthTokenService>();
builder.Services.AddScoped<AppointmentExpirationService>();
builder.Services.AddScoped<InvitationTokenService>();
builder.Services.AddScoped<PublicNumberGenerator>();
builder.Services.AddScoped<AvailabilityExceptionService>();
builder.Services.AddScoped<BusinessProvisioningService>();
builder.Services.AddScoped<NotificationWriter>();
builder.Services.AddScoped<EmailConfirmationService>();
builder.Services.AddScoped<AppointmentEmailService>();
builder.Services.AddScoped<BusinessPhotoStorageService>();
builder.Services.AddScoped<ApplicationRoleSeeder>();
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>()
            ?? throw new InvalidOperationException("Jwt configuration was not found.");

        if (string.IsNullOrWhiteSpace(jwtOptions.SigningKey))
        {
            throw new InvalidOperationException("Jwt signing key was not found.");
        }

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });
builder.Services.AddControllers();
builder.Services.AddAuthorization();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter a JWT access token."
    });
    options.OperationFilter<AuthorizeOperationFilter>();
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var roleSeeder = scope.ServiceProvider.GetRequiredService<ApplicationRoleSeeder>();
    await roleSeeder.SeedAsync();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await DevelopmentDataSeeder.SeedAsync(dbContext, app.Configuration);

    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program;
