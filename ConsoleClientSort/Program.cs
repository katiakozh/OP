using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Net.Http.Headers;

namespace ShellSortConsoleClient
{
	class Program
	{
		private static readonly HttpClient _client = new HttpClient();
		private static string _token = string.Empty; 
		static async Task Main(string[] args)
		{
			_client.BaseAddress = new Uri("http://localhost:5062");

			while (true)
			{
				Console.WriteLine();

				if (string.IsNullOrEmpty(_token))
				{
					Console.WriteLine("Меню авторизации ");
					Console.WriteLine("1) Регистрация");
					Console.WriteLine("2) Логин");
					Console.WriteLine("0) Выход");
				}
				else
				{
					Console.WriteLine("Главное меню:");
					Console.WriteLine("1) Сменить пароль");
					Console.WriteLine("2) Создать/перезаписать массив ");
					Console.WriteLine("3) Изменить массив ");
					Console.WriteLine("4) Получить массив ");
					Console.WriteLine("5) Сортировать массив Shell");
					Console.WriteLine("6) Посмотреть историю запросов");
					Console.WriteLine("7) Очистить историю запросов");
					Console.WriteLine("L) Выйти");
					Console.WriteLine("0) Завершить программу");
				}

				Console.Write(">> ");
				var command = Console.ReadLine()?.ToLower().Trim();
				if (command == "exit" || command == "0") break;

				try
				{
					if (string.IsNullOrEmpty(_token))
					{
						switch (command)
						{
							case "1":
							case "register":
								await RegisterAsync();
								break;
							case "2":
							case "login":
								await LoginAsync();
								break;
							default:
								Console.WriteLine("Неизвестная команда (сначала авторизуйтесь).");
								break;
						}
					}
					else
					{
						switch (command)
						{
							case "1":
							case "change_password":
								await ChangePasswordAsync();
								break;
							case "2":
							case "create_array":
								await CreateArrayAsync();
								break;
							case "3":
							case "patch_array":
								await PatchArrayAsync();
								break;
							case "4":
							case "get_array":
								await GetArrayAsync();
								break;
							case "5":
							case "sort_array_shell":
								await SortArrayShellAsync();
								break;
							case "6":
							case "get_requests_history":
								await GetRequestsHistoryAsync();
								break;
							case "7":
							case "delete_requests_history":
								await DeleteRequestsHistoryAsync();
								break;
							case "l":
							case "logout":
								Logout();
								break;
							default:
								Console.WriteLine("Неизвестная команда.");
								break;
						}
					}
				}
				catch (Exception ex)
				{
					Console.WriteLine("Ошибка: " + ex.Message);
				}
			}

			Console.WriteLine("Программа завершена.");
		}
		private static async Task RegisterAsync()
		{
			Console.Write("Имя пользователя (username): ");
			string username = Console.ReadLine() ?? "";

			Console.Write("Пароль (password): ");
			string password = Console.ReadLine() ?? "";

			string url = $"/register?username={Uri.EscapeDataString(username)}&password={Uri.EscapeDataString(password)}";
			var response = await _client.PostAsync(url, null);

			if (response.IsSuccessStatusCode)
			{
				string json = await response.Content.ReadAsStringAsync();
				var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
				if (dict != null)
				{
					if (dict.TryGetValue("message", out var msg))
						Console.WriteLine("Сообщение: " + msg);
					if (dict.TryGetValue("token", out var tokenObj))
					{
						_token = tokenObj?.ToString() ?? "";
						Console.WriteLine("Получен токен: " + _token);
					}
				}
				else
				{
					Console.WriteLine("Не удалось десериализовать ответ сервера при регистрации.");
				}
			}
			else
			{
				Console.WriteLine("Ошибка при регистрации. Код: " + response.StatusCode);
				Console.WriteLine("Ответ: " + await response.Content.ReadAsStringAsync());
			}
		}

		private static async Task LoginAsync()
		{
			Console.Write("Имя пользователя (username): ");
			string username = Console.ReadLine() ?? "";

			Console.Write("Пароль (password): ");
			string password = Console.ReadLine() ?? "";

			string url = $"/login?username={Uri.EscapeDataString(username)}&password={Uri.EscapeDataString(password)}";
			var response = await _client.PostAsync(url, null);

			if (response.IsSuccessStatusCode)
			{
				string json = await response.Content.ReadAsStringAsync();
				var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
				if (dict != null)
				{
					if (dict.TryGetValue("message", out var msg))
						Console.WriteLine("Сообщение: " + msg);
					if (dict.TryGetValue("token", out var tokenObj))
					{
						_token = tokenObj?.ToString() ?? "";
						Console.WriteLine("Получен токен: " + _token);
					}
				}
				else
				{
					Console.WriteLine("Не удалось десериализовать ответ сервера при логине.");
				}
			}
			else
			{
				Console.WriteLine("Ошибка при логине. Код: " + response.StatusCode);
				Console.WriteLine("Ответ: " + await response.Content.ReadAsStringAsync());
			}
		}

