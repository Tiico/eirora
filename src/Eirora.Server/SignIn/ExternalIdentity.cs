namespace Eirora.Server.SignIn;

/// <summary>A verified sign-in: the provider, its identifier for the player and the client id the token was issued to.</summary>
public sealed record ExternalIdentity(string Provider, string Subject, string Audience);
