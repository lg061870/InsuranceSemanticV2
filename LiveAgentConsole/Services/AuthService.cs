using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using Blazored.LocalStorage;

namespace LiveAgentConsole.Services;

public class AuthService
{
    private readonly HttpClient _httpClient;
    private readonly ILocalStorageService _localStorage;
    private const string TokenKey = "authToken";

    public AuthService(HttpClient httpClient, ILocalStorageService localStorage)
    {
        _httpClient = httpClient;
        _localStorage = localStorage;
    }

    public async Task<LoginResult> LoginAsync(string username, string password)
    {
        try
        {
            var request = new { Username = username, Password = password };
            var response = await _httpClient.PostAsJsonAsync("api/auth/login", request);

            if (!response.IsSuccessStatusCode)
            {
                return new LoginResult
                {
                    Success = false,
                    ErrorMessage = "Invalid username or password"
                };
            }

            var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>();
            if (loginResponse == null || string.IsNullOrEmpty(loginResponse.Token))
            {
                return new LoginResult
                {
                    Success = false,
                    ErrorMessage = "Failed to receive authentication token"
                };
            }

            // Store token in localStorage
            await _localStorage.SetItemAsStringAsync(TokenKey, loginResponse.Token);

            return new LoginResult
            {
                Success = true,
                Agent = loginResponse.Agent
            };
        }
        catch (Exception ex)
        {
            return new LoginResult
            {
                Success = false,
                ErrorMessage = $"Login failed: {ex.Message}"
            };
        }
    }

    public async Task LogoutAsync()
    {
        try
        {
            // Get token before removing it
            var token = await GetTokenAsync();
            if (!string.IsNullOrEmpty(token))
            {
                // Call logout endpoint
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", token);
                await _httpClient.PostAsync("api/auth/logout", null);
            }
        }
        catch
        {
            // Ignore errors during logout API call
        }
        finally
        {
            // Always remove token from localStorage
            await _localStorage.RemoveItemAsync(TokenKey);
        }
    }

    public async Task<string?> GetTokenAsync()
    {
        return await _localStorage.GetItemAsStringAsync(TokenKey);
    }

    public async Task<AgentResponse?> GetCurrentAgentAsync()
    {
        try
        {
            var token = await GetTokenAsync();
            if (string.IsNullOrEmpty(token))
            {
                return null;
            }

            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.PostAsync("api/auth/me", null);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadFromJsonAsync<AgentResponse>();
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> IsAuthenticatedAsync()
    {
        var token = await GetTokenAsync();
        if (string.IsNullOrEmpty(token))
        {
            return false;
        }

        // Parse JWT to check expiration
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);

            // Check if token is expired
            if (jwtToken.ValidTo < DateTime.UtcNow)
            {
                // Token expired, remove it
                await _localStorage.RemoveItemAsync(TokenKey);
                return false;
            }

            return true;
        }
        catch
        {
            // Invalid token format
            await _localStorage.RemoveItemAsync(TokenKey);
            return false;
        }
    }

    public async Task<ClaimsPrincipal?> GetClaimsPrincipalAsync()
    {
        var token = await GetTokenAsync();
        if (string.IsNullOrEmpty(token))
        {
            return null;
        }

        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);

            var identity = new ClaimsIdentity(jwtToken.Claims, "jwt");
            return new ClaimsPrincipal(identity);
        }
        catch
        {
            return null;
        }
    }
}

// DTOs
public class LoginResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public AgentResponse? Agent { get; set; }
}

public class LoginResponse
{
    public string Token { get; set; } = string.Empty;
    public AgentResponse Agent { get; set; } = new();
}

public class AgentResponse
{
    public int AgentId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string AvatarUrl { get; set; } = string.Empty;
}
