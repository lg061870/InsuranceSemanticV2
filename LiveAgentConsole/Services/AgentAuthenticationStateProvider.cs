using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace LiveAgentConsole.Services;

public class AgentAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly AuthService _authService;
    private ClaimsPrincipal _currentUser = new(new ClaimsIdentity());

    public AgentAuthenticationStateProvider(AuthService authService)
    {
        _authService = authService;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var isAuthenticated = await _authService.IsAuthenticatedAsync();

        if (!isAuthenticated)
        {
            _currentUser = new ClaimsPrincipal(new ClaimsIdentity());
            return new AuthenticationState(_currentUser);
        }

        var claimsPrincipal = await _authService.GetClaimsPrincipalAsync();
        if (claimsPrincipal != null)
        {
            _currentUser = claimsPrincipal;
        }
        else
        {
            _currentUser = new ClaimsPrincipal(new ClaimsIdentity());
        }

        return new AuthenticationState(_currentUser);
    }

    public async Task MarkUserAsAuthenticatedAsync(string token)
    {
        var claimsPrincipal = await _authService.GetClaimsPrincipalAsync();
        if (claimsPrincipal != null)
        {
            _currentUser = claimsPrincipal;
        }
        else
        {
            _currentUser = new ClaimsPrincipal(new ClaimsIdentity());
        }

        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_currentUser)));
    }

    public void MarkUserAsLoggedOut()
    {
        _currentUser = new ClaimsPrincipal(new ClaimsIdentity());
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_currentUser)));
    }
}
