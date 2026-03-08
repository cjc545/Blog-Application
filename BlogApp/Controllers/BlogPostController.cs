using BlogApp.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BlogApp.Models;

namespace BlogApp.Controllers
{
    public class BlogPostController : Controller
    {
        private readonly BlogDbContext _context;
        public BlogPostController(BlogDbContext context)
        {
            _context = context;
        }
        public async Task<IActionResult> Index()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("UserEmail")))
            {
                TempData["BlogListError"] = "If you want to view our posts, you have to login first!";
                return RedirectToAction("Login", "Account");
            }

            var blogPost = await _context.BlogPosts
                .Include(p => p.User)
                .OrderByDescending(p => p.ID)
                .ToListAsync();

            return View(blogPost);
        }

        public IActionResult Create()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("UserEmail")))
            {
                TempData["BlogListError"] = "If you want to create a post, you have to login first!";
                return RedirectToAction("Login", "Account");
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(BlogPost BlogDetails)
        {
            if(string.IsNullOrEmpty(HttpContext.Session.GetString("UserEmail")))
            {
                return RedirectToAction("Login", "Account");
            }
            var characterCheck = ContentCharacterCount(BlogDetails.Content);

            if (BlogDetails.Content == "<p><br></p>")
            {
                TempData["BlogPostError"] = "Your post needs content dude!!!";
                return View(BlogDetails);
            }

            if (characterCheck < 10)
            {
                TempData["BlogPostError"] = "Your post needs at least 10 characters";
                return View(BlogDetails);
            }

            //get user login details
            string userEmail = HttpContext.Session.GetString("UserEmail");
            var userDetails = await _context.Users.FirstOrDefaultAsync(u => u.Email == userEmail);
            var content = HttpContext.Session.GetString("#contentInput");

            BlogDetails.UserId = userDetails.ID;

            ModelState.Remove("User");
            ModelState.Remove("Comments");

            if (ModelState.IsValid)
            {
                BlogDetails.PublishedDate = DateTime.UtcNow;
                _context.Add(BlogDetails);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));

            }

            return View(BlogDetails);
        }

        public async Task<IActionResult> Details(int Id, string returnUrl = null)
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("UserEmail")))
            {
                TempData["BlogListError"] = "If you want to view our posts, you have to login first!";
                return RedirectToAction("Login", "Account");
            }

            var blogDetails = await _context.BlogPosts
                .Include(p => p.User)
                .Include(p => p.Comments)
                .FirstOrDefaultAsync(p => p.ID == Id);

            if (blogDetails == null)
            {
                return NotFound();
            }

            ViewBag.ReturnUrl = returnUrl ?? Url.Action("Index", "Home");

            return View(blogDetails);

        }

        public async Task<IActionResult> Edit(int Id)
        {
            var blogDetails = await _context.BlogPosts.FindAsync(Id);
            var postOwner = await _context.Users.FirstOrDefaultAsync(u => u.ID == blogDetails.UserId);

            if (HttpContext.Session.GetString("UserName") != postOwner.Name)
            {
                TempData["BlogListError"] = "Can't Edit post that isn't yours! That would be rude!";
                return RedirectToAction(nameof(Index));
            }

            if (blogDetails == null)
            {
                return NotFound();
            }
            return View(blogDetails);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int Id, BlogPost BlogDetails)
        {
            if (Id != BlogDetails.ID)
            {
                return NotFound();
            }
            BlogDetails.UserId = 1;
            ModelState.Remove("User");
            ModelState.Remove("Comments");

            if(ModelState.IsValid)
            {
                _context.Update(BlogDetails);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }

            return View(BlogDetails);

        }

        public async Task<IActionResult> Delete(int Id)
        {
            var blogDetails = await _context.BlogPosts
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.ID == Id);

            var postOwner = await _context.Users.FirstOrDefaultAsync(u => u.ID == blogDetails.UserId);

            if (HttpContext.Session.GetString("UserName") != postOwner.Name )
            {
                TempData["BlogListError"] = "Can't Delete a post that isn't yours! That would be really rude!";
                return RedirectToAction(nameof(Index));
            }

            if (blogDetails == null)
            {
                return NotFound();
            }
            return View(blogDetails);
        }

        [HttpPost, ActionName("DeleteConfirmed")]
        public async Task<IActionResult> DeleteConfirmed(int Id)
        {
            var blogDetails = await _context.BlogPosts.FindAsync(Id);
            if (Id != blogDetails.ID)
            {
                return NotFound();
            }

            // 1. Parse image URLs from Content
            var htmlContent = blogDetails.Content ?? "";
            var doc = new HtmlAgilityPack.HtmlDocument();
            doc.LoadHtml(htmlContent);

            var imageNodes = doc.DocumentNode.SelectNodes("//img[@src]");
            if (imageNodes != null)
            {
                foreach (var img in imageNodes)
                {
                    var src = img.GetAttributeValue("src", null);
                    if (!string.IsNullOrEmpty(src) && src.StartsWith("/uploads/"))
                    {
                        // Convert URL to physical path
                        var fileName = src.Substring("/uploads/".Length);
                        var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads", fileName);

                        if (System.IO.File.Exists(filePath))
                        {
                            try
                            {
                                System.IO.File.Delete(filePath);
                            }
                            catch(Exception ex)
                            {
                                throw new ApplicationException(string.Format("Error occured deleteing image {0} from /uploads", fileName), ex);
                            }
                        }
                    }
                }
            }


            _context.BlogPosts.Remove(blogDetails);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));

        }

        private int ContentCharacterCount(string content)
        {
            content = content.Replace("<p>", "");
            content = content.Replace("</p>", "");
            content = content.Replace("<br>", "");

            return content.Length;
        }

    }
}
