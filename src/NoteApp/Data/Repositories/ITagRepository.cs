using NoteApp.Domain.Functional;
using NoteApp.Domain.Models;

namespace NoteApp.Data.Repositories;

public interface ITagRepository
{
    Task<Result<IReadOnlyList<Tag>, AppError>> GetAllAsync();
    Task<Result<Tag, AppError>> CreateAsync(Tag tag);
    Task<Result<Tag, AppError>> UpdateAsync(Tag tag);
    Task<Result<Unit, AppError>> DeleteAsync(Guid id);
}
