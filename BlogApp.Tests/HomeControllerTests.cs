using System;
using System.Collections.Generic;
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
    public class HomeControllerTests
    {
        //Helpers

        private BlogDbContext CreateInMemoryContext()
        {
            var options = new DbContextOptionsBuilder<BlogDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new BlogDbContext(options);
        }

        private HomeController CreateController(BlogDbContext context, string traceIdentifier = "test-trace-id")
        {
            var httpContextMock = new Mock<HttpContext>();
            httpContextMock
                .Setup(c => c.TraceIdentifier)
                .Returns(traceIdentifier);

            var tempDataProvider = new Mock<ITempDataProvider>();
            var tempData = new TempDataDictionary(httpContextMock.Object, tempDataProvider.Object);

            return new HomeController(context)
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
        public async Task Index_EmptyDatabase_ReturnsViewWithEmptyList()
        {
            using var context = CreateInMemoryContext();
            var controller = CreateController(context);

            var result = await controller.Index();

            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<List<BlogPost>>(viewResult.Model);
            Assert.Empty(model);
        }

        [Fact]
        public async Task Index_WithPosts_ReturnsViewWithAllPosts()
        {
            using var context = CreateInMemoryContext();
            var user = new User { ID = 1, Name = "Alice", Email = "alice@example.com", PasswordHash = "hash" };
            context.Users.Add(user);
            context.BlogPosts.AddRange(
                new BlogPost { ID = 1, Title = "First Post", Content = "Content one", UserId = 1, PublishedDate = DateTime.UtcNow },
                new BlogPost { ID = 2, Title = "Second Post", Content = "Content two", UserId = 1, PublishedDate = DateTime.UtcNow },
                new BlogPost { ID = 3, Title = "Third Post", Content = "Content three", UserId = 1, PublishedDate = DateTime.UtcNow }
            );
            await context.SaveChangesAsync();

            var controller = CreateController(context);

            var result = await controller.Index();

            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<List<BlogPost>>(viewResult.Model);
            Assert.Equal(3, model.Count);
        }

        [Fact]
        public async Task Index_PostsIncludeUserData()
        {
            //Verify that the User navigation property is loaded,
            //the homepage view needs author names to render post summaries
            using var context = CreateInMemoryContext();
            var user = new User { ID = 1, Name = "Alice", Email = "alice@example.com", PasswordHash = "hash" };
            context.Users.Add(user);
            context.BlogPosts.Add(
                new BlogPost { ID = 1, Title = "Post", Content = "Content", UserId = 1, PublishedDate = DateTime.UtcNow }
            );
            await context.SaveChangesAsync();

            var controller = CreateController(context);

            var result = await controller.Index();

            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<List<BlogPost>>(viewResult.Model);
            Assert.NotNull(model[0].User);
            Assert.Equal("Alice", model[0].User.Name);
        }

        //PRIVACY

        [Fact]
        public void Privacy_ReturnsView()
        {
            using var context = CreateInMemoryContext();
            var controller = CreateController(context);

            var result = controller.Privacy();

            Assert.IsType<ViewResult>(result);
        }

        //ERROR

        [Fact]
        public void Error_ReturnsViewWithRequestId()
        {
            using var context = CreateInMemoryContext();
            //Activity.Current is null in a test environment, so the controller
            //falls back to HttpContext.TraceIdentifier, we can verify that path here
            var controller = CreateController(context, traceIdentifier: "test-trace-abc-123");

            var result = controller.Error();

            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<ErrorViewModel>(viewResult.Model);
            Assert.Equal("test-trace-abc-123", model.RequestId);
        }

        [Fact]
        public void Error_RequestId_IsNeverNullOrEmpty()
        {
            //The RequestId is used to display diagnostic info on the error page,
            //it should always have a value so the view can render correctly
            using var context = CreateInMemoryContext();
            var controller = CreateController(context, traceIdentifier: "fallback-id");

            var result = controller.Error();

            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<ErrorViewModel>(viewResult.Model);
            Assert.False(string.IsNullOrEmpty(model.RequestId));
        }
    }
}