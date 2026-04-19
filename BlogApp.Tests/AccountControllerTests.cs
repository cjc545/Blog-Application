using System;
using System.Security.Cryptography;
using System.Text;
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
    public class AccountControllerTests
    {
        //Helpers

        private BlogDbContext CreateInMemoryContext()
        {
            var options = new DbContextOptionsBuilder<BlogDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new BlogDbContext(options);
        }

        ///<summary>
        ///Mirrors the controller's own HashPassword logic so we can pre-hash
        ///passwords when seeding test users.
        ///</summary>
        private string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(bytes);
        }

        ///<summary>
        ///Builds a controller with a mock HttpContext. The sessionValues dictionary
        ///lets us pre-populate session keys, and the capturedSession dictionary
        ///captures any values the controller writes to the session during the test.
        ///</summary>
        private AccountController CreateController(
            BlogDbContext context,
            Dictionary<string, string> sessionValues = null,
            Dictionary<string, string> capturedSession = null)
        {
            var sessionMock = new Mock<ISession>();

            //Allow reading pre-seeded session values (e.g. for logout test)
            sessionValues ??= new Dictionary<string, string>();
            foreach (var kvp in sessionValues)
            {
                var bytes = Encoding.UTF8.GetBytes(kvp.Value);
                sessionMock
                    .Setup(s => s.TryGetValue(kvp.Key, out bytes))
                    .Returns(true);
            }

            //Capture any values written to the session by the controller
            if (capturedSession != null)
            {
                sessionMock
                    .Setup(s => s.Set(It.IsAny<string>(), It.IsAny<byte[]>()))
                    .Callback<string, byte[]>((key, value) =>
                        capturedSession[key] = Encoding.UTF8.GetString(value));
            }

            //Track whether Clear() was called (used in the logout test)
            sessionMock.Setup(s => s.Clear());

            var httpContextMock = new Mock<HttpContext>();
            httpContextMock.Setup(c => c.Session).Returns(sessionMock.Object);

            var tempDataProvider = new Mock<ITempDataProvider>();
            var tempData = new TempDataDictionary(httpContextMock.Object, tempDataProvider.Object);

            return new AccountController(context)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = httpContextMock.Object
                },
                TempData = tempData
            };
        }

        //REGISTER (GET)

        [Fact]
        public void Register_Get_ReturnsView()
        {
            using var context = CreateInMemoryContext();
            var controller = CreateController(context);

            var result = controller.Register();

            Assert.IsType<ViewResult>(result);
        }

        //REGISTER (POST)
     
        [Fact]
        public async Task Register_Post_InvalidModelState_ReturnsViewWithModel()
        {
            using var context = CreateInMemoryContext();
            var controller = CreateController(context);
            controller.ModelState.AddModelError("Name", "Required");

            var model = new RegisterViewModel { Email = "test@example.com", Password = "password123" };
            var result = await controller.Register(model);

            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal(model, viewResult.Model);
        }

        [Fact]
        public async Task Register_Post_DuplicateEmail_ReturnsViewWithModelError()
        {
            using var context = CreateInMemoryContext();

            context.Users.Add(new User
            {
                Name = "Existing User",
                Email = "taken@example.com",
                PasswordHash = HashPassword("somepassword")
            });
            await context.SaveChangesAsync();

            var controller = CreateController(context);

            var model = new RegisterViewModel
            {
                Name = "New User",
                Email = "taken@example.com",
                Password = "newpassword123"
            };
            var result = await controller.Register(model);

            //Should return the view, not redirect
            var viewResult = Assert.IsType<ViewResult>(result);

            //Should have an error on the Email field specifically
            Assert.True(controller.ModelState.ContainsKey("Email"));
            Assert.Equal("Email is already registered",
                controller.ModelState["Email"].Errors[0].ErrorMessage);
        }

        [Fact]
        public async Task Register_Post_ValidNewUser_SavesUserToDatabase()
        {
            using var context = CreateInMemoryContext();
            var controller = CreateController(context);

            var model = new RegisterViewModel
            {
                Name = "Alice",
                Email = "alice@example.com",
                Password = "securepassword"
            };
            await controller.Register(model);

            //One user should now exist in the database
            Assert.Equal(1, await context.Users.CountAsync());

            var savedUser = await context.Users.FirstAsync();
            Assert.Equal("Alice", savedUser.Name);
            Assert.Equal("alice@example.com", savedUser.Email);

            //Password should be stored as a hash, never plain text
            Assert.NotEqual("securepassword", savedUser.PasswordHash);
            Assert.Equal(HashPassword("securepassword"), savedUser.PasswordHash);
        }

        [Fact]
        public async Task Register_Post_ValidNewUser_SetsSessionAndRedirectsToHome()
        {
            using var context = CreateInMemoryContext();
            var capturedSession = new Dictionary<string, string>();
            var controller = CreateController(context, capturedSession: capturedSession);

            var model = new RegisterViewModel
            {
                Name = "Alice",
                Email = "alice@example.com",
                Password = "securepassword"
            };
            var result = await controller.Register(model);

            //Should redirect to Home/Index
            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Index", redirect.ActionName);
            Assert.Equal("Home", redirect.ControllerName);

            //Session should contain the new user's email and name
            Assert.Equal("alice@example.com", capturedSession["UserEmail"]);
            Assert.Equal("Alice", capturedSession["UserName"]);
        }

        //LOGIN (GET)

        [Fact]
        public void Login_Get_ReturnsView()
        {
            using var context = CreateInMemoryContext();
            var controller = CreateController(context);

            var result = controller.Login();

            Assert.IsType<ViewResult>(result);
        }

        //LOGIN (POST)

        [Fact]
        public async Task Login_Post_InvalidModelState_ReturnsViewWithModel()
        {
            using var context = CreateInMemoryContext();
            var controller = CreateController(context);
            controller.ModelState.AddModelError("Email", "Required");

            var model = new LoginViewModel { Email = "", Password = "" };
            var result = await controller.Login(model);

            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal(model, viewResult.Model);
        }

        [Fact]
        public async Task Login_Post_UnknownEmail_ReturnsViewWithGenericError()
        {
            using var context = CreateInMemoryContext();
            var controller = CreateController(context);

            var model = new LoginViewModel
            {
                Email = "nobody@example.com",
                Password = "irrelevant"
            };
            var result = await controller.Login(model);

            Assert.IsType<ViewResult>(result);

            //Error should be generic — must not reveal whether the email exists
            var error = controller.ModelState[""].Errors[0].ErrorMessage;
            Assert.Equal("Invalid Email or Password", error);
        }

        [Fact]
        public async Task Login_Post_WrongPassword_ReturnsViewWithGenericError()
        {
            using var context = CreateInMemoryContext();
            context.Users.Add(new User
            {
                Name = "Alice",
                Email = "alice@example.com",
                PasswordHash = HashPassword("correctpassword")
            });
            await context.SaveChangesAsync();

            var controller = CreateController(context);

            var model = new LoginViewModel
            {
                Email = "alice@example.com",
                Password = "wrongpassword"
            };
            var result = await controller.Login(model);

            Assert.IsType<ViewResult>(result);

            //Same generic error regardless of whether email or password was wrong
            var error = controller.ModelState[""].Errors[0].ErrorMessage;
            Assert.Equal("Invalid Email or Password", error);
        }

        [Fact]
        public async Task Login_Post_CorrectCredentials_SetsSessionAndRedirectsToHome()
        {
            using var context = CreateInMemoryContext();
            context.Users.Add(new User
            {
                Name = "Alice",
                Email = "alice@example.com",
                PasswordHash = HashPassword("correctpassword")
            });
            await context.SaveChangesAsync();

            var capturedSession = new Dictionary<string, string>();
            var controller = CreateController(context, capturedSession: capturedSession);

            var model = new LoginViewModel
            {
                Email = "alice@example.com",
                Password = "correctpassword"
            };
            var result = await controller.Login(model);

            //Should redirect to Home/Index on success
            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Index", redirect.ActionName);
            Assert.Equal("Home", redirect.ControllerName);

            //Session should be populated with the logged-in user's details
            Assert.Equal("alice@example.com", capturedSession["UserEmail"]);
            Assert.Equal("Alice", capturedSession["UserName"]);
        }

        //LOGOUT

        [Fact]
        public void LogOut_ClearsSessionAndRedirectsToHome()
        {
            using var context = CreateInMemoryContext();

            //Track whether Clear() is actually called on the session
            var sessionMock = new Mock<ISession>();
            var clearWasCalled = false;
            sessionMock.Setup(s => s.Clear()).Callback(() => clearWasCalled = true);

            var httpContextMock = new Mock<HttpContext>();
            httpContextMock.Setup(c => c.Session).Returns(sessionMock.Object);

            var tempDataProvider = new Mock<ITempDataProvider>();
            var tempData = new TempDataDictionary(httpContextMock.Object, tempDataProvider.Object);

            var controller = new AccountController(context)
            {
                ControllerContext = new ControllerContext { HttpContext = httpContextMock.Object },
                TempData = tempData
            };

            var result = controller.LogOut();

            //Session must be cleared on logout
            Assert.True(clearWasCalled);

            //Should redirect to Home/Index
            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Index", redirect.ActionName);
            Assert.Equal("Home", redirect.ControllerName);
        }
    }
}