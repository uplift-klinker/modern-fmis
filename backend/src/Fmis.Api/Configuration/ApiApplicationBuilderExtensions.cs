using Fmis.Core;
using Microsoft.EntityFrameworkCore;

namespace Fmis.Api.Configuration;

public static class ApiApplicationBuilderExtensions
{
    public static WebApplication UseApiPipeline(this WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.MapOpenApi();
        app.UseCors("Spa");
        app.UseAuthentication();
        app.UseAuthorization();
        return app;
    }

    public static WebApplication MapApiEndpoints(this WebApplication app)
    {
        app.MapControllers();
        return app;
    }

    public static WebApplication MigrateDatabase(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FmisDbContext>();
        if (db.Database.IsRelational())
        {
            db.Database.Migrate();
        }
        return app;
    }
}
