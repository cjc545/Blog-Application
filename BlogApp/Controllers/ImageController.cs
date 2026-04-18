using BlogApp.Data;
using Microsoft.AspNetCore.Mvc;

namespace BlogApp.Controllers
{
    [Route("Image")]
    public class ImageController : Controller
    {
        //All actions in this controller are rooted under the /Image path.
        private readonly BlogDbContext _context;

        //Constructor (dependancy injection)
        public ImageController(BlogDbContext context)
        {
            _context = context;
        }

        //Handles image file uploads posted to /Image/Upload.
        //Validates that a file has been provided and is not empty, returning a 400 if not.
        //Ensures the /wwwroot/uploads directory exists, creating it if necessary.
        //Saves the file using a GUID-based name to avoid collisions and to prevent
        //user-supplied filenames from being used directly on disk.
        [HttpPost("Upload")]
        public async Task<IActionResult> Upload(IFormFile image)
        {
            if (image == null || image.Length == 0)
                return BadRequest();

            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads");
            //Create the uploads directory if it doesn't already exist.
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            //Generate a unique filename using a GUID to prevent collisions and
            //avoid exposing or trusting the original user-supplied filename.
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
