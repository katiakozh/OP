using System.Net;

using System.Text.Json;


namespace ShellSortApi.Tests
{
	public class ApiTests : IClassFixture<CustomWebApplicationFactory>
	{
		private readonly HttpClient _client;

		public ApiTests(CustomWebApplicationFactory factory)
		{
			_client = factory.CreateClient();
		}

		[Fact(DisplayName = "1) Регистрация нового пользователя")]
		public async Task RegisterUser_ShouldReturnCreatedAndToken()
		{
			var username = "testuser";
			var password = "12345";
			var response = await _client.PostAsync(
				$"/register?username={username}&password={password}",
				content: null 
			);
			Assert.Equal(HttpStatusCode.Created, response.StatusCode);
			var json = await response.Content.ReadAsStringAsync();
			using var doc = JsonDocument.Parse(json);
			var root = doc.RootElement;

			Assert.Equal("User registered successfully", root.GetProperty("message").GetString());
			var token = root.GetProperty("token").GetString();
			Assert.False(string.IsNullOrEmpty(token), "Token не должен быть пустым");
		}

		[Fact(DisplayName = "2) Логин зарегистрированного пользователя")]
		public async Task LoginUser_ShouldReturnOkAndNewToken()
		{
			var username = "loginuser";
			var password = "p@ss";
			var registerResp = await _client.PostAsync($"/register?username={username}&password={password}", null);
			registerResp.EnsureSuccessStatusCode();
			var loginResp = await _client.PostAsync($"/login?username={username}&password={password}", null);
			Assert.Equal(HttpStatusCode.OK, loginResp.StatusCode);
			var json = await loginResp.Content.ReadAsStringAsync();
			using var doc = JsonDocument.Parse(json);
			var root = doc.RootElement;
			Assert.Equal("Login successful", root.GetProperty("message").GetString());
			var token = root.GetProperty("token").GetString();
			Assert.False(string.IsNullOrEmpty(token));
		}
		[Fact(DisplayName = "3) Создание массива (Array Create)")]
		public async Task CreateArray_ShouldReturnOkAndArray()
		{
			var username = "arrayuser";
			var password = "arraypass";
			var registerResp = await _client.PostAsync($"/register?username={username}&password={password}", null);
			registerResp.EnsureSuccessStatusCode();

			var registerJson = await registerResp.Content.ReadAsStringAsync();
			using var regDoc = JsonDocument.Parse(registerJson);
			var token = regDoc.RootElement.GetProperty("token").GetString();
			var size = 5;
			var request = new HttpRequestMessage(HttpMethod.Post, $"/array/create?size={size}");
			request.Headers.Add("Authorization", $"Bearer {token}");
			var response = await _client.SendAsync(request);
			Assert.Equal(HttpStatusCode.OK, response.StatusCode);
			var json = await response.Content.ReadAsStringAsync();
			using var doc = JsonDocument.Parse(json);
			var root = doc.RootElement;
			Assert.Equal("Array created/updated", root.GetProperty("message").GetString());
			var arr = root.GetProperty("array").EnumerateArray().Select(e => e.GetInt32()).ToArray();
			Assert.Equal(size, arr.Length); 
		}

