using FluentAssertions;
using GvResearch.Sip.Registrar;

namespace GvResearch.Sip.Tests.Registrar;

public sealed class RegistrationStoreTests
{
    private readonly RegistrationStore _store = new();

    private static Uri ContactUri(string uri) => new(uri, UriKind.RelativeOrAbsolute);

    [Fact]
    public void AddOrUpdate_StoresRegistration()
    {
        // Arrange
        var expires = DateTimeOffset.UtcNow.AddHours(1);
        var contactUri = ContactUri("sip:alice@192.168.1.1:5060");

        // Act
        _store.AddOrUpdate("alice", contactUri, expires);

        // Assert
        var all = _store.GetAll();
        all.Should().ContainSingle();
        all[0].Username.Should().Be("alice");
        all[0].ContactUri.Should().Be(contactUri);
        all[0].ExpiresAt.Should().Be(expires);
    }

    [Fact]
    public void AddOrUpdate_ReplacesExistingRegistration()
    {
        // Arrange
        var firstExpires = DateTimeOffset.UtcNow.AddHours(1);
        var secondExpires = DateTimeOffset.UtcNow.AddHours(2);
        var secondUri = ContactUri("sip:alice@192.168.1.2:5060");

        _store.AddOrUpdate("alice", ContactUri("sip:alice@192.168.1.1:5060"), firstExpires);

        // Act
        _store.AddOrUpdate("alice", secondUri, secondExpires);

        // Assert
        var all = _store.GetAll();
        all.Should().ContainSingle();
        all[0].ContactUri.Should().Be(secondUri);
        all[0].ExpiresAt.Should().Be(secondExpires);
    }

    [Fact]
    public void GetAll_ExcludesExpiredRegistrations()
    {
        // Arrange
        var expired = DateTimeOffset.UtcNow.AddSeconds(-1);
        var valid = DateTimeOffset.UtcNow.AddHours(1);

        _store.AddOrUpdate("expired", ContactUri("sip:expired@host"), expired);
        _store.AddOrUpdate("valid", ContactUri("sip:valid@host"), valid);

        // Act
        var result = _store.GetAll();

        // Assert
        result.Should().ContainSingle(r => r.Username == "valid");
        result.Should().NotContain(r => r.Username == "expired");
    }

    [Fact]
    public void GetAll_ReturnsEmptyWhenNoRegistrations()
    {
        _store.GetAll().Should().BeEmpty();
    }

    [Fact]
    public void Remove_DeletesRegistration()
    {
        // Arrange
        _store.AddOrUpdate("bob", ContactUri("sip:bob@host"), DateTimeOffset.UtcNow.AddHours(1));

        // Act
        var removed = _store.Remove("bob");

        // Assert
        removed.Should().BeTrue();
        _store.GetAll().Should().BeEmpty();
    }

    [Fact]
    public void Remove_ReturnsFalseForUnknownUser()
    {
        // Act
        var removed = _store.Remove("nonexistent");

        // Assert
        removed.Should().BeFalse();
    }

    [Fact]
    public void TryGet_ReturnsNullForExpiredRegistration()
    {
        // Arrange
        _store.AddOrUpdate("charlie", ContactUri("sip:charlie@host"), DateTimeOffset.UtcNow.AddSeconds(-1));

        // Act
        var result = _store.TryGet("charlie");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void TryGet_ReturnsRegistrationForValidUser()
    {
        // Arrange
        var expires = DateTimeOffset.UtcNow.AddHours(1);
        _store.AddOrUpdate("dan", ContactUri("sip:dan@host"), expires);

        // Act
        var result = _store.TryGet("dan");

        // Assert
        result.Should().NotBeNull();
        result!.Username.Should().Be("dan");
    }
}
