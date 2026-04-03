using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NoteApp.Domain.Models;
using NoteApp.Domain.ValueObjects;

namespace NoteApp.Services;

public static class EncryptionService
{
    private const int SaltSize = 16;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 100_000;
    private const int HeaderSize = SaltSize + NonceSize + TagSize; // 44 bytes

    public static byte[] EncryptBlocks(IReadOnlyList<NoteBlock> blocks, string password)
    {
        var json = SerializeBlocks(blocks);
        var plainBytes = Encoding.UTF8.GetBytes(json);
        return Encrypt(plainBytes, password);
    }

    public static IReadOnlyList<NoteBlock> DecryptBlocks(byte[] encryptedData, string password)
    {
        var plainBytes = Decrypt(encryptedData, password);
        var json = Encoding.UTF8.GetString(plainBytes);
        return DeserializeBlocks(json);
    }

    private static byte[] Encrypt(byte[] plainData, string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = DeriveKey(password, salt);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var cipherText = new byte[plainData.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(nonce, plainData, cipherText, tag);

        // Pack: [salt][nonce][tag][ciphertext]
        var result = new byte[HeaderSize + cipherText.Length];
        Buffer.BlockCopy(salt, 0, result, 0, SaltSize);
        Buffer.BlockCopy(nonce, 0, result, SaltSize, NonceSize);
        Buffer.BlockCopy(tag, 0, result, SaltSize + NonceSize, TagSize);
        Buffer.BlockCopy(cipherText, 0, result, HeaderSize, cipherText.Length);

        return result;
    }

    private static byte[] Decrypt(byte[] packed, string password)
    {
        if (packed.Length < HeaderSize)
            throw new CryptographicException("Invalid encrypted data.");

        var salt = new byte[SaltSize];
        var nonce = new byte[NonceSize];
        var tag = new byte[TagSize];
        Buffer.BlockCopy(packed, 0, salt, 0, SaltSize);
        Buffer.BlockCopy(packed, SaltSize, nonce, 0, NonceSize);
        Buffer.BlockCopy(packed, SaltSize + NonceSize, tag, 0, TagSize);

        var cipherText = new byte[packed.Length - HeaderSize];
        Buffer.BlockCopy(packed, HeaderSize, cipherText, 0, cipherText.Length);

        var key = DeriveKey(password, salt);
        var plainData = new byte[cipherText.Length];

        using var aes = new AesGcm(key, TagSize);
        aes.Decrypt(nonce, cipherText, tag, plainData);

        return plainData;
    }

    private static byte[] DeriveKey(string password, byte[] salt)
    {
        return Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeySize);
    }

    private static string SerializeBlocks(IReadOnlyList<NoteBlock> blocks)
    {
        var dtos = blocks.Select(b => b.Match(
            text: t => new BlockDto
            {
                Type = "text", Id = b.Id, SortOrder = b.SortOrder,
                RichText = t.RichText
            },
            file: f => new BlockDto
            {
                Type = "file", Id = b.Id, SortOrder = b.SortOrder,
                FileData = Convert.ToBase64String(f.Data),
                FileName = f.FileName, FileExtension = f.Extension, FileSizeBytes = f.SizeBytes
            },
            link: l => new BlockDto
            {
                Type = "link", Id = b.Id, SortOrder = b.SortOrder,
                LinkUrl = l.Url.Value.ToString(), LinkDescription = l.Description
            }
        )).ToList();

        return JsonSerializer.Serialize(dtos);
    }

    private static IReadOnlyList<NoteBlock> DeserializeBlocks(string json)
    {
        var dtos = JsonSerializer.Deserialize<List<BlockDto>>(json) ?? [];
        var blocks = new List<NoteBlock>();

        foreach (var dto in dtos.OrderBy(d => d.SortOrder))
        {
            NoteBlock block = dto.Type switch
            {
                "text" => new NoteBlock.Text(dto.RichText ?? string.Empty)
                    { Id = dto.Id, SortOrder = dto.SortOrder },
                "file" => new NoteBlock.File(
                    Convert.FromBase64String(dto.FileData ?? string.Empty),
                    dto.FileName ?? "unknown",
                    dto.FileExtension ?? "",
                    dto.FileSizeBytes ?? 0)
                    { Id = dto.Id, SortOrder = dto.SortOrder },
                "link" => CreateLinkBlock(dto),
                _ => throw new InvalidOperationException($"Unknown block type: {dto.Type}")
            };
            blocks.Add(block);
        }

        return blocks;
    }

    private static NoteBlock CreateLinkBlock(BlockDto dto)
    {
        var urlResult = LinkUrl.From(dto.LinkUrl);
        var url = urlResult.Match(
            success: u => u,
            failure: _ => LinkUrl.From("https://invalid").Match(u => u,
                _ => throw new InvalidOperationException()));
        return new NoteBlock.Link(url, dto.LinkDescription ?? string.Empty)
            { Id = dto.Id, SortOrder = dto.SortOrder };
    }

    private sealed class BlockDto
    {
        public string Type { get; init; } = string.Empty;
        public Guid Id { get; init; }
        public int SortOrder { get; init; }
        public string? RichText { get; init; }
        public string? FileData { get; init; }
        public string? FileName { get; init; }
        public string? FileExtension { get; init; }
        public long? FileSizeBytes { get; init; }
        public string? LinkUrl { get; init; }
        public string? LinkDescription { get; init; }
    }
}
