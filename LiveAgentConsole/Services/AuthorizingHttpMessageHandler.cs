using System.Net.Http.Headers;

namespace LiveAgentConsole.Services;

public class AuthorizingHttpMessageHandler : DelegatingHandler
{
    private readonly AuthService _authService;

    public AuthorizingHttpMessageHandler(AuthService authService)
    {
        _authService = authService;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Get token from AuthService
        var token = await _authService.GetTokenAsync();

        // If token exists, add it to the Authorization header
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
