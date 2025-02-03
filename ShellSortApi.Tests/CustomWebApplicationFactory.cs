using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
namespace ShellSortApi.Tests
{
	public class CustomWebApplicationFactory : WebApplicationFactory<Program>
	{
		private SqliteConnection _connection;

		protected override IHost CreateHost(IHostBuilder builder)
		{
			_connection = new SqliteConnection("DataSource=:memory:");
			_connection.Open();

			builder.ConfigureServices(services =>
			{
				services.RemoveAll(typeof(DbContextOptions<AppDbContext>));

				services.AddDbContext<AppDbContext>(options =>
				{
					options.UseSqlite(_connection);
				});
			});

			return base.CreateHost(builder);
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);
			if (disposing)
			{
				_connection.Close();
				_connection.Dispose();
			}
		}
	}
}
