using System.Security.Cryptography;
using NoteApp.Domain.Models;
using NoteApp.Services;

namespace NoteApp.Tests.Services;

public class EncryptionServiceTests
{
    [Fact]
    public void RoundTrip_PreservesEveryBlockKind_PlainTextIncluded()
    {
        var blocks = SampleBlocks();

        var encrypted = EncryptionService.EncryptBlocks(blocks, "s3cret");
        var decrypted = EncryptionService.DecryptBlocks(encrypted, "s3cret");

        Assert.Equal(3, decrypted.Count);
        Assert.Equal(blocks.Select(b => b.Id), decrypted.Select(b => b.Id));

        var text = Assert.IsType<NoteBlock.Text>(decrypted[0]);
        Assert.Equal("<rich/>", text.RichText);
        Assert.Equal("plain text", text.PlainText);

        var file = Assert.IsType<NoteBlock.File>(decrypted[1]);
        Assert.Equal([1, 2, 3], file.Data);
        Assert.Equal(3, file.SizeBytes);

        var link = Assert.IsType<NoteBlock.Link>(decrypted[2]);
        Assert.Equal("https://example.com/page", link.Url.ToString());
    }

    [Fact]
    public void Decrypt_WithTheWrongPassword_Throws()
    {
        var encrypted = EncryptionService.EncryptBlocks(SampleBlocks(), "right");

        // AesGcm raises AuthenticationTagMismatchException, a CryptographicException subtype —
        // which is exactly what NoteService.UnlockNoteAsync catches to report "Wrong password".
        Assert.ThrowsAny<CryptographicException>(() => EncryptionService.DecryptBlocks(encrypted, "wrong"));
    }

    [Fact]
    public void Decrypt_TamperedCiphertext_Throws()
    {
        var encrypted = EncryptionService.EncryptBlocks(SampleBlocks(), "pw");
        encrypted[^1] ^= 0xFF;

        Assert.ThrowsAny<CryptographicException>(() => EncryptionService.DecryptBlocks(encrypted, "pw"));
    }

    [Fact]
    public void Decrypt_PayloadShorterThanTheHeader_Throws() =>
        Assert.Throws<CryptographicException>(() => EncryptionService.DecryptBlocks(new byte[10], "pw"));

    [Fact]
    public void Encrypt_UsesFreshSaltAndNonce_SoIdenticalInputDiffers()
    {
        var blocks = SampleBlocks();

        var first = EncryptionService.EncryptBlocks(blocks, "pw");
        var second = EncryptionService.EncryptBlocks(blocks, "pw");

        Assert.NotEqual(first, second);
    }
}
