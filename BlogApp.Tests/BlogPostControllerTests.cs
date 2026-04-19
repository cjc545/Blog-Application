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
    public class BlogPostControllerTests
    {
        // Helpers


        ///<summary>
        ///Creates a fresh in-memory BlogDbContext with a unique database name so
        ///each test gets an isolated store.
        ///</summary>
        private BlogDbContext CreateInMemoryContext(string dbName = null)
        {
            var options = new DbContextOptionsBuilder<BlogDbContext>()
                .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
                .Options;
            return new BlogDbContext(options);
        }

        ///<summary>
        ///Builds a controller wired up with a mock HttpContext whose Session can
        ///return a configurable value for "UserEmail" and "UserName".
        ///TempData is also initialised so TempData assignments in actions don't throw.
        ///</summary>
        private BlogPostController CreateController(
            BlogDbContext context,
            string sessionEmail = null,
            string sessionUserName = null)
        {
            var sessionMock = new Mock<ISession>();

            //Helper that makes GetString(key) return the supplied value (or null).
            void SetupSessionKey(string key, string value)
            {
                if (value != null)
                {
                    var bytes = System.Text.Encoding.UTF8.GetBytes(value);
                    sessionMock
                        .Setup(s => s.TryGetValue(key, out bytes))
                        .Returns(true);
                }
                else
                {
                    byte[] noBytes = null;
                    sessionMock
                        .Setup(s => s.TryGetValue(key, out noBytes))
                        .Returns(false);
                }
            }

            SetupSessionKey("UserEmail", sessionEmail);
            SetupSessionKey("UserName", sessionUserName);

            //HttpContext mock
            var httpContextMock = new Mock<HttpContext>();
            httpContextMock.Setup(c => c.Session).Returns(sessionMock.Object);

            //TempData
            var tempDataProvider = new Mock<ITempDataProvider>();
            var tempData = new TempDataDictionary(httpContextMock.Object, tempDataProvider.Object);

            var urlHelperMock = new Mock<IUrlHelper>();
            urlHelperMock
                .Setup(u => u.Action(It.IsAny<Microsoft.AspNetCore.Mvc.Routing.UrlActionContext>()))
                .Returns("/Home/Index");

            var controller = new BlogPostController(context)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = httpContextMock.Object
                },
                TempData = tempData,
                Url = urlHelperMock.Object
            };

            return controller;
        }

        //INDEX

        [Fact]
        public async Task Index_UnauthenticatedUser_RedirectsToLogin()
        {
            using var context = CreateInMemoryContext();
            var controller = CreateController(context, sessionEmail: null);

            //Act
            var result = await controller.Index();

            //Assert
            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Login", redirect.ActionName);
            Assert.Equal("Account", redirect.ControllerName);
        }

        [Fact]
        public async Task Index_AuthenticatedUser_ReturnsViewWithPosts()
        {
            using var context = CreateInMemoryContext();
            var user = new User { ID = 1, Email = "user@example.com", Name = "Alice", PasswordHash = "PasswordHash" };
            context.Users.Add(user);
            context.BlogPosts.AddRange(
                new BlogPost { ID = 1, Title = "First", Content = "Hello world", UserId = 1, PublishedDate = DateTime.UtcNow.AddDays(-1) },
                new BlogPost { ID = 2, Title = "Second", Content = "Hello again", UserId = 1, PublishedDate = DateTime.UtcNow }
            );
            await context.SaveChangesAsync();

            var controller = CreateController(context, sessionEmail: "user@example.com");

            //Act
            var result = await controller.Index();

            //Assert – returns a View with a list of posts ordered newest-first
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<List<BlogPost>>(viewResult.Model);
            Assert.Equal(2, model.Count);
            Assert.Equal(2, model[0].ID); // OrderByDescending(ID) → newest first
        }

        //CREATE (GET)

        [Fact]
        public void Create_Get_UnauthenticatedUser_RedirectsToLogin()
        {
            using var context = CreateInMemoryContext();
            var controller = CreateController(context, sessionEmail: null);

            var result = controller.Create();

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Login", redirect.ActionName);
            Assert.Equal("Account", redirect.ControllerName);
        }

        [Fact]
        public void Create_Get_AuthenticatedUser_ReturnsView()
        {
            using var context = CreateInMemoryContext();
            var controller = CreateController(context, sessionEmail: "user@example.com");

            var result = controller.Create();

            Assert.IsType<ViewResult>(result);
        }

        //CREATE (POST)

        [Fact]
        public async Task Create_Post_EmptyContent_ReturnsViewWithError()
        {
            using var context = CreateInMemoryContext();
            var user = new User { ID = 1, Email = "user@example.com", Name = "Alice", PasswordHash = "PasswordHash" };
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var controller = CreateController(context, sessionEmail: "user@example.com");

            var post = new BlogPost { Title = "Test", Content = "<p><br></p>" };
            var result = await controller.Create(post);

            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal("Your post needs content dude!!!", controller.TempData["BlogPostError"]);
        }

        [Fact]
        public async Task Create_Post_ContentTooShort_ReturnsViewWithError()
        {
            using var context = CreateInMemoryContext();
            var user = new User { ID = 1, Email = "user@example.com", Name = "Alice", PasswordHash = "PasswordHash" };
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var controller = CreateController(context, sessionEmail: "user@example.com");

            // 5 visible characters after stripping tags → below the 10-char minimum
            var post = new BlogPost { Title = "Test", Content = "<p>Hi!</p>" };
            var result = await controller.Create(post);

            Assert.IsType<ViewResult>(result);
            Assert.Equal("Your post needs at least 10 characters", controller.TempData["BlogPostError"]);
        }

        [Fact]
        public async Task Create_Post_ValidContent_SavesAndRedirectsToIndex()
        {
            using var context = CreateInMemoryContext();
            var user = new User { ID = 1, Email = "user@example.com", Name = "Alice", PasswordHash = "PasswordHash" };
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var controller = CreateController(context, sessionEmail: "user@example.com");

            //Content has well over 10 visible characters
            var post = new BlogPost
            {
                Title = "A valid post",
                Content = "<p>This is a perfectly valid blog post with plenty of content.</p>"
            };

            var result = await controller.Create(post);

            //Should redirect to Index on success
            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Index", redirect.ActionName);

            //The post should now exist in the database
            Assert.Equal(1, await context.BlogPosts.CountAsync());
        }

        //DETAILS

        [Fact]
        public async Task Details_UnauthenticatedUser_RedirectsToLogin()
        {
            using var context = CreateInMemoryContext();
            var controller = CreateController(context, sessionEmail: null);

            var result = await controller.Details(1);

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Login", redirect.ActionName);
        }

        [Fact]
        public async Task Details_NonExistentPost_ReturnsNotFound()
        {
            using var context = CreateInMemoryContext();
            var controller = CreateController(context, sessionEmail: "user@example.com");

            var result = await controller.Details(999);

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task Details_ExistingPost_ReturnsViewWithPost()
        {
            using var context = CreateInMemoryContext();
            var user = new User { ID = 1, Email = "user@example.com", Name = "Alice", PasswordHash = "PasswordHash" };
            context.Users.Add(user);
            context.BlogPosts.Add(new BlogPost { ID = 1, Title = "Hello", Content = "World", UserId = 1 });
            await context.SaveChangesAsync();

            var controller = CreateController(context, sessionEmail: "user@example.com");

            var result = await controller.Details(1);

            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<BlogPost>(viewResult.Model);
            Assert.Equal(1, model.ID);
        }

        //EDIT (GET)

        [Fact]
        public async Task Edit_Get_NonOwner_RedirectsToIndexWithError()
        {
            using var context = CreateInMemoryContext();
            var owner = new User { ID = 1, Email = "owner@example.com", Name = "Owner" , PasswordHash = "PasswordHash2" };
            context.Users.Add(owner);
            context.BlogPosts.Add(new BlogPost { ID = 1, Title = "Post", Content = "Content", UserId = 1 });
            await context.SaveChangesAsync();

            //Logged in as a different user
            var controller = CreateController(context, sessionEmail: "other@example.com", sessionUserName: "OtherUser");

            var result = await controller.Edit(1);

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Index", redirect.ActionName);
            Assert.Equal("Can't Edit post that isn't yours! That would be rude!", controller.TempData["BlogListError"]);
        }

        [Fact]
        public async Task Edit_Get_Owner_ReturnsView()
        {
            using var context = CreateInMemoryContext();
            var owner = new User { ID = 1, Email = "owner@example.com", Name = "Alice", PasswordHash = "PasswordHash" };
            context.Users.Add(owner);
            context.BlogPosts.Add(new BlogPost { ID = 1, Title = "Post", Content = "Content", UserId = 1 });
            await context.SaveChangesAsync();

            var controller = CreateController(context, sessionEmail: "owner@example.com", sessionUserName: "Alice");

            var result = await controller.Edit(1);

            Assert.IsType<ViewResult>(result);
        }

        //EDIT (POST)

        [Fact]
        public async Task Edit_Post_MismatchedIds_ReturnsNotFound()
        {
            using var context = CreateInMemoryContext();
            var controller = CreateController(context, sessionEmail: "user@example.com");

            //Route ID (5) doesn't match the model's ID (1)
            var result = await controller.Edit(5, new BlogPost { ID = 1, Title = "X", Content = "Y" });

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task Edit_Post_ValidUpdate_SavesAndRedirects()
        {
            using var context = CreateInMemoryContext();
            var user = new User { ID = 1, Email = "user@example.com", Name = "Alice" , PasswordHash = "PasswordHash" };
            context.Users.Add(user);
            context.BlogPosts.Add(new BlogPost { ID = 1, Title = "Original title", Content = "Original content", UserId = 1 });
            await context.SaveChangesAsync();

            var controller = CreateController(context, sessionEmail: "user@example.com");

            var updatedPost = new BlogPost
            {
                ID = 1,
                Title = "Updated title",
                Content = "Updated content",
                PublishedDate = DateTime.UtcNow
            };

            var result = await controller.Edit(1, updatedPost);

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Index", redirect.ActionName);

            //Verify the changes were persisted
            var saved = await context.BlogPosts.FindAsync(1);
            Assert.Equal("Updated title", saved.Title);
            Assert.Equal("Updated content", saved.Content);
        }

        //DELETE (GET)

        [Fact]
        public async Task Delete_Get_NonOwner_RedirectsToIndexWithError()
        {
            using var context = CreateInMemoryContext();
            var owner = new User { ID = 1, Email = "owner@example.com", Name = "Owner", PasswordHash = "PasswordHash" };
            context.Users.Add(owner);
            context.BlogPosts.Add(new BlogPost { ID = 1, Title = "Post", Content = "Content", UserId = 1 });
            await context.SaveChangesAsync();

            var controller = CreateController(context, sessionEmail: "other@example.com", sessionUserName: "OtherUser");

            var result = await controller.Delete(1);

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Index", redirect.ActionName);
            Assert.Contains("rude", controller.TempData["BlogListError"]?.ToString());
        }

        [Fact]
        public async Task Delete_Get_Owner_ReturnsView()
        {
            using var context = CreateInMemoryContext();
            var owner = new User { ID = 1, Email = "owner@example.com", Name = "Alice", PasswordHash = "PasswordHash" };
            context.Users.Add(owner);
            context.BlogPosts.Add(new BlogPost { ID = 1, Title = "Post", Content = "Content", UserId = 1 });
            await context.SaveChangesAsync();

            var controller = CreateController(context, sessionEmail: "owner@example.com", sessionUserName: "Alice");

            var result = await controller.Delete(1);

            Assert.IsType<ViewResult>(result);
        }

        //DELETE CONFIRMED (POST)

        [Fact]
        public async Task DeleteConfirmed_ExistingPost_RemovesFromDbAndRedirects()
        {
            using var context = CreateInMemoryContext();
            var user = new User { ID = 1, Email = "user@example.com", Name = "Alice", PasswordHash = "PasswordHash" };
            context.Users.Add(user);
            // Content with no <img> tags so no file-system interaction is needed
            context.BlogPosts.Add(new BlogPost { ID = 1, Title = "Post", Content = "<p>No images here</p>", UserId = 1 });
            await context.SaveChangesAsync();

            var controller = CreateController(context, sessionEmail: "user@example.com");

            var result = await controller.DeleteConfirmed(1);

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Index", redirect.ActionName);
            Assert.Equal(0, await context.BlogPosts.CountAsync());
        }
    }
}