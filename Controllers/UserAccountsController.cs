using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Reports.Data;
using Reports.Models;
using System;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Reports.Controllers
{
    public class UserAccountsController : Controller
    {
        private readonly ReportsDbContext _context;

        public UserAccountsController(ReportsDbContext context)
        {
            _context = context;
        }

        // GET: UserAccounts
        [Authorize]
        public async Task<IActionResult> Index()
        {
            var users = await _context.UserAccounts.ToListAsync();
            return View(users);
        }

        // GET: UserAccounts/Details/5
        [Authorize]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var user = await _context.UserAccounts
                .Include(u => u.CreatedVouchers)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (user == null) return NotFound();

            return View(user);
        }

        // GET: UserAccounts/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: UserAccounts/Create
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(UserAccount user)
        {
            if (ModelState.IsValid)
            {
                user.CreatedAt = DateTime.UtcNow;

                // Ensure default role is User if not set
                if (!Enum.IsDefined(typeof(UserRole), user.Role))
                {
                    user.Role = UserRole.User;
                }

                // Hash password + confirm password
                if (!string.IsNullOrWhiteSpace(user.Password))
                {
                    var hashed = HashPassword(user.Password);
                    user.Password = hashed;
                    user.ConfirmPassword = hashed;
                }

                _context.Add(user);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            return View(user);
        }

        // GET: UserAccounts/Edit/5
        [Authorize]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var user = await _context.UserAccounts.FindAsync(id);
            if (user == null) return NotFound();

            return View(user);
        }

        // POST: UserAccounts/Edit/5
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, UserAccount user)
        {
            if (id != user.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    var existingUser = await _context.UserAccounts.FindAsync(id);
                    if (existingUser == null) return NotFound();

                    // Update fields
                    existingUser.Username = user.Username;
                    existingUser.Email = user.Email;
                    existingUser.FullName = user.FullName;
                    existingUser.PhoneNumber = user.PhoneNumber;
                    existingUser.Role = user.Role;

                    // Only update password if changed
                    if (!string.IsNullOrWhiteSpace(user.Password))
                    {
                        var hashed = HashPassword(user.Password);
                        existingUser.Password = hashed;
                        existingUser.ConfirmPassword = hashed;
                    }

                    existingUser.UpdatedAt = DateTime.UtcNow;

                    _context.Update(existingUser);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!UserAccountExists(user.Id))
                        return NotFound();
                    else
                        throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(user);
        }

        // GET: UserAccounts/Delete/5
        [Authorize]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var user = await _context.UserAccounts
                .FirstOrDefaultAsync(m => m.Id == id);

            if (user == null) return NotFound();

            return View(user);
        }

        // POST: UserAccounts/Delete/5
        [HttpPost, ActionName("Delete")]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var user = await _context.UserAccounts.FindAsync(id);
            if (user != null)
            {
                _context.UserAccounts.Remove(user);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        private bool UserAccountExists(int id)
        {
            return _context.UserAccounts.Any(e => e.Id == id);
        }

        // ----------------------
        // REGISTER
        // ----------------------
        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(UserAccount model)
        {
            if (!ModelState.IsValid)
                return View(model);

            // Check if email exists
            var existingEmail = await _context.UserAccounts
                .FirstOrDefaultAsync(u => u.Email == model.Email);

            if (existingEmail != null)
            {
                ModelState.AddModelError("Email", "Email is already registered.");
                return View(model);
            }

            // Check if username exists
            var existingUsername = await _context.UserAccounts
                .FirstOrDefaultAsync(u => u.Username == model.Username);

            if (existingUsername != null)
            {
                ModelState.AddModelError("Username", "Username is already taken.");
                return View(model);
            }

            // Hash the password + confirm password
            model.Password = HashPassword(model.Password);
            model.ConfirmPassword = model.Password;

            // Set default values
            model.Role = UserRole.User;
            model.CreatedAt = DateTime.UtcNow;

            _context.UserAccounts.Add(model);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Registration successful! Please login.";
            return RedirectToAction("Login");
        }

        // ----------------------
        // LOGIN
        // ----------------------
        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, bool rememberMe = false)
        {
            if (!ModelState.IsValid)
                return View(model);

            // Find user by username
            var user = await _context.UserAccounts
                .FirstOrDefaultAsync(u => u.Username == model.Username);

            if (user == null || !VerifyPassword(model.Password, user.Password))
            {
                ModelState.AddModelError(string.Empty, "Invalid username or password.");
                return View(model);
            }

            // Update last login
            user.LastLoginAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            // Create claims
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.FullName ?? user.Username),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role.ToString())
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

            var authProperties = new AuthenticationProperties
            {
                IsPersistent = rememberMe,
                ExpiresUtc = rememberMe ? DateTimeOffset.UtcNow.AddDays(30) : DateTimeOffset.UtcNow.AddHours(8)
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties
            );

            TempData["Success"] = $"Welcome back, {user.FullName ?? user.Username}!";

            // Redirect based on role
            return RedirectToAction("Index", "Dashboard");
        }

        // ----------------------
        // LOGOUT
        // ----------------------
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            TempData["Success"] = "You have been logged out successfully.";
            return RedirectToAction("Login");
        }

        // ----------------------
        // PASSWORD HASHING METHODS
        // ----------------------
        private string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                return BitConverter.ToString(hashedBytes).Replace("-", "").ToLower();
            }
        }

        private bool VerifyPassword(string enteredPassword, string storedPassword)
        {
            var hashedEnteredPassword = HashPassword(enteredPassword);
            return hashedEnteredPassword == storedPassword;
        }

        // ----------------------
        // RESET PASSWORD
        // ----------------------
        [HttpGet]
        public IActionResult ResetPassword()
        {
            return View(new ForgotPasswordViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = _context.UserAccounts.FirstOrDefault(u => u.Email == model.Email);
            if (user == null)
            {
                ModelState.AddModelError("", "No account found with that email.");
                return View(model);
            }

            // Update password (hash both fields)
            var hashed = HashPassword(model.NewPassword);
            user.Password = hashed;
            user.ConfirmPassword = hashed;

            user.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            TempData["Message"] = "Password has been reset successfully. You can now log in.";
            return RedirectToAction("Login");
        }

        // GET: /Account/AccessDenied
        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}
