using System.Text.Json;
using System.Text.Json.Serialization;
using CatalogService.Data;
using CatalogService.Dtos;
using Microsoft.EntityFrameworkCore;

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
    c.SwaggerDoc("v1", new() { Title = "Catalog Service API", Version = "v1" });
});

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddDbContext<CatalogServiceContext>(options =>
        options.UseInMemoryDatabase("CatalogServiceDb"));
}
else
{
    var connectionString = builder.Configuration.GetConnectionString("CatalogDb");
    builder.Services.AddDbContext<CatalogServiceContext>(options =>
        options.UseNpgsql(connectionString));
}

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

app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<CatalogServiceContext>();
    if (!app.Environment.IsDevelopment())
    {
        context.Database.Migrate();
    }
    else
    {
        await DataSeeder.SeedAsync(context);
    }
}

app.Run();
