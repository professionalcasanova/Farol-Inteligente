using Farol.Domain.Users;
using Farol.Infrastructure.Auth;

namespace Farol.Tests.Auth;

public sealed class PasswordServiceTests
{
    [Fact]
    public void HashPassword_ValidPassword_ShouldGenerateHashDifferentFromPlainPassword()
    {
        var service = new PasswordService();
        var user = new User("Maria", "maria@email.com", "temporary-hash");

        var hash = service.HashPassword(user, "Password123");

        Assert.NotEqual("Password123", hash);
        Assert.False(string.IsNullOrWhiteSpace(hash));
    }

    [Fact]
    public void VerifyPassword_CorrectPassword_ShouldReturnTrue()
    {
        var service = new PasswordService();
        var user = new User("Maria", "maria@email.com", "temporary-hash");
        var hash = service.HashPassword(user, "Password123");
        user.ChangePasswordHash(hash);

        var isValid = service.VerifyPassword(user, "Password123");

        Assert.True(isValid);
    }

    [Fact]
    public void VerifyPassword_IncorrectPassword_ShouldReturnFalse()
    {
        var service = new PasswordService();
        var user = new User("Maria", "maria@email.com", "temporary-hash");
        var hash = service.HashPassword(user, "Password123");
        user.ChangePasswordHash(hash);

        var isValid = service.VerifyPassword(user, "Password456");

        Assert.False(isValid);
    }

    [Fact]
    public void ValidatePassword_ValidPassword_ShouldReturnValid()
    {
        var service = new PasswordService();

        var result = service.ValidatePassword("Password123");

        Assert.True(result.IsValid);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void ValidatePassword_WithoutNumber_ShouldReturnInvalid()
    {
        var service = new PasswordService();

        var result = service.ValidatePassword("Password");

        Assert.False(result.IsValid);
        Assert.Equal(PasswordService.PasswordPolicyErrorMessage, result.ErrorMessage);
    }

    [Fact]
    public void ValidatePassword_OnlySpaces_ShouldReturnInvalid()
    {
        var service = new PasswordService();

        var result = service.ValidatePassword("        ");

        Assert.False(result.IsValid);
        Assert.Equal(PasswordService.PasswordPolicyErrorMessage, result.ErrorMessage);
    }
}
