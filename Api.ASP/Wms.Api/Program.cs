using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Serilog;
using Wms.Api.Infrastructure;
using Wms.Application;
using Wms.Infrastructure;
using Wms.Infrastructure.Data;
using Wms.Infrastructure.Identity;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration));

builder.Services.AddApplication();

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddAuthorizationBuilder()
   .SetFallbackPolicy(new AuthorizationPolicyBuilder()
       .RequireAuthenticatedUser()
       .Build());

builder.Services.AddProblemDetails(options => 
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Instance = 
            $"{context.HttpContext.Request.Method} {context.HttpContext.Request.Path}";

        context.ProblemDetails.Extensions.TryAdd("requestId", context.HttpContext.TraceIdentifier);

        var activity = context.HttpContext.Features.Get<IHttpActivityFeature>()?.Activity;
        context.ProblemDetails.Extensions.TryAdd("traceId", activity?.Id);
    };
});

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

var app = builder.Build();

if (args.Contains("seed", StringComparer.OrdinalIgnoreCase))
{
    using var maintenanceScope = app.Services.CreateScope();
    var dbContext = maintenanceScope.ServiceProvider.GetRequiredService<AppDbContext>();
    await WarehouseSeeder.SeedAsync(dbContext);
    return;
}

if (args.Contains("truncate", StringComparer.OrdinalIgnoreCase))
{
    using var maintenanceScope = app.Services.CreateScope();
    var dbContext = maintenanceScope.ServiceProvider.GetRequiredService<AppDbContext>();
    await WarehouseCleaner.ClearAsync(dbContext);
    return;
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseSerilogRequestLogging();

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
    var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();

    await RoleSeeder.SeedAsync(roleManager);
    await AdminSeeder.SeedAsync(userManager, config);
}

app.Run();