		private static void Logout()
		{
			_token = string.Empty;
			Console.WriteLine("Токен очищен. Вы вышли из учётной записи.");
		}

		private static async Task ChangePasswordAsync()
		{
			if (!CheckToken()) return;

			Console.Write("Введите новый пароль: ");
			string newPassword = Console.ReadLine() ?? "";

			_client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);

			string url = $"/change_password?newPassword={Uri.EscapeDataString(newPassword)}";

			var req = new HttpRequestMessage(new HttpMethod("PATCH"), url)
			{
				Content = null
			};

			var response = await _client.SendAsync(req);
			if (response.IsSuccessStatusCode)
			{
				string json = await response.Content.ReadAsStringAsync();
				var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
				if (dict != null)
				{
					if (dict.TryGetValue("message", out var msg))
						Console.WriteLine("Сообщение: " + msg);
					if (dict.TryGetValue("newToken", out var tokObj))
					{
						_token = tokObj?.ToString() ?? "";
						Console.WriteLine("Новый токен: " + _token);
					}
				}
				else
				{
					Console.WriteLine("Не удалось десериализовать ответ сервера при смене пароля.");
				}
			}
			else
			{
				Console.WriteLine("Ошибка при смене пароля. Код: " + response.StatusCode);
				Console.WriteLine("Ответ: " + await response.Content.ReadAsStringAsync());
			}
		}

		private static async Task CreateArrayAsync()
		{
			if (!CheckToken()) return;

			Console.Write("Введите размер массива (size): ");
			string sizeStr = Console.ReadLine() ?? "0";

			if (!int.TryParse(sizeStr, out int size) || size < 1)
			{
				Console.WriteLine("Некорректный размер массива.");
				return;
			}

			_client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);

			string url = $"/array/create?size={size}";
			var response = await _client.PostAsync(url, null);

