using NoteApp.Domain.Models;
using NoteApp.Services;

namespace NoteApp.Tests.Services;

public class SecretJsonTests
{
    [Fact]
    public void Round_trips_every_field_exactly_spaces_included()
    {
        var secret = new NoteBlock.Secret("Support site", "vbanard", "  p@ss word ", "https://support.example.com/");

        var back = SecretJson.Deserialize(SecretJson.Serialize(secret));

        Assert.Equal(("Support site", "vbanard", "  p@ss word ", "https://support.example.com/"),
            (back.Label, back.UserName, back.Password, back.Url));
    }

    [Fact]
    public void Empty_fields_are_left_out_of_the_json()
    {
        var json = SecretJson.Serialize(new NoteBlock.Secret("Wifi", "", "hunter2"));

        Assert.Equal("""{"l":"Wifi","p":"hunter2"}""", json);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    public void Unreadable_content_yields_an_empty_secret(string? json)
    {
        var secret = SecretJson.Deserialize(json);

        Assert.Equal(("", "", "", ""), (secret.Label, secret.UserName, secret.Password, secret.Url));
    }

    [Fact]
    public void The_searchable_text_never_holds_the_password()
    {
        var secret = new NoteBlock.Secret("Wifi", "", "hunter2", "https://router.local.example/");

        Assert.Equal("Wifi\nhttps://router.local.example/", secret.PlainText);
    }
}
