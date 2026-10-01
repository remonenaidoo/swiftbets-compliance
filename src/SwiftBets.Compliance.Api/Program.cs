using SwiftBets.BuildingBlocks.Observability;
using SwiftBets.BuildingBlocks.Web;
using SwiftBets.Compliance.Api.Endpoints;
using SwiftBets.Compliance.Application;
using SwiftBets.Compliance.Infrastructure;

if (HealthProbe.TryRun(args) is { } probeExitCode)
{
    return probeExitCode;
}

var builder = WebApplication.CreateBuilder(args);
builder.AddSwiftBetsObservability("swiftbets-compliance");
builder.Services.AddSwiftBetsWeb();
builder.Services.AddSwiftBetsJwtBearer(builder.Configuration);
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(CompliancePermissions.Read, p => p.RequireClaim("perm", CompliancePermissions.Read));
builder.Services.AddComplianceApplication();
builder.Services.AddComplianceInfrastructure(builder.Configuration);

var app = builder.Build();
app.UseSwiftBetsObservability();
app.UseSwiftBetsWeb();
app.UseAuthentication();
app.UseAuthorization();
app.MapSwiftBetsOperationalEndpoints();
app.MapComplianceEndpoints();

await app.RunAsync();
return 0;

public partial class Program;
