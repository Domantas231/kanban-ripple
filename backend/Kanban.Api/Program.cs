using FluentValidation;
using System.Text.Json.Serialization;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.CookiePolicy;
using Microsoft.EntityFrameworkCore;
using Kanban.Api.Configuration;
using Kanban.Api.Data;
using Kanban.Api.Hubs;
using Kanban.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    });
builder.Services.AddApiBehavior();
builder.Services.AddProjectSignalR(builder.Configuration);

builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

builder.Services.AddTypedOptions(builder.Configuration);
builder.Services.AddDatabase(builder.Configuration);
builder.Services.AddJwtAuth(builder.Configuration);
builder.Services.AddFrontendCors(builder.Configuration);
builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();

builder.Services.AddEmail(builder.Configuration);
builder.Services.AddFileStorage(builder.Configuration);
builder.Services.AddDomainServices();
builder.Services.AddGoogleIntegrations();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>("database");

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    dbContext.Database.Migrate();
}

app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseHttpsRedirection();

app.UseCookiePolicy(new CookiePolicyOptions
{
    HttpOnly = HttpOnlyPolicy.Always,
    Secure = app.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always,
    MinimumSameSitePolicy = app.Environment.IsDevelopment() ? SameSiteMode.Lax : SameSiteMode.None
});

app.UseCors("Frontend");

app.UseAuthentication();
app.UseMiddleware<AccessTokenBlocklistMiddleware>();
app.UseAuthorization();

app.MapControllers();
app.MapHub<ProjectHub>("/hubs/project");
app.MapHealthChecks("/health");

app.Run();

public partial class Program;
