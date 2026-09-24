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
    public DbSet<NoteVersionEntity> NoteVersions => Set<NoteVersionEntity>();

    public NoteDbContext(DbContextOptions<NoteDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new NoteConfiguration());
        modelBuilder.ApplyConfiguration(new NoteBlockConfiguration());
        modelBuilder.ApplyConfiguration(new TagConfiguration());
        modelBuilder.ApplyConfiguration(new NoteTagConfiguration());
        modelBuilder.ApplyConfiguration(new NoteVersionConfiguration());

        // Trashed notes are invisible everywhere by default — list, open, update,
        // encrypted payload — so no query path has to remember the rule. Only the
        // trash operations in NoteRepository lift it with IgnoreQueryFilters(). Templates
        // are hidden the same way: they are not notes of the list, search or links.
        modelBuilder.Entity<NoteEntity>().HasQueryFilter(n => n.DeletedAt == null && !n.IsTemplate);
    }
}
