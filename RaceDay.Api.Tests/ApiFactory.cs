using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RaceDay.Api.Data;
namespace RaceDay.Api.Tests;
public class ApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        connection.Open();
        builder.UseEnvironment("Development");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<RaceDayDbContext>>();
            services.RemoveAll<RaceDayDbContext>();
            services.AddDbContext<RaceDayDbContext>(o => o.UseSqlite(connection));
        });
    }
    public HttpClient Client()
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        using var scope = Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<RaceDayDbContext>().Database.EnsureCreated();
        return client;
    }
    public async Task Database(Func<RaceDayDbContext,Task> action)
    {
        using var scope = Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<RaceDayDbContext>());
    }
    protected override void Dispose(bool disposing)
    { base.Dispose(disposing); if (disposing) connection.Dispose(); }
}
