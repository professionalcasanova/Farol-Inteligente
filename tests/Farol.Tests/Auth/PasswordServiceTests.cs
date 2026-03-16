using Farol.Domain.Users;
using Farol.Infrastructure.Auth;

namespace Farol.Tests.Auth;

public sealed class PasswordServiceTests
{
    [Fact]
    public void HashPassword_ShouldGenerateHashDifferentFromPlainPassword()
    {
        var service = new PasswordService();
        var user = new User("Maria", "maria@email.com", "temporary-hash");

        var hash = service.HashPassword(user, "123456");

        Assert.NotEqual("123456", hash);
        Assert.False(string.IsNullOrWhiteSpace(hash));
    }

    [Fact]
    public void VerifyPassword_ShouldReturnTrueForCorrectPassword()
    {
        var service = new PasswordService();
        var user = new User("Maria", "maria@email.com", "temporary-hash");
        var hash = service.HashPassword(user, "123456");
        user.ChangePasswordHash(hash);

        var isValid = service.VerifyPassword(user, "123456");

        Assert.True(isValid);
    }

    [Fact]
    public void VerifyPassword_ShouldReturnFalseForIncorrectPassword()
    {
        var service = new PasswordService();
        var user = new User("Maria", "maria@email.com", "temporary-hash");
        var hash = service.HashPassword(user, "123456");
        user.ChangePasswordHash(hash);

        var isValid = service.VerifyPassword(user, "654321");

        Assert.False(isValid);
    }
}
