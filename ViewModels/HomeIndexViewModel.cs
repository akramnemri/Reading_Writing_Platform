using Reading_Writing_Platform.Models;

namespace Reading_Writing_Platform.ViewModels
{
    public class HomeIndexViewModel
    {
        public IReadOnlyList<NovelListItemViewModel> PublishedNovels { get; set; } = [];
    }
}
