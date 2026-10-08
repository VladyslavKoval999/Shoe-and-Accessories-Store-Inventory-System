using Microsoft.AspNetCore.Mvc;
using BlacksmithStoreWeb.Data;
using BlacksmithStoreWeb.Models;
using System.Security.Cryptography;
using System.Text;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;

namespace BlacksmithStoreWeb.Controllers
{
    public class AccountController : Controller
    {
        private readonly StoreContext _context;

        public AccountController(StoreContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Register() => View();

        [HttpPost]
        public IActionResult Register(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                if (_context.Users.Any(u => u.Username == model.Username))
                {
                    ModelState.AddModelError("Username", "Користувач з таким логіном вже існує.");
                    return View(model);
                }

                var clientRole = _context.Roles.FirstOrDefault(r => r.RoleName == "Client" || r.RoleName == "Клієнт");
                int defaultRoleId = clientRole?.RoleId ?? 2;

                var user = new User
                {
                    Username = model.Username,
                    FullName = $"{model.FirstName} {model.LastName}".Trim(),
                    PasswordHash = HashPassword(model.Password),
                    RoleId = defaultRoleId
                };

                _context.Users.Add(user);
                _context.SaveChanges();

                var random = new Random();
                string cardNumber = $"BS-{random.Next(1000, 9999)}-{random.Next(1000, 9999)}";

                var customer = new Customer
                {
                    UserId = user.UserId,
                    LoyaltyCardNumber = cardNumber,
                    PhoneNumber = model.PhoneNumber,
                    Email = model.Email,
                    PurchasedItemsCount = 0,
                    AvailableBonuses = 0
                };

                _context.Customers.Add(customer);
                _context.SaveChanges();

                return RedirectToAction("Login");
            }
            return View(model);
        }

        [HttpGet]
        public IActionResult Login() => View();

        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (ModelState.IsValid)
            {
                var hash = HashPassword(model.Password);
                var user = _context.Users.FirstOrDefault(u => u.Username == model.Username && u.PasswordHash == hash);

                if (user != null)
                {
                    var role = _context.Roles.FirstOrDefault(r => r.RoleId == user.RoleId);
                    string roleName = role?.RoleName ?? "Client";

                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.Name, user.Username!),
                        new Claim(ClaimTypes.Role, roleName)
                    };

