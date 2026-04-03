using Microsoft.EntityFrameworkCore;
using NoteApp.Data.Configurations;
using NoteApp.Data.Entities;

namespace NoteApp.Data;

public sealed class NoteDbContext : DbContext
{
    public DbSet<NoteEntity> Notes => Set<NoteEntity>();
    public DbSet<NoteBlockEntity> NoteBlocks => Set<NoteBlockEntity>();
    public DbSet<TagEntity> Tags => Set<TagEntity>();
    public DbSet<NoteTagEntity> NoteTags => Set<NoteTagEntity>();

    public NoteDbContext(DbContextOptions<NoteDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new NoteConfiguration());
        modelBuilder.ApplyConfiguration(new NoteBlockConfiguration());
        modelBuilder.ApplyConfiguration(new TagConfiguration());
        modelBuilder.ApplyConfiguration(new NoteTagConfiguration());
    }
}
