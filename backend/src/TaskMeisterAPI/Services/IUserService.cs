using ErrorOr;
using TaskMeisterAPI.Models.Entities;
using TaskMeisterAPI.Models.Requests;
using TaskMeisterAPI.Models.Responses;

namespace TaskMeisterAPI.Services;

public interface IUserService
{
    Task<ErrorOr<AuthResponse>> SignupAsync(SignupRequest request);
    Task<ErrorOr<AuthResponse>> LoginAsync(LoginRequest request);
    Task<ErrorOr<Success>> LogoutAsync(User user);
}