			if (response.IsSuccessStatusCode)
			{
				string json = await response.Content.ReadAsStringAsync();
				var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
				if (dict != null)
				{
					if (dict.TryGetValue("message", out var msg))
						Console.WriteLine("Сообщение: " + msg);
					if (dict.TryGetValue("array", out var arrObj))
					{
						Console.WriteLine("Массив создан:");
						Console.WriteLine(FormatArrayObject(arrObj));
					}
				}
				else
				{
					Console.WriteLine("Не удалось десериализовать ответ (create array).");
				}
			}
			else
			{
				Console.WriteLine("Ошибка при создании массива. Код: " + response.StatusCode);
				Console.WriteLine("Ответ: " + await response.Content.ReadAsStringAsync());
			}
		}

		private static async Task PatchArrayAsync()
		{
			if (!CheckToken()) return;

			Console.Write("Новый размер массива (newSize, 0 - пропустить): ");
			string newSizeStr = Console.ReadLine() ?? "0";

			Console.Write("Введите новые значения (через запятую) или оставьте пустым, чтобы пропустить: ");
			var newValuesStr = Console.ReadLine()?.Trim();

			string queryParams = "";
			if (int.TryParse(newSizeStr, out int newSize) && newSize > 0)
			{
				queryParams += $"newSize={newSize}";
			}

			if (!string.IsNullOrWhiteSpace(newValuesStr))
			{

				var parts = newValuesStr.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
				foreach (var p in parts)
				{
					int val;
					if (int.TryParse(p.Trim(), out val))
					{
						if (!string.IsNullOrEmpty(queryParams))
							queryParams += "&";
						queryParams += $"newValues={val}";
					}
				}
			}

			if (!string.IsNullOrEmpty(queryParams))
				queryParams = "?" + queryParams;

			_client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);

			string url = "/array" + queryParams;
			var req = new HttpRequestMessage(new HttpMethod("PATCH"), url);

			var response = await _client.SendAsync(req);
			if (response.IsSuccessStatusCode)
			{
				string json = await response.Content.ReadAsStringAsync();
				var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
				if (dict != null)
				{
					if (dict.TryGetValue("message", out var msg))
						Console.WriteLine("Сообщение: " + msg);
					if (dict.TryGetValue("array", out var arrObj))
					{
						Console.WriteLine("Обновлённый массив:");
						Console.WriteLine(FormatArrayObject(arrObj));
					}
				}
				else
				{
					Console.WriteLine("Не удалось десериализовать ответ (patch array).");
				}
			}
			else
			{
				Console.WriteLine("Ошибка при PATCH массива. Код: " + response.StatusCode);
				Console.WriteLine("Ответ: " + await response.Content.ReadAsStringAsync());
			}
		}

		private static async Task GetArrayAsync()
		{
			if (!CheckToken()) return;

			_client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);

			string url = "/array";
			var response = await _client.GetAsync(url);

			if (response.IsSuccessStatusCode)
			{
				string json = await response.Content.ReadAsStringAsync();
				var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
				if (dict != null && dict.TryGetValue("array", out var arrObj))
				{
					Console.WriteLine("=== Текущий массив ===");
					Console.WriteLine(FormatArrayObject(arrObj));
				}
				else
				{
					Console.WriteLine("Не удалось десериализовать ответ (get array) или 'array' не найден в данных.");
				}
			}
			else
			{
				Console.WriteLine("Ошибка при получении массива. Код: " + response.StatusCode);
				Console.WriteLine("Ответ: " + await response.Content.ReadAsStringAsync());
			}
		}

		private static async Task SortArrayShellAsync()
		{
			if (!CheckToken()) return;

			_client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);

			string url = "/array/sort/shell";
			var response = await _client.PostAsync(url, null);

			if (response.IsSuccessStatusCode)
			{
				string json = await response.Content.ReadAsStringAsync();
				var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
				if (dict != null)
				{
					if (dict.TryGetValue("message", out var msg))
						Console.WriteLine("Сообщение: " + msg);
					if (dict.TryGetValue("array", out var arrObj))
					{
						Console.WriteLine("Отсортированный массив:");
						Console.WriteLine(FormatArrayObject(arrObj));
					}
				}
				else
				{
					Console.WriteLine("Не удалось десериализовать ответ (sort array).");
				}
			}
			else
			{
				Console.WriteLine("Ошибка при сортировке массива. Код: " + response.StatusCode);
				Console.WriteLine("Ответ: " + await response.Content.ReadAsStringAsync());
			}
		}

		private static async Task GetRequestsHistoryAsync()
		{
			if (!CheckToken()) return;

			_client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);

			string url = "/requests_history";
			var response = await _client.GetAsync(url);

			if (response.IsSuccessStatusCode)
			{
				string json = await response.Content.ReadAsStringAsync();
				var list = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(json);
				if (list == null || list.Count == 0)
				{
					Console.WriteLine("История пустая или не удалось десериализовать ответ.");
					return;
				}

				Console.WriteLine("=== История запросов (последние сначала) ===");
				foreach (var item in list)
				{
					if (item.TryGetValue("id", out var idVal))
						Console.Write($"ID: {idVal}, ");
					if (item.TryGetValue("endpoint", out var endVal))
						Console.Write($"Endpoint: {endVal}, ");
					if (item.TryGetValue("timestamp", out var tsVal))
						Console.WriteLine($"Timestamp: {tsVal}");
				}
			}
			else
			{
				Console.WriteLine("Ошибка при получении истории. Код: " + response.StatusCode);
				Console.WriteLine("Ответ: " + await response.Content.ReadAsStringAsync());
			}
		}

		private static async Task DeleteRequestsHistoryAsync()
		{
			if (!CheckToken()) return;

			_client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);

			string url = "/requests_history";
			var response = await _client.DeleteAsync(url);

			if (response.IsSuccessStatusCode)
			{
				string json = await response.Content.ReadAsStringAsync();
				var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
				if (dict != null && dict.TryGetValue("message", out var msgObj))
				{
					Console.WriteLine("=== История удалена ===");
					Console.WriteLine("Сообщение: " + msgObj);
				}
				else
				{
					Console.WriteLine("История удалена, но не удалось десериализовать ответ.");
				}
			}
			else
			{
				Console.WriteLine("Ошибка при удалении истории. Код: " + response.StatusCode);
				Console.WriteLine("Ответ: " + await response.Content.ReadAsStringAsync());
			}
		}

		private static bool CheckToken()
		{
			if (string.IsNullOrEmpty(_token))
			{
				Console.WriteLine("Ошибка: вы не авторизованы (нет токена).");
				return false;
			}
			return true;
		}

		private static string FormatArrayObject(object arrObj)
		{

			if (arrObj is JsonElement elem && elem.ValueKind == JsonValueKind.Array)
			{
				var intList = new List<int>();
				foreach (var item in elem.EnumerateArray())
				{
					if (item.ValueKind == JsonValueKind.Number && item.TryGetInt32(out int val))
					{
						intList.Add(val);
					}
				}
				return "[" + string.Join(", ", intList) + "]";
			}
			if (arrObj is List<object> listObj)
			{
				var intList = new List<int>();
				foreach (var item in listObj)
				{
					if (item is int i)
						intList.Add(i);
					else
					{
						if (item is string s && int.TryParse(s, out int parsed))
							intList.Add(parsed);
					}
				}
				return "[" + string.Join(", ", intList) + "]";
			}

			return arrObj.ToString() ?? "(unknown array)";
		}
	}
}