		[Fact(DisplayName = "4) Сортировка массива методом Шелла")]
		public async Task SortArray_ShouldReturnSortedArray()
		{
			var username = "sortuser";
			var password = "sortpass";
			var registerResp = await _client.PostAsync($"/register?username={username}&password={password}", null);
			registerResp.EnsureSuccessStatusCode();
			var registerJson = await registerResp.Content.ReadAsStringAsync();
			using var regDoc = JsonDocument.Parse(registerJson);
			var token = regDoc.RootElement.GetProperty("token").GetString();
			int size = 5;
			var createRequest = new HttpRequestMessage(HttpMethod.Post, $"/array/create?size={size}");
			createRequest.Headers.Add("Authorization", $"Bearer {token}");
			var createResp = await _client.SendAsync(createRequest);
			createResp.EnsureSuccessStatusCode();
			var getArrayRequest = new HttpRequestMessage(HttpMethod.Get, "/array");
			getArrayRequest.Headers.Add("Authorization", $"Bearer {token}");
			var getArrayResp = await _client.SendAsync(getArrayRequest);
			getArrayResp.EnsureSuccessStatusCode();
			var getArrayJson = await getArrayResp.Content.ReadAsStringAsync();
			using var getArrayDoc = JsonDocument.Parse(getArrayJson);
			var unsortedArray = getArrayDoc.RootElement.GetProperty("array")
									  .EnumerateArray()
									  .Select(e => e.GetInt32())
									  .ToArray();

			var expectedSorted = unsortedArray.OrderBy(x => x).ToArray();
			var sortRequest = new HttpRequestMessage(HttpMethod.Post, "/array/sort/shell");
			sortRequest.Headers.Add("Authorization", $"Bearer {token}");
			var sortResp = await _client.SendAsync(sortRequest);
			sortResp.EnsureSuccessStatusCode();
			var sortJson = await sortResp.Content.ReadAsStringAsync();
			using var sortDoc = JsonDocument.Parse(sortJson);
			Assert.Equal("Shell sort completed", sortDoc.RootElement.GetProperty("message").GetString());
			var sortedArray = sortDoc.RootElement.GetProperty("array")
								   .EnumerateArray()
								   .Select(e => e.GetInt32())
								   .ToArray();

			Assert.True(
				sortedArray.SequenceEqual(expectedSorted),
				$"Массив не отсортирован корректно: [{string.Join(",", sortedArray)}]. Ожидалось: [{string.Join(",", expectedSorted)}]"
			);
		}
		[Fact(DisplayName = "5) История запросов (GET /requests_history)")]
		public async Task RequestHistory_ShouldReturnListOfRequests()
		{
			var username = "historyuser";
			var password = "historypass";
			var registerResp = await _client.PostAsync($"/register?username={username}&password={password}", null);
			registerResp.EnsureSuccessStatusCode();
			var registerJson = await registerResp.Content.ReadAsStringAsync();
			var token = JsonDocument.Parse(registerJson).RootElement.GetProperty("token").GetString();
			int size = 5;
			var createRequest = new HttpRequestMessage(HttpMethod.Post, $"/array/create?size={size}");
			createRequest.Headers.Add("Authorization", $"Bearer {token}");
			var createResp = await _client.SendAsync(createRequest);
			createResp.EnsureSuccessStatusCode();
			var getArrayRequest = new HttpRequestMessage(HttpMethod.Get, "/array");
			getArrayRequest.Headers.Add("Authorization", $"Bearer {token}");
			var getArrayResp = await _client.SendAsync(getArrayRequest);
			getArrayResp.EnsureSuccessStatusCode();
			var getArrayJson = await getArrayResp.Content.ReadAsStringAsync();
			using var getArrayDoc = JsonDocument.Parse(getArrayJson);
			var unsortedArray = getArrayDoc.RootElement.GetProperty("array")
									  .EnumerateArray()
									  .Select(e => e.GetInt32())
									  .ToArray();

			var historyRequest = new HttpRequestMessage(HttpMethod.Get, "/requests_history");
			historyRequest.Headers.Add("Authorization", $"Bearer {token}");
			var historyResp = await _client.SendAsync(historyRequest);
			historyResp.EnsureSuccessStatusCode();

			var historyJson = await historyResp.Content.ReadAsStringAsync();
			var historyArray = JsonDocument.Parse(historyJson).RootElement.EnumerateArray().ToList();
			Assert.NotEmpty(historyArray); 
			var endpoints = historyArray.Select(h => h.GetProperty("endpoint").GetString()).ToList();
			Assert.Contains("GET /array", endpoints);
		}

		[Fact(DisplayName = "6) Смена пароля (PATCH /change_password) и повторная авторизация")]
		public async Task ChangePassword_ShouldInvalidateOldTokenAndAllowNewRequestsWithNewToken()
		{
			var username = "changepassuser";
			var password = "oldpass";
			var registerResp = await _client.PostAsync($"/register?username={username}&password={password}", null);
			registerResp.EnsureSuccessStatusCode();
			var registerJson = await registerResp.Content.ReadAsStringAsync();
			var tokenOld = JsonDocument.Parse(registerJson).RootElement.GetProperty("token").GetString();
			var newPassword = "newpass";
			var changePwdRequest = new HttpRequestMessage(HttpMethod.Patch, $"/change_password?newPassword={newPassword}");
			changePwdRequest.Headers.Add("Authorization", $"Bearer {tokenOld}");
			var changePwdResp = await _client.SendAsync(changePwdRequest);
			changePwdResp.EnsureSuccessStatusCode();
			var pwdJson = await changePwdResp.Content.ReadAsStringAsync();
			var newToken = JsonDocument.Parse(pwdJson).RootElement.GetProperty("newToken").GetString();
			Assert.NotEqual(tokenOld, newToken); 
			var badRequest = new HttpRequestMessage(HttpMethod.Get, "/array");
			badRequest.Headers.Add("Authorization", $"Bearer {tokenOld}");
			var badResp = await _client.SendAsync(badRequest);
			Assert.Equal(HttpStatusCode.Unauthorized, badResp.StatusCode);
			var goodRequest = new HttpRequestMessage(HttpMethod.Get, "/array");
			goodRequest.Headers.Add("Authorization", $"Bearer {newToken}");
			var goodResp = await _client.SendAsync(goodRequest);
			Assert.NotEqual(HttpStatusCode.Unauthorized, goodResp.StatusCode);
		}
	}
}
