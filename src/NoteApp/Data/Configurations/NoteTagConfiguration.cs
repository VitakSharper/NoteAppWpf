using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NoteApp.Data.Entities;

namespace NoteApp.Data.Configurations;

public sealed class NoteTagConfiguration : IEntityTypeConfiguration<NoteTagEntity>
{
    public void Configure(EntityTypeBuilder<NoteTagEntity> builder)
    {
        builder.ToTable("NoteTags");
        builder.HasKey(nt => new { nt.NoteId, nt.TagId });

        builder.HasOne(nt => nt.Note)
            .WithMany(n => n.NoteTags)
            .HasForeignKey(nt => nt.NoteId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(nt => nt.Tag)
            .WithMany(t => t.NoteTags)
            .HasForeignKey(nt => nt.TagId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
