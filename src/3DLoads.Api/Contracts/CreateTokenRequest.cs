namespace ThreeDLoads.Api.Contracts;

// Credentials used by the development JWT endpoint.
public sealed class CreateTokenRequest
{
    public required string Username { get; init; }
    public required string Password { get; init; }
}