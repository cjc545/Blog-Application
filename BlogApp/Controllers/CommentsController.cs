using Microsoft.AspNetCore.Mvc;
using BlogApp.Data;
using BlogApp.Models;

namespace BlogApp.Controllers
{
    public class CommentsController : Controller
    {
        private readonly BlogDbContext _context;

        //Constructor (dependancy injection)
        public CommentsController(BlogDbContext context)
        {
            _context = context;
        }
        //Return Comments index view
        public IActionResult Index()
        {
            return View();
        }

        //Handles comment submissions.
        //Validates comment content is not empty, if so it redirects with error message.
        //On valid input, builds a new Comment using the PostId, the session username as
        //the author, the submitted content, and a UTC timestamp, then saves it to the database.
        //On success, redirects back to the blog post's Details page with a success message.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int PostId, string User, string UserComments)
        {
            if (string.IsNullOrWhiteSpace(UserComments))
            {
                TempData["Error"] = "Name and Comments cannot be Empty";
                return RedirectToAction("Details", "Post", new { Id = PostId });
            }

            var comment = new Comments
            {
                PostId = PostId,
                Author = HttpContext.Session.GetString("UserName"),
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
