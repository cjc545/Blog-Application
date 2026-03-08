using BlogApp.Data;
using Microsoft.AspNetCore.Mvc;

namespace BlogApp.Controllers
{
    [Route("Image")]
    public class ImageController : Controller
    {
        private readonly BlogDbContext _context;
        public ImageController(BlogDbContext context)
        {
            _context = context;
        }

        [HttpPost("Upload")]
        public async Task<IActionResult> Upload(IFormFile image)
        {
            if (image == null || image.Length == 0)
                return BadRequest();

            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads");

            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            var fileName = Guid.NewGuid() + Path.GetExtension(image.FileName);
            var filePath = Path.Combine(uploadsFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await image.CopyToAsync(stream);
            }

            return Content("/uploads/" + fileName);
        }
    }
}
