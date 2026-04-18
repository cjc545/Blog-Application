using BlogApp.Data;
using BlogApp.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Runtime.Intrinsics.Arm;
using System.Security.Cryptography;
using System.Text;

namespace BlogApp.Controllers
{
    public class AccountController : Controller
    {
        private readonly BlogDbContext _context;

        //Constructor (dependancy injection)
        public AccountController(BlogDbContext context)
        {
            _context = context;
        }
        //Returns the default Account index view.
        public IActionResult Index()
        {
            return View();
        }
        //Returns the Register form view.
        public IActionResult Register()
        {
            return View();
        }

        //Handles the submitted Register form.
        //Validates the model, then check whether the submitted email is already in use.
        //If the email is available, hashes the password and saves the new user to the database.
        //On success, stores the user's email and name in session to log them in immediately,
        //then redirects to the Home index.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (await _context.Users.AnyAsync(u => u.Email == model.Email))
            {
                ModelState.AddModelError("Email", "Email is already registered");
                return View(model);
            }

            string passwordHash = HashPassword(model.Password);

            var user = new User
            {
                Name = model.Name,
                Email = model.Email,
                PasswordHash = passwordHash
            };

            _context.Users.Add(user);

            await _context.SaveChangesAsync();

            //store session for login
            HttpContext.Session.SetString("UserEmail", user.Email);
            HttpContext.Session.SetString("UserName", user.Name);

            return RedirectToAction("Index", "Home");
        }

        //Hashes a plain-text password using SHA-256 and returns the result as a Base64 string.
        //This hash is used both when storing a new password and when verifying a login attempt.
        private string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(bytes);
        }

        // Returns the Login view.
        public IActionResult Login()
        {
            return View();
        }

        //Handles the submitted Login form.
        //Validates the model, then looks up the user by email.
        //If no matching user is found, or the hashed submitted password doesn't match the stored
        //hash, a generic error is shown (intentionally vague to avoid revealing whether
        //the email exists in the system).
        //On success, stores the user's email and name in session and redirects to the Home index.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == model.Email);

            if (user == null || !VerifyPassword(model.Password, user.PasswordHash))
            {
                ModelState.AddModelError("", "Invalid Email or Password");
                return View(model);
            }

            //store session for login
            HttpContext.Session.SetString("UserEmail", user.Email);
            HttpContext.Session.SetString("UserName", user.Name);

            return RedirectToAction("Index", "Home");
        }

        //Compares a plain-text password attempt against a stored hash by hashing
        //the attempt and doing a string equality check. Returns true if they match.
        private bool VerifyPassword(string enteredPassword, string storedHashPassword)
        {
            return HashPassword(enteredPassword) == storedHashPassword;
        }

        //Logs the current user out by clearing all session data,
        //then redirects them to the Home index.
        [HttpPost]
        public IActionResult LogOut()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Index", "Home");
        }

    }
}
