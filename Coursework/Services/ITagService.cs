using System.Collections.Generic;
using System.Threading.Tasks;
using MauiApp1.Models;

namespace Coursework.Services
{
    public interface ITagService
    {
        Task InitializeAsync();
        Task<List<Tag>> GetAllTagsAsync();
        Task<Tag?> CreateTagAsync(string tagName);
        Task<bool> UpdateTagAsync(Tag tag);
        Task<bool> DeleteTagAsync(int tagId);
    }
}