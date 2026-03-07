using Microsoft.AspNetCore.Mvc;
using BlogApp.Data;
using BlogApp.Models;

namespace BlogApp.Controllers
{
    public class CommentsController : Controller
    {
        private readonly BlogDbContext _context;

        public CommentsController(BlogDbContext context)
        {
            _context = context;
        }
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int PostId, string User, string UserComments)
        {
            if (string.IsNullOrWhiteSpace(User) || string.IsNullOrWhiteSpace(UserComments))
            {
                TempData["Error"] = "Name and Comments cannot be Empty";
                return RedirectToAction("Details", "Post", new { Id = PostId });
            }

            var comment = new Comments
            {
                PostId = PostId,
                Author = User,
                Content = UserComments,
                CreateAt = DateTime.UtcNow

            };

            _context.Comments.Add(comment);

            await _context.SaveChangesAsync();

            TempData["Success"] = "Comments added Successfully";

            return RedirectToAction("Details", "BlogPost", new { ID = PostId });
        }
    }
}