                    var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                    await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));

                    return RedirectToAction("Index", "Home");
                }
                ModelState.AddModelError("", "Невірний логін або пароль.");
            }
            return View(model);
        }

        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var username = User.Identity?.Name;
            if (string.IsNullOrEmpty(username))
            {
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return RedirectToAction("Login");
            }

            var user = _context.Users.FirstOrDefault(u => u.Username == username);
            if (user == null)
            {
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return RedirectToAction("Login");
            }

            var customer = _context.Customers.FirstOrDefault(c => c.UserId == user.UserId);

            var role = _context.Roles.FirstOrDefault(r => r.RoleId == user.RoleId);
            string roleName = role?.RoleName ?? "Client";

            DateTime cardDate = DateTime.Today;
            var oldestOrder = _context.Orders.Where(o => o.UserId == user.UserId).OrderBy(o => o.OrderDate).FirstOrDefault();
            if (oldestOrder != null)
            {
                cardDate = oldestOrder.OrderDate;
            }

            var vm = new ProfileViewModel
            {
                Username = user.Username,
                FullName = user.FullName,
                CardCreationDate = cardDate,
                Role = (roleName.Equals("Admin", StringComparison.OrdinalIgnoreCase) || roleName.Contains("Адмін")) ? "Адміністратор" : "Клієнт",
                LoyaltyCardNumber = customer?.LoyaltyCardNumber ?? "Картка формується",
                PhoneNumber = customer?.PhoneNumber,
                Email = customer?.Email,
                PurchasedItemsCount = customer?.PurchasedItemsCount ?? 0,
                AvailableBonuses = customer?.AvailableBonuses ?? 0,
                RecentOrders = GetUserOrderHistory(user.UserId)
            };

            return View(vm);
        }

        [Authorize]
        [HttpPost]
        public IActionResult UpdateProfile(string? PhoneNumber, string? Email)
        {
            var username = User.Identity?.Name;
            var user = _context.Users.FirstOrDefault(u => u.Username == username);

            if (user != null)
            {
                var customer = _context.Customers.FirstOrDefault(c => c.UserId == user.UserId);
                if (customer != null)
                {
                    customer.PhoneNumber = PhoneNumber;
                    customer.Email = Email;
                    _context.SaveChanges();
                    TempData["SuccessMessage"] = "Контактні дані успішно оновлено!";
                }
            }
            return RedirectToAction("Profile");
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> ChangePassword(ProfileViewModel model)
        {
            var username = User.Identity?.Name;
            if (string.IsNullOrEmpty(username))
            {
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return RedirectToAction("Login");
            }

            var user = _context.Users.FirstOrDefault(u => u.Username == username);
            if (user == null)
            {
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return RedirectToAction("Login");
            }

            var customer = _context.Customers.FirstOrDefault(c => c.UserId == user.UserId);

            var role = _context.Roles.FirstOrDefault(r => r.RoleId == user.RoleId);
            string roleName = role?.RoleName ?? "Client";

            DateTime cardDate = DateTime.Today;
            var oldestOrder = _context.Orders.Where(o => o.UserId == user.UserId).OrderBy(o => o.OrderDate).FirstOrDefault();
            if (oldestOrder != null) cardDate = oldestOrder.OrderDate;

            model.Username = user.Username;
            model.FullName = user.FullName;
            model.CardCreationDate = cardDate;
            model.Role = (roleName.Equals("Admin", StringComparison.OrdinalIgnoreCase) || roleName.Contains("Адмін")) ? "Адміністратор" : "Клієнт";
            model.LoyaltyCardNumber = customer?.LoyaltyCardNumber ?? "BS-NEW-CARD";
            model.PhoneNumber = customer?.PhoneNumber;
            model.Email = customer?.Email;
            model.PurchasedItemsCount = customer?.PurchasedItemsCount ?? 0;
            model.AvailableBonuses = customer?.AvailableBonuses ?? 0;
            model.RecentOrders = GetUserOrderHistory(user.UserId);

            if (ModelState.IsValid)
            {
                var oldHash = HashPassword(model.ChangePassword.OldPassword);
                if (user.PasswordHash != oldHash)
                {
                    ModelState.AddModelError("ChangePassword.OldPassword", "Невірний поточний пароль.");
                    return View("Profile", model);
                }

                user.PasswordHash = HashPassword(model.ChangePassword.NewPassword);
                _context.SaveChanges();

                TempData["SuccessMessage"] = "Пароль успішно змінено!";
                return RedirectToAction("Profile");
            }
            return View("Profile", model);
        }

        [Authorize]
        [HttpPost]
        public IActionResult CancelOrder(int orderId)
        {
            var username = User.Identity?.Name;
            var user = _context.Users.FirstOrDefault(u => u.Username == username);
            if (user == null) return RedirectToAction("Login");

            var order = _context.Orders.FirstOrDefault(o => o.OrderId == orderId && o.UserId == user.UserId);
            if (order != null && order.Status == "Очікує збору")
            {
                order.Status = "Скасовано";

                if (!string.IsNullOrEmpty(order.PaymentMethod) && order.PaymentMethod.Contains("(Використано бонус)"))
                {
                    var customer = _context.Customers.FirstOrDefault(c => c.UserId == user.UserId);
                    if (customer != null)
                    {
                        customer.AvailableBonuses += 1;
                        order.PaymentMethod = order.PaymentMethod.Replace(" (Використано бонус)", "");
                    }
                }

                var orderItems = _context.OrderItems.Where(oi => oi.OrderId == order.OrderId).ToList();
                foreach (var item in orderItems)
                {
                    var stock = _context.Stocks.FirstOrDefault(s => s.StockId == item.StockId);
                    if (stock != null)
                    {
                        stock.Quantity += item.Quantity;
                    }
                }

                _context.SaveChanges();
                TempData["SuccessMessage"] = $"Замовлення #{orderId} було успішно скасовано. Товари та бонуси (якщо використовувались) повернуто.";
            }

            return RedirectToAction("Profile");
        }

        [Authorize]
        [HttpPost]
        public IActionResult CancelOrderItem(int orderId, string? productName, string? size)
        {
            var username = User.Identity?.Name;
            var user = _context.Users.FirstOrDefault(u => u.Username == username);
            if (user == null) return RedirectToAction("Login");

            var order = _context.Orders.FirstOrDefault(o => o.OrderId == orderId && o.UserId == user.UserId);
            if (order == null || order.Status != "Очікує збору")
            {
                TempData["ErrorMessage"] = "Це замовлення вже обробляється, його неможливо змінити.";
                return RedirectToAction("Profile");
            }

            if (string.IsNullOrEmpty(productName))
            {
                return RedirectToAction("Profile");
            }

            var product = _context.Products.FirstOrDefault(p => p.Name == productName);
            if (product == null)
            {
                TempData["ErrorMessage"] = "Товар не знайдено в базі даних.";
                return RedirectToAction("Profile");
            }

            var sizeRecord = string.IsNullOrEmpty(size) ? null : _context.Sizes.FirstOrDefault(s => s.Value == size);
            var stockQuery = _context.Stocks.Where(s => s.ProductId == product.ProductId);
            var stockRecord = sizeRecord != null ? stockQuery.FirstOrDefault(s => s.SizeId == sizeRecord.SizeId) : stockQuery.FirstOrDefault();

            if (stockRecord != null)
            {
                var orderItem = _context.OrderItems.FirstOrDefault(oi => oi.OrderId == orderId && oi.StockId == stockRecord.StockId && oi.Quantity > 0);

                if (orderItem != null)
                {
                    stockRecord.Quantity += orderItem.Quantity;
                    decimal refundAmount = orderItem.Quantity * orderItem.PriceAtTimeOfSale;
                    order.TotalAmount = Math.Max(0, order.TotalAmount - refundAmount);
                    orderItem.Quantity = 0;

                    _context.SaveChanges();

                    bool hasActiveItems = _context.OrderItems.Any(oi => oi.OrderId == orderId && oi.Quantity > 0);
                    if (!hasActiveItems)
                    {
                        order.Status = "Скасовано";
                        _context.SaveChanges();
                        TempData["SuccessMessage"] = "Всі товари скасовано. Замовлення повністю скасоване!";
                    }
                    else
                    {
                        TempData["SuccessMessage"] = "Товар успішно скасовано, суму замовлення перераховано!";
                    }
                }
            }

            return RedirectToAction("Profile");
        }

        private string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                var builder = new StringBuilder();
                foreach (var b in bytes) builder.Append(b.ToString("x2"));
                return builder.ToString();
            }
        }

        private List<OrderHistoryViewModel> GetUserOrderHistory(int userId)
        {
            var orders = _context.Orders
                .Where(o => o.UserId == userId)
                .OrderByDescending(o => o.OrderDate)
                .ToList();

            var history = new List<OrderHistoryViewModel>();
            if (!orders.Any()) return history;

            var orderIds = orders.Select(o => o.OrderId).ToList();

            var allItems = (from oi in _context.OrderItems
                            join st in _context.Stocks on oi.StockId equals st.StockId
                            join p in _context.Products on st.ProductId equals p.ProductId
                            join sz in _context.Sizes on st.SizeId equals sz.SizeId into szGroup
                            from sz in szGroup.DefaultIfEmpty()
                            join c in _context.Colors on st.ColorId equals c.ColorId into cGroup
                            from c in cGroup.DefaultIfEmpty()
                            where orderIds.Contains(oi.OrderId)
                            select new
                            {
                                oi.OrderId,
                                ProductName = p.Name,
                                ProductImage = p.Images,
                                Size = sz != null ? sz.Value : null,
                                Color = c != null ? c.Name : null,
                                Quantity = oi.Quantity,
                                Price = oi.PriceAtTimeOfSale
                            }).ToList();

            foreach (var o in orders)
            {
                var itemsForOrder = allItems.Where(i => i.OrderId == o.OrderId).Select(i => new OrderHistoryItemDto
                {
                    ProductName = i.ProductName ?? "Товар",
                    ProductImage = !string.IsNullOrEmpty(i.ProductImage) ? i.ProductImage.Split(',')[0].Trim() : null,
                    Size = i.Size,
                    Color = i.Color,
                    Quantity = i.Quantity,
                    Price = i.Price
                }).ToList();

                string statusVal = "Очікує збору";
                if (!string.IsNullOrEmpty(o.Status)) statusVal = o.Status;

                history.Add(new OrderHistoryViewModel
                {
                    OrderId = o.OrderId,
                    OrderDate = o.OrderDate,
                    TotalAmount = o.TotalAmount,
                    PaymentMethod = o.PaymentMethod ?? "Готівкою/Карткою",
                    Status = statusVal,
                    DeliveryAddress = o.DeliveryAddress,
                    Comment = o.Comment,
                    TotalItemsCount = itemsForOrder.Sum(x => x.Quantity),
                    Items = itemsForOrder
                });
            }

            return history;
        }
    }
}