using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace DndCompanion.Services;

public class AuthStateService
{
    private const string PlayerIdKey = "player_id";
    private const string NicknameKey = "player_nickname";
    private const string RoleKey = "player_role";

    private readonly IJSRuntime _js;
    private readonly NavigationManager _navigation;

    public AuthStateService(IJSRuntime js, NavigationManager navigation)
    {
        _js = js;
        _navigation = navigation;
    }

    public async Task<bool> IsLoggedInAsync()
    {
        var id = await GetPlayerIdAsync();
        return !string.IsNullOrEmpty(id);
    }

    public ValueTask<string?> GetPlayerIdAsync()
        => _js.InvokeAsync<string?>("localStorage.getItem", PlayerIdKey);

    public ValueTask<string?> GetNicknameAsync()
        => _js.InvokeAsync<string?>("localStorage.getItem", NicknameKey);

    public ValueTask<string?> GetRoleAsync()
        => _js.InvokeAsync<string?>("localStorage.getItem", RoleKey);

    public async Task SetSessionAsync(string playerId, string nickname, string role)
    {
        await _js.InvokeVoidAsync("localStorage.setItem", PlayerIdKey, playerId);
        await _js.InvokeVoidAsync("localStorage.setItem", NicknameKey, nickname);
        await _js.InvokeVoidAsync("localStorage.setItem", RoleKey, role);
    }

    public async Task LogoutAsync()
    {
        await _js.InvokeVoidAsync("localStorage.removeItem", PlayerIdKey);
        await _js.InvokeVoidAsync("localStorage.removeItem", NicknameKey);
        await _js.InvokeVoidAsync("localStorage.removeItem", RoleKey);
        _navigation.NavigateTo("/login");
    }
}
