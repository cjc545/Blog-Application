using System;
using System.Threading.Tasks;
using BlogApp.Controllers;
using BlogApp.Data;
using BlogApp.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace BlogApp.Controllers
{
    public class CommentsControllerTests
    {
        //Helpers

        private BlogDbContext CreateInMemoryContext()
        {
            var options = new DbContextOptionsBuilder<BlogDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new BlogDbContext(options);
        }

        private CommentsController CreateController(
            BlogDbContext context,
            string sessionUserName = null,
            Dictionary<string, string> capturedSession = null)
        {
            var sessionMock = new Mock<ISession>();

            //Set up a readable UserName session value if provided
            if (sessionUserName != null)
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(sessionUserName);
                sessionMock
                    .Setup(s => s.TryGetValue("UserName", out bytes))
                    .Returns(true);
            }
            else
            {
                byte[] noBytes = null;
                sessionMock
                    .Setup(s => s.TryGetValue("UserName", out noBytes))
                    .Returns(false);
            }

            var httpContextMock = new Mock<HttpContext>();
            httpContextMock.Setup(c => c.Session).Returns(sessionMock.Object);

            var tempDataProvider = new Mock<ITempDataProvider>();
            var tempData = new TempDataDictionary(httpContextMock.Object, tempDataProvider.Object);

            return new CommentsController(context)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = httpContextMock.Object
                },
                TempData = tempData
            };
        }

        //INDEX

        [Fact]
        public void Index_ReturnsView()
        {
            using var context = CreateInMemoryContext();
            var controller = CreateController(context);

            var result = controller.Index();

            Assert.IsType<ViewResult>(result);
        }

        //CREATE (POST)

        [Fact]
        public async Task Create_EmptyComment_RedirectsToPostDetailsWithError()
        {
            using var context = CreateInMemoryContext();
            var controller = CreateController(context, sessionUserName: "Alice");

            var result = await controller.Create(PostId: 1, User: "Alice", UserComments: "");

            //Should redirect back to the post, not save anything
            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Details", redirect.ActionName);
            Assert.Equal("Post", redirect.ControllerName);
            Assert.Equal(1, redirect.RouteValues["Id"]);

            //Error message should be set in TempData
            Assert.Equal("Name and Comments cannot be Empty", controller.TempData["Error"]);

            //Nothing should have been saved to the database
            Assert.Equal(0, await context.Comments.CountAsync());
        }

        [Fact]
        public async Task Create_WhitespaceOnlyComment_RedirectsToPostDetailsWithError()
        {
            //Whitespace should be treated the same as empty — IsNullOrWhiteSpace covers this
            using var context = CreateInMemoryContext();
            var controller = CreateController(context, sessionUserName: "Alice");

            var result = await controller.Create(PostId: 1, User: "Alice", UserComments: "   ");

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Details", redirect.ActionName);
            Assert.Equal("Post", redirect.ControllerName);

            Assert.Equal("Name and Comments cannot be Empty", controller.TempData["Error"]);
            Assert.Equal(0, await context.Comments.CountAsync());
        }

        [Fact]
        public async Task Create_NullComment_RedirectsToPostDetailsWithError()
        {
            using var context = CreateInMemoryContext();
            var controller = CreateController(context, sessionUserName: "Alice");

            var result = await controller.Create(PostId: 1, User: "Alice", UserComments: null);

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Details", redirect.ActionName);
            Assert.Equal("Post", redirect.ControllerName);

            Assert.Equal("Name and Comments cannot be Empty", controller.TempData["Error"]);
            Assert.Equal(0, await context.Comments.CountAsync());
        }

        [Fact]
        public async Task Create_ValidComment_SavesCommentToDatabase()
        {
            using var context = CreateInMemoryContext();
            var controller = CreateController(context, sessionUserName: "Alice");

            await controller.Create(PostId: 5, User: "Alice", UserComments: "Great post!");

            //Exactly one comment should now be in the database
            Assert.Equal(1, await context.Comments.CountAsync());

            var saved = await context.Comments.FirstAsync();
            Assert.Equal(5, saved.PostId);
            Assert.Equal("Great post!", saved.Content);
        }

        [Fact]
        public async Task Create_ValidComment_AuthorIsSetFromSession_NotFormInput()
        {
            //The controller ignores the User parameter and reads the author from
            //session instead — this test verifies that behaviour is correct, since
            //trusting user-supplied input for the author field would be a security risk
            using var context = CreateInMemoryContext();
            var controller = CreateController(context, sessionUserName: "RealSessionUser");

            //Pass a different name via the form parameter
            await controller.Create(PostId: 1, User: "SpoofedUser", UserComments: "Hello!");

            var saved = await context.Comments.FirstAsync();
            Assert.Equal("RealSessionUser", saved.Author);
            Assert.NotEqual("SpoofedUser", saved.Author);
        }

        [Fact]
        public async Task Create_ValidComment_TimestampIsSetToUtcNow()
        {
            using var context = CreateInMemoryContext();
            var controller = CreateController(context, sessionUserName: "Alice");

            var before = DateTime.UtcNow;
            await controller.Create(PostId: 1, User: "Alice", UserComments: "Nice post!");
            var after = DateTime.UtcNow;

            var saved = await context.Comments.FirstAsync();

            //Timestamp should fall within the window of when the test ran
            Assert.True(saved.CreateAt >= before && saved.CreateAt <= after);
        }

        [Fact]
        public async Task Create_ValidComment_RedirectsToBlogPostDetailsWithSuccess()
        {
            using var context = CreateInMemoryContext();
            var controller = CreateController(context, sessionUserName: "Alice");

            var result = await controller.Create(PostId: 7, User: "Alice", UserComments: "Really enjoyed this.");

            //On success, should redirect to BlogPost/Details (not Post/Details)
            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Details", redirect.ActionName);
            Assert.Equal("BlogPost", redirect.ControllerName);
            Assert.Equal(7, redirect.RouteValues["ID"]);

            //Success message should be set in TempData
            Assert.Equal("Comments added Successfully", controller.TempData["Success"]);
        }

        [Fact]
        public async Task Create_ValidComment_MultipleComments_AllSavedToCorrectPosts()
        {
            //Verify comments are correctly associated with their respective posts
            //when multiple posts receive comments
            using var context = CreateInMemoryContext();
            var controller = CreateController(context, sessionUserName: "Alice");

            await controller.Create(PostId: 1, User: "Alice", UserComments: "Comment on post 1");
            await controller.Create(PostId: 2, User: "Alice", UserComments: "Comment on post 2");
            await controller.Create(PostId: 1, User: "Alice", UserComments: "Another comment on post 1");

            Assert.Equal(3, await context.Comments.CountAsync());
            Assert.Equal(2, await context.Comments.CountAsync(c => c.PostId == 1));
            Assert.Equal(1, await context.Comments.CountAsync(c => c.PostId == 2));
        }
    }
}