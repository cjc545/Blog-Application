using BlogApp.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BlogApp.Models;

namespace BlogApp.Controllers
{
    public class BlogPostController : Controller
    {
        private readonly BlogDbContext _context;

        //Constructor (dependancy injection)
        public BlogPostController(BlogDbContext context)
        {
            _context = context;
        }

        //Displays list of all blog posts
        //Redirects unauthenticated users
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

        //Returns create post view
        //Redirects unauthenticated users
        public IActionResult Create()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("UserEmail")))
            {
                TempData["BlogListError"] = "If you want to create a post, you have to login first!";
                return RedirectToAction("Login", "Account");
            }
            return View();
        }

        //Handles submitted Create post form
        //Validates content is not empty, at least min char count (10)
        //On success, saves post to db
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(BlogPost BlogDetails)
        {
            if(string.IsNullOrEmpty(HttpContext.Session.GetString("UserEmail")))
            {
                return RedirectToAction("Login", "Account");
            }
            //Custom func that returns character length of post
            var characterCheck = ContentCharacterCount(BlogDetails.Content);

            //If post is empty, show error message & dont proceed
            if (BlogDetails.Content == "<p><br></p>")
            {
                TempData["BlogPostError"] = "Your post needs content dude!!!";
                return View(BlogDetails);
            }
            //If post has less than 10 characters, show error message & dont proceed
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

            //We don't care about these from model, so lets remove
            ModelState.Remove("User");
            ModelState.Remove("Comments");

            //If model state is valid, save to db and redirect
            if (ModelState.IsValid)
            {
                BlogDetails.PublishedDate = DateTime.UtcNow;
                _context.Add(BlogDetails);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));

            }

            return View(BlogDetails);
        }

        //Returns Details view
        //Redirects unauthenticated users to Login.
        //Returns a 404 if no post is found for the given ID.
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

            //Store return URL in ViewBag, in case user wants to go back
            ViewBag.ReturnUrl = returnUrl ?? Url.Action("Index", "Home");

            return View(blogDetails);

        }

        //Returns Edit view
        //Verifies that the session username matches the post owner's name before allowing access.
        //Non-owners are redirected to Index with an error message.
        public async Task<IActionResult> Edit(int Id)
        {
            //Grab blog details based on Id param
            var blogDetails = await _context.BlogPosts.FindAsync(Id);
            var postOwner = await _context.Users.FirstOrDefaultAsync(u => u.ID == blogDetails.UserId);

            if (HttpContext.Session.GetString("UserName") != postOwner.Name)
            {
                TempData["BlogListError"] = "Can't Edit post that isn't yours! That would be rude!";
                return RedirectToAction(nameof(Index));
            }
            //If blog details hare null, return 404
            if (blogDetails == null)
            {
                return NotFound();
            }
            return View(blogDetails);
        }

        //Handles the submitted Edit form.
        //Confirms the route ID matches the posted model ID, then fetches the tracked entity from
        //the database and applies only the editable fields (Title, Content, PublishedDate) to it.
        //This avoids overwriting fields like UserId that should not be changed on edit.
        //On success, saves changes and redirects to Index.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int Id, BlogPost BlogDetails)
        {
            if (Id != BlogDetails.ID)
            {
                return NotFound();
            }
            //Grab blog details based on Id param
            var blogDetails = await _context.BlogPosts.FindAsync(Id);

            //If blog details are null, return 404
            if (blogDetails == null)
            {
                return NotFound();
            }

            blogDetails.Title = BlogDetails.Title;
            blogDetails.Content = BlogDetails.Content;
            blogDetails.PublishedDate = BlogDetails.PublishedDate;

            ModelState.Remove("User");
            ModelState.Remove("Comments");

            //If model state is valid, save to db and redirect
            if (ModelState.IsValid)
            {
                //_context.Update(BlogDetails);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }

            return View(BlogDetails);

        }

        //Returns Delete view
        //Checks that the logged-in user is the post owner before allowing access.
        public async Task<IActionResult> Delete(int Id)
        {
            var blogDetails = await _context.BlogPosts
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.ID == Id);

            var postOwner = await _context.Users.FirstOrDefaultAsync(u => u.ID == blogDetails.UserId);

            if (HttpContext.Session.GetString("UserName") != postOwner.Name)
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

        //Permanently deletes a blog post and any images it referenced.
        [HttpPost, ActionName("DeleteConfirmed")]
        public async Task<IActionResult> DeleteConfirmed(int Id)
        {
            //Get blog details, check Id matches incoming Id from func var
            var blogDetails = await _context.BlogPosts.FindAsync(Id);
            if (Id != blogDetails.ID)
            {
                return NotFound();
            }

            //Parse image URLs from Content
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

                        //If filePath exists, try delete and catch exception if error occurs
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

            //Delete blogpost and save
            _context.BlogPosts.Remove(blogDetails);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));

        }

        //Custom func, strips HTML tags and counts remaining chars, returns int value
        private int ContentCharacterCount(string content)
        {
            //Remove the html tags inserted by Quilljs and count actual chars
            content = content.Replace("<p>", "");
            content = content.Replace("</p>", "");
            content = content.Replace("<br>", "");

            return content.Length;
        }

    }
}
