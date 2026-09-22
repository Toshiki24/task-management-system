using TaskManagementSystem.Api.Dtos.Common;

namespace TaskManagementSystem.Api.Dtos.Auth;

public record LoginResponse(string AccessToken, UserDto User);
