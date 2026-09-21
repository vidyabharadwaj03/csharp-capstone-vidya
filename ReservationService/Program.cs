using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ReservationService.BackgroundJobs;
using ReservationService.Clients;
using ReservationService.Data;
using ReservationService.Dtos;
using ReservationService.Options;
using ReservationService.Services;
using ReservationService.Validation;

var errorJsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper));
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Reservation Service API", Version = "v1" });
});

builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<ReturnRequestValidator>();

builder.Services.Configure<ServiceUrlsOptions>(builder.Configuration.GetSection("ServiceUrls"));

var serviceUrls = builder.Configuration.GetSection("ServiceUrls").Get<ServiceUrlsOptions>() ?? new ServiceUrlsOptions();

builder.Services.AddHttpClient<UserServiceClient>(client =>
{
    client.BaseAddress = new Uri(serviceUrls.UserService);
});

builder.Services.AddHttpClient<CatalogServiceClient>(client =>
{
    client.BaseAddress = new Uri(serviceUrls.CatalogService);
});

builder.Services.AddScoped<WaitlistCascadeService>();
builder.Services.AddHostedService<WaitlistExpiryBackgroundService>();

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddDbContext<ReservationServiceContext>(options =>
        options.UseInMemoryDatabase("ReservationServiceDb"));
}
else
{
    var connectionString = builder.Configuration.GetConnectionString("ReservationDb");
    builder.Services.AddDbContext<ReservationServiceContext>(options =>
        options.UseNpgsql(connectionString));
}

var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Secret))
        };

        options.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";
                var body = JsonSerializer.Serialize(new ApiErrorResponse
                {
                    Error = "UNAUTHORIZED",
                    Message = "Authentication required"
                }, errorJsonOptions);
                await context.Response.WriteAsync(body);
            },
            OnForbidden = async context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json";
                var body = JsonSerializer.Serialize(new ApiErrorResponse
                {
                    Error = "FORBIDDEN",
                    Message = "You do not have permission to access this resource"
                }, errorJsonOptions);
                await context.Response.WriteAsync(body);
            }
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        var body = JsonSerializer.Serialize(new ApiErrorResponse
        {
            Error = "INTERNAL_SERVER_ERROR",
            Message = "An unexpected error occurred"
        }, errorJsonOptions);
        await context.Response.WriteAsync(body);
    });
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

if (!app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<ReservationServiceContext>().Database.Migrate();
}

app.Run();

public partial class Program;
