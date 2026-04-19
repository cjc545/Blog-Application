using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BlogApp.Controllers;
using BlogApp.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace BlogApp.Controllers
{
    public class ImageControllerTests : IDisposable
    {
        //The controller writes files to wwwroot/uploads relative to
        //Directory.GetCurrentDirectory(), so we point that at a temp folder
        //for the duration of each test, then clean up afterwards.

        private readonly string _originalWorkingDirectory;
        private readonly string _tempRoot;
        private readonly string _uploadsFolder;

        public ImageControllerTests()
        {
            _originalWorkingDirectory = Directory.GetCurrentDirectory();

            //Create an isolated temp directory to act as the app root
            _tempRoot = Path.Combine(Path.GetTempPath(), "ImageControllerTests_" + Guid.NewGuid());
            Directory.CreateDirectory(_tempRoot);
            Directory.SetCurrentDirectory(_tempRoot);

            _uploadsFolder = Path.Combine(_tempRoot, "wwwroot", "uploads");
        }

        public void Dispose()
        {
            //Restore the working directory and delete temp files after each test
            Directory.SetCurrentDirectory(_originalWorkingDirectory);
            if (Directory.Exists(_tempRoot))
                Directory.Delete(_tempRoot, recursive: true);
        }

        //Helpers

        private BlogDbContext CreateInMemoryContext()
        {
            var options = new DbContextOptionsBuilder<BlogDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new BlogDbContext(options);
        }

        private ImageController CreateController(BlogDbContext context)
        {
            var httpContextMock = new Mock<HttpContext>();

            return new ImageController(context)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = httpContextMock.Object
                }
            };
        }

        ///<summary>
        ///Builds a mock IFormFile with the given filename and content.
        ///</summary>
        private IFormFile CreateMockFile(string fileName, string content = "fake image bytes")
        {
            var bytes = Encoding.UTF8.GetBytes(content);
            var stream = new MemoryStream(bytes);

            var fileMock = new Mock<IFormFile>();
            fileMock.Setup(f => f.FileName).Returns(fileName);
            fileMock.Setup(f => f.Length).Returns(bytes.Length);
            fileMock.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .Callback<Stream, CancellationToken>((target, _) => stream.CopyTo(target))
                .Returns(Task.CompletedTask);

            return fileMock.Object;
        }

        //UPLOAD

        [Fact]
        public async Task Upload_NullFile_ReturnsBadRequest()
        {
            using var context = CreateInMemoryContext();
            var controller = CreateController(context);

            var result = await controller.Upload(null);

            Assert.IsType<BadRequestResult>(result);
        }

        [Fact]
        public async Task Upload_EmptyFile_ReturnsBadRequest()
        {
            using var context = CreateInMemoryContext();
            var controller = CreateController(context);

            //A file with zero bytes should be rejected
            var emptyFileMock = new Mock<IFormFile>();
            emptyFileMock.Setup(f => f.Length).Returns(0);
            emptyFileMock.Setup(f => f.FileName).Returns("empty.png");

            var result = await controller.Upload(emptyFileMock.Object);

            Assert.IsType<BadRequestResult>(result);
        }

        [Fact]
        public async Task Upload_ValidFile_ReturnsUrlPath()
        {
            using var context = CreateInMemoryContext();
            var controller = CreateController(context);
            var file = CreateMockFile("photo.jpg");

            var result = await controller.Upload(file);

            // hould return the URL path to the uploaded file, not a view
            var contentResult = Assert.IsType<ContentResult>(result);
            Assert.StartsWith("/uploads/", contentResult.Content);
        }

        [Fact]
        public async Task Upload_ValidFile_ReturnedPathEndsWithOriginalExtension()
        {
            using var context = CreateInMemoryContext();
            var controller = CreateController(context);
            var file = CreateMockFile("diagram.png");

            var result = await controller.Upload(file);

            var contentResult = Assert.IsType<ContentResult>(result);
            Assert.EndsWith(".png", contentResult.Content);
        }

        [Fact]
        public async Task Upload_ValidFile_ReturnedFilenameIsNotOriginalFilename()
        {
            //The controller must use a GUID-based name, never the user-supplied filename,
            //to prevent path traversal attacks and filename collisions
            using var context = CreateInMemoryContext();
            var controller = CreateController(context);
            var file = CreateMockFile("my-photo.jpg");

            var result = await controller.Upload(file);

            var contentResult = Assert.IsType<ContentResult>(result);
            Assert.DoesNotContain("my-photo", contentResult.Content);
        }

        [Fact]
        public async Task Upload_ValidFile_FileIsWrittenToDisk()
        {
            using var context = CreateInMemoryContext();
            var controller = CreateController(context);
            var file = CreateMockFile("photo.jpg", content: "fake image data");

            var result = await controller.Upload(file);

            //Extract the filename from the returned URL and check it exists on disk
            var contentResult = Assert.IsType<ContentResult>(result);
            var fileName = Path.GetFileName(contentResult.Content); // e.g. "abc123.jpg"
            var fullPath = Path.Combine(_uploadsFolder, fileName);

            Assert.True(File.Exists(fullPath), $"Expected file to exist at: {fullPath}");
        }

        [Fact]
        public async Task Upload_ValidFile_CreatesUploadsFolderIfMissing()
        {
            //The uploads directory should be created on demand if it doesn't exist yet
            using var context = CreateInMemoryContext();
            var controller = CreateController(context);

            //Confirm the uploads folder does not exist before the upload
            Assert.False(Directory.Exists(_uploadsFolder));

            var file = CreateMockFile("photo.jpg");
            await controller.Upload(file);

            Assert.True(Directory.Exists(_uploadsFolder));
        }

        [Fact]
        public async Task Upload_ValidFile_UploadsFolderAlreadyExists_DoesNotThrow()
        {
            //Uploading when the directory already exists should work without errors
            using var context = CreateInMemoryContext();
            var controller = CreateController(context);

            Directory.CreateDirectory(_uploadsFolder);

            var file = CreateMockFile("photo.jpg");
            var ex = await Record.ExceptionAsync(() => controller.Upload(file));

            Assert.Null(ex);
        }

        [Fact]
        public async Task Upload_TwoFilesWithSameName_BothSavedWithDifferentFilenames()
        {
            //Each upload must produce a unique GUID filename even if the original
            //filenames are identical, so neither file overwrites the other
            using var context = CreateInMemoryContext();
            var controller = CreateController(context);

            var file1 = CreateMockFile("image.jpg", content: "first file");
            var file2 = CreateMockFile("image.jpg", content: "second file");

            var result1 = await controller.Upload(file1);
            var result2 = await controller.Upload(file2);

            var path1 = ((ContentResult)result1).Content;
            var path2 = ((ContentResult)result2).Content;

            Assert.NotEqual(path1, path2);
        }

        [Fact]
        public async Task Upload_ValidFile_PreservesFileExtensionCaseInsensitive()
        {
            //Extensions like .JPG or .PNG should be preserved as supplied
            using var context = CreateInMemoryContext();
            var controller = CreateController(context);
            var file = CreateMockFile("photo.JPG");

            var result = await controller.Upload(file);

            var contentResult = Assert.IsType<ContentResult>(result);
            Assert.EndsWith(".JPG", contentResult.Content);
        }
    }
}