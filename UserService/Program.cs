using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using UserService.Data;
using UserService.Dtos;
using UserService.Options;
using UserService.Services;
using UserService.Validation;

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
    c.SwaggerDoc("v1", new() { Title = "User Service API", Version = "v1" });
});

builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<ServiceUrlsOptions>(builder.Configuration.GetSection("ServiceUrls"));

builder.Services.AddSingleton<PasswordHasher>();
builder.Services.AddSingleton<JwtTokenService>();

builder.Services.AddHttpClient<ReservationServiceClient>((sp, client) =>
{
    var serviceUrls = builder.Configuration.GetSection("ServiceUrls").Get<ServiceUrlsOptions>() ?? new ServiceUrlsOptions();
    client.BaseAddress = new Uri(serviceUrls.ReservationService);
});

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddDbContext<UserServiceContext>(options =>
        options.UseInMemoryDatabase("UserServiceDb"));
}
else
{
    var connectionString = builder.Configuration.GetConnectionString("UserDb");
    builder.Services.AddDbContext<UserServiceContext>(options =>
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

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<UserServiceContext>();
    if (!app.Environment.IsDevelopment())
    {
        context.Database.Migrate();
    }
    else
    {
        var passwordHasher = scope.ServiceProvider.GetRequiredService<PasswordHasher>();
        await DataSeeder.SeedAsync(context, passwordHasher);
    }
}

app.Run();

public partial class Program;
