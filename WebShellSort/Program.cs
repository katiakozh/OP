using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;
using BCrypt.Net;
using System.Text.RegularExpressions;
using System.Collections.Generic;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
{
	options.UseSqlite("Data Source=shellsort.db");
});

builder.Services.AddControllers().AddJsonOptions(opts =>
{
	opts.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
	var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
	db.Database.EnsureCreated();
}

if (app.Environment.IsDevelopment())
{
	app.UseSwagger();
	app.UseSwaggerUI();
}

app.UseHttpsRedirection();

static string? ExtractBearerToken(string authHeader)
{
	if (string.IsNullOrEmpty(authHeader)) return null;
	var match = Regex.Match(authHeader, @"Bearer\s+(.*)", RegexOptions.IgnoreCase);
	if (match.Success) return match.Groups[1].Value.Trim();
	return null;
}

void SaveRequestHistory(AppDbContext db, int userId, string endpoint)
{
	var record = new RequestHistory
	{
		UserId = userId,
		Endpoint = endpoint,
		Timestamp = DateTime.UtcNow
	};
	db.RequestHistories.Add(record);
	db.SaveChanges();
}

async Task<User?> GetCurrentUser(AppDbContext db, HttpRequest req)
{
	var authHeader = req.Headers["Authorization"].ToString();
	var token = ExtractBearerToken(authHeader);
	if (string.IsNullOrWhiteSpace(token)) return null;

	var user = await db.Users.FirstOrDefaultAsync(u => u.Token == token);
	return user;
}

static int[] ShellSort(int[] array)
{

	int n = array.Length;

	int gap = n / 2;

	while (gap > 0)
	{
		for (int i = gap; i < n; i++)
		{
			int temp = array[i];
			int j = i;
			while (j >= gap && array[j - gap] > temp)
			{
				array[j] = array[j - gap];
				j -= gap;
			}
			array[j] = temp;
		}
		gap /= 2; 
	}
	return array;
}

app.MapPost("/register", async (AppDbContext db, string username, string password) =>
{
	if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
		return Results.BadRequest(new { error = "Username and password required" });

	var existingUser = await db.Users.FirstOrDefaultAsync(u => u.Username == username);
	if (existingUser != null)
		return Results.BadRequest(new { error = "User already exists" });

	var hash = BCrypt.Net.BCrypt.HashPassword(password);
	var token = AuthUtils.GenerateToken();

	var user = new User
	{
		Username = username,
		PasswordHash = hash,
		Token = token
	};
	db.Users.Add(user);
	await db.SaveChangesAsync();

	return Results.Created("/register", new
	{
		message = "User registered successfully",
		token = user.Token
	});
});

app.MapPost("/login", async (AppDbContext db, string username, string password) =>
{
	var user = await db.Users.FirstOrDefaultAsync(u => u.Username == username);
	if (user == null)
		return Results.Unauthorized();

	if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
		return Results.Unauthorized();

	user.Token = AuthUtils.GenerateToken();
	await db.SaveChangesAsync();

	return Results.Ok(new
	{
		message = "Login successful",
		token = user.Token
	});
});

app.MapMethods("/change_password", new[] { "PATCH" }, async (AppDbContext db, HttpRequest req, string newPassword) =>
{
	var currentUser = await GetCurrentUser(db, req);
	if (currentUser == null) return Results.Unauthorized();

	if (string.IsNullOrWhiteSpace(newPassword))
		return Results.BadRequest(new { error = "New password is required" });

	currentUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);

	currentUser.Token = AuthUtils.GenerateToken();
	await db.SaveChangesAsync();

	SaveRequestHistory(db, currentUser.Id, "PATCH /change_password");

	return Results.Ok(new
	{
		message = "Password changed",
		newToken = currentUser.Token
	});
});

app.MapPost("/array/create", async (AppDbContext db, HttpRequest req, int size) =>
{
	var currentUser = await GetCurrentUser(db, req);
	if (currentUser == null) return Results.Unauthorized();

	if (size <= 0)
		return Results.BadRequest(new { error = "Size must be > 0" });

	var rand = new Random();
	var array = new int[size];
	for (int i = 0; i < size; i++)
	{
		array[i] = rand.Next(0, 100); 
	}
	var arrayJson = System.Text.Json.JsonSerializer.Serialize(array);
	var userArray = await db.UserArrays.FirstOrDefaultAsync(a => a.UserId == currentUser.Id);
	if (userArray == null)
	{
		userArray = new UserArray
		{
			UserId = currentUser.Id,
			ArrayData = arrayJson
		};
		db.UserArrays.Add(userArray);
	}
	else
	{
		userArray.ArrayData = arrayJson;
	}

	await db.SaveChangesAsync();

	SaveRequestHistory(db, currentUser.Id, "POST /array/create");

	return Results.Ok(new
	{
		message = "Array created/updated",
		array = array
	});
});

