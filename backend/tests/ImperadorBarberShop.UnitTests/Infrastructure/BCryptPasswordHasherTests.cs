using FluentAssertions;
using ImperadorBarberShop.Infrastructure.Services;

namespace ImperadorBarberShop.UnitTests.Infrastructure;

public class BCryptPasswordHasherTests
{
    private readonly BCryptPasswordHasher _hasher = new();

    [Fact]
    public void Hash_ReturnsNonPlaintextString()
    {
        const string password = "my_secure_password";

        var hash = _hasher.Hash(password);

        hash.Should().NotBe(password);
        hash.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Hash_TwiceSamePassword_ProducesDifferentHashes()
    {
        const string password = "my_secure_password";

        var hash1 = _hasher.Hash(password);
        var hash2 = _hasher.Hash(password);

        // BCrypt uses a random salt — two hashes of the same password must differ
        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void Verify_CorrectPassword_ReturnsTrue()
    {
        const string password = "my_secure_password";
        var hash = _hasher.Hash(password);

        var result = _hasher.Verify(password, hash);

        result.Should().BeTrue();
    }

    [Fact]
    public void Verify_WrongPassword_ReturnsFalse()
    {
        var hash = _hasher.Hash("correct_password");

        var result = _hasher.Verify("wrong_password", hash);

        result.Should().BeFalse();
    }
}
