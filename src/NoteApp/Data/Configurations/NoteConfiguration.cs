using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NoteApp.Data.Entities;

namespace NoteApp.Data.Configurations;

public sealed class NoteConfiguration : IEntityTypeConfiguration<NoteEntity>
{
    public void Configure(EntityTypeBuilder<NoteEntity> builder)
    {
        builder.ToTable("Notes");
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).ValueGeneratedNever();
        builder.Property(n => n.Title).HasMaxLength(500).IsRequired();
        builder.Property(n => n.IsEncrypted).HasDefaultValue(false);
        builder.Property(n => n.EncryptedContent).HasColumnType("varbinary(max)");
        builder.Property(n => n.CreatedAt).IsRequired();
        builder.Property(n => n.UpdatedAt).IsRequired();

        builder.HasMany(n => n.Blocks)
            .WithOne(b => b.Note)
            .HasForeignKey(b => b.NoteId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class NoteBlockConfiguration : IEntityTypeConfiguration<NoteBlockEntity>
{
    public void Configure(EntityTypeBuilder<NoteBlockEntity> builder)
    {
        builder.ToTable("NoteBlocks");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).ValueGeneratedNever();
        builder.Property(b => b.BlockType).IsRequired();
        builder.Property(b => b.SortOrder).IsRequired();
        builder.Property(b => b.TextContent).HasColumnType("nvarchar(max)");
        builder.Property(b => b.PlainText).HasColumnType("nvarchar(max)");
        builder.Property(b => b.FileData).HasColumnType("varbinary(max)");
        builder.Property(b => b.FileName).HasMaxLength(500);
        builder.Property(b => b.FileExtension).HasMaxLength(50);
        builder.Property(b => b.LinkUrl).HasMaxLength(2000);
        builder.Property(b => b.LinkDescription).HasMaxLength(1000);
        builder.Property(b => b.ChecklistJson).HasColumnType("nvarchar(max)");
    }
}
