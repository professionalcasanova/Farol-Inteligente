using Farol.Domain.Users;

namespace Farol.Tests.Domain;

public sealed class UserTests
{
    [Fact]
    public void Constructor_ShouldNormalizeEmail()
    {
        var user = new User("Maria", "  Maria@Email.com  ", "hash");

        Assert.Equal("maria@email.com", user.Email);
    }

    [Fact]
    public void UpdateProfile_ShouldNormalizeEmail()
    {
        var user = new User("Maria", "maria@email.com", "hash");

        user.UpdateProfile("Maria Silva", "  NOVO@EMAIL.com ");

        Assert.Equal("novo@email.com", user.Email);
    }
}
