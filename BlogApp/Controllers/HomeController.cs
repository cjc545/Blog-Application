using System.Diagnostics;
using System.Threading.Tasks;
using BlogApp.Data;
using BlogApp.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Controllers
{
    public class HomeController : Controller
    {
        private readonly BlogDbContext _context;

        //Constructor (dependancy injection)
        public HomeController(BlogDbContext context)
        {
            _context = context;
        }
        //Fetch all blog posts from db, return view
        public async Task<IActionResult> Index()
        {
            List<BlogPost> posts = await _context.BlogPosts
                .Include(p => p.User)
                        .ToListAsync();

            return View(posts);
        }
        //Returns Privacy view.
        public IActionResult Privacy()
        {
            return View();
        }
        //Returns the Error view populated with the current request's trace ID.
        //The trace ID is taken from the active diagnostic activity if available,
        //falling back to ASP.NET Core's own HttpContext identifier.
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