app.MapMethods("/array", new[] { "PATCH" }, async (AppDbContext db, HttpRequest req, int? newSize, int[]? newValues) =>
{

	var currentUser = await GetCurrentUser(db, req);
	if (currentUser == null) return Results.Unauthorized();

	var userArray = await db.UserArrays.FirstOrDefaultAsync(a => a.UserId == currentUser.Id);
	if (userArray == null)
	{
		return Results.NotFound(new { error = "Array not found. Create an array first (/array/create)" });
	}

	var array = System.Text.Json.JsonSerializer.Deserialize<int[]>(userArray.ArrayData) ?? new int[0];

	bool updated = false;

	if (newSize.HasValue && newSize > 0)
	{
		var rand = new Random();
		var newArray = new int[newSize.Value];
		for (int i = 0; i < newSize.Value; i++)
		{
			newArray[i] = rand.Next(0, 100);
		}
		array = newArray;
		updated = true;
	}

	if (newValues != null && newValues.Length > 0)
	{
		array = newValues;
		updated = true;
	}

	if (!updated)
	{
		return Results.BadRequest(new { message = "Nothing to update. Provide newSize>0 or newValues." });
	}

	userArray.ArrayData = System.Text.Json.JsonSerializer.Serialize(array);
	await db.SaveChangesAsync();

	SaveRequestHistory(db, currentUser.Id, "PATCH /array");

	return Results.Ok(new
	{
		message = "Array updated",
		array = array
	});
});

app.MapGet("/array", async (AppDbContext db, HttpRequest req) =>
{
	var currentUser = await GetCurrentUser(db, req);
	if (currentUser == null) return Results.Unauthorized();

	var userArray = await db.UserArrays.FirstOrDefaultAsync(a => a.UserId == currentUser.Id);
	if (userArray == null)
		return Results.NotFound(new { error = "Array not found" });

	var array = System.Text.Json.JsonSerializer.Deserialize<int[]>(userArray.ArrayData) ?? new int[0];

	SaveRequestHistory(db, currentUser.Id, "GET /array");

	return Results.Ok(new { array = array });
});

app.MapPost("/array/sort/shell", async (AppDbContext db, HttpRequest req) =>
{
	var currentUser = await GetCurrentUser(db, req);
	if (currentUser == null) return Results.Unauthorized();

	var userArray = await db.UserArrays.FirstOrDefaultAsync(a => a.UserId == currentUser.Id);
	if (userArray == null)
		return Results.NotFound(new { error = "Array not found" });

	var array = System.Text.Json.JsonSerializer.Deserialize<int[]>(userArray.ArrayData) ?? new int[0];

	var sorted = ShellSort(array);

	userArray.ArrayData = System.Text.Json.JsonSerializer.Serialize(sorted);
	await db.SaveChangesAsync();

	SaveRequestHistory(db, currentUser.Id, "POST /array/sort/shell");

	return Results.Ok(new
	{
		message = "Shell sort completed",
		array = sorted
	});
});

app.MapGet("/requests_history", async (AppDbContext db, HttpRequest req) =>
{
	var currentUser = await GetCurrentUser(db, req);
	if (currentUser == null) return Results.Unauthorized();

	var history = await db.RequestHistories
		.Where(r => r.UserId == currentUser.Id)
		.OrderByDescending(r => r.Timestamp)
		.ToListAsync();

	SaveRequestHistory(db, currentUser.Id, "GET /requests_history");

	return Results.Ok(history.Select(h => new
	{
		h.Id,
		h.Endpoint,
		h.Timestamp
	}));
});

app.MapDelete("/requests_history", async (AppDbContext db, HttpRequest req) =>
{
	var currentUser = await GetCurrentUser(db, req);
	if (currentUser == null) return Results.Unauthorized();

	var toDelete = db.RequestHistories.Where(r => r.UserId == currentUser.Id);
	db.RequestHistories.RemoveRange(toDelete);
	await db.SaveChangesAsync();

	SaveRequestHistory(db, currentUser.Id, "DELETE /requests_history");

	return Results.Ok(new { message = "Request history deleted" });
});

app.Run();

public class AppDbContext : DbContext
{
	public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
	public DbSet<User> Users => Set<User>();
	public DbSet<RequestHistory> RequestHistories => Set<RequestHistory>();
	public DbSet<UserArray> UserArrays => Set<UserArray>();
}

public class User
{
	public int Id { get; set; }
	public string Username { get; set; } = null!;
	public string PasswordHash { get; set; } = null!;
	public string Token { get; set; } = null!;
}

public class RequestHistory
{
	public int Id { get; set; }
	public int UserId { get; set; }
	public string Endpoint { get; set; } = null!;
	public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class UserArray
{
	public int Id { get; set; }
	public int UserId { get; set; }

	public string ArrayData { get; set; } = "[]";
}

static class AuthUtils
{
	public static string GenerateToken() => Guid.NewGuid().ToString("N");
}

public partial class Program { }