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

        // Trashed notes are invisible everywhere by default — list, open, update,
        // encrypted payload — so no query path has to remember the rule. Only the
        // trash operations in NoteRepository lift it with IgnoreQueryFilters().
        modelBuilder.Entity<NoteEntity>().HasQueryFilter(n => n.DeletedAt == null);
    }
}
