using Microsoft.AspNetCore.Mvc;
using BlacksmithStoreWeb.Data;
using BlacksmithStoreWeb.Models;
using Microsoft.AspNetCore.Authorization;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BlacksmithStoreWeb.Controllers
{
    public class RestockController : Controller
    {
        private readonly StoreContext _context;

        public RestockController(StoreContext context)
        {
            _context = context;
        }

        private List<string> GetExpectedSizes(int? categoryId)
        {
            var category = _context.Categories.FirstOrDefault(c => c.CategoryId == categoryId);
            string catName = category?.Name?.ToLower() ?? "";

            if (categoryId == 1 || catName.Contains("чоловік") || catName.Contains("чоловіче"))
                return Enumerable.Range(41, 10).Select(n => n.ToString()).ToList();

            if (categoryId == 2 || catName.Contains("жіноче") || catName.Contains("жінк"))
                return Enumerable.Range(36, 10).Select(n => n.ToString()).ToList();

            if (categoryId == 3 || catName.Contains("дитяче") || catName.Contains("діт"))
                return Enumerable.Range(21, 21).Select(n => n.ToString()).ToList();

            return Enumerable.Range(36, 15).Select(n => n.ToString()).ToList();
        }

        [HttpGet]
        public IActionResult Index()
        {
            var allProducts = _context.Products.ToList();
            var allSizes = _context.Sizes.ToList();
            var allStocks = _context.Stocks.ToList();

            var restockList = new List<Product>();

            foreach (var p in allProducts)
            {
                if (p.ProductType == "Взуття")
                {
                    var expectedSizes = GetExpectedSizes(p.CategoryId);

                    var availableSizeIds = allStocks
                        .Where(s => s.ProductId == p.ProductId && s.Quantity > 0 && s.SizeId != null)
                        .Select(s => s.SizeId)
                        .ToList();

                    var availableSizeValues = allSizes
                        .Where(sz => availableSizeIds.Contains(sz.SizeId))
                        .Select(sz => sz.Value)
                        .ToList();

                    var missingSizes = expectedSizes.Except(availableSizeValues).ToList();

                    if (missingSizes.Any())
                    {
                        restockList.Add(p);
                    }
                }
                else
                {
                    var stock = allStocks.FirstOrDefault(s => s.ProductId == p.ProductId);
                    if (stock == null || stock.Quantity <= 0)
                    {
                        restockList.Add(p);
                    }
                }
            }

            return View(restockList);
        }

        [HttpGet]
        public IActionResult Details(int id)
        {
            var product = _context.Products.FirstOrDefault(p => p.ProductId == id);
            if (product == null) return NotFound();

            var outOfStockSizes = new List<string>();

            if (product.ProductType == "Взуття")
            {
                var expectedSizes = GetExpectedSizes(product.CategoryId);

                var availableSizeIds = _context.Stocks
                    .Where(s => s.ProductId == id && s.Quantity > 0 && s.SizeId != null)
                    .Select(s => s.SizeId)
                    .ToList();

                var availableSizeValues = _context.Sizes
                    .Where(sz => availableSizeIds.Contains(sz.SizeId))
                    .Select(sz => sz.Value)
                    .ToList();

                outOfStockSizes = expectedSizes.Except(availableSizeValues).ToList();
            }

            var vm = new RestockProductDetailsViewModel
            {
                Product = product,
                OutOfStockSizes = outOfStockSizes
            };

            return View(vm);
        }

        [Authorize]
        [HttpPost]
        public IActionResult RequestNotification(int productId, string? size)
        {
            var username = User.Identity?.Name;
            var user = _context.Users.FirstOrDefault(u => u.Username == username);
            if (user == null) return RedirectToAction("Login", "Account");

            var customer = _context.Customers.FirstOrDefault(c => c.UserId == user.UserId);
            if (customer == null || string.IsNullOrEmpty(customer.Email))
            {
                TempData["ErrorMessage"] = "Будь ласка, вкажіть ваш Email у профілі, щоб ми могли надіслати вам сповіщення.";
                return RedirectToAction("Profile", "Account");
            }

            bool alreadyRequested = _context.RestockRequests
                .Any(r => r.UserId == user.UserId && r.ProductId == productId && r.Size == size && !r.IsFulfilled);

            if (!alreadyRequested)
            {
                var request = new RestockRequest
                {
                    UserId = user.UserId,
                    ProductId = productId,
                    Size = size,
                    RequestDate = DateTime.Now,
                    IsFulfilled = false
                };

                _context.RestockRequests.Add(request);
                _context.SaveChanges();

                string sizeMsg = !string.IsNullOrEmpty(size) ? $" (розмір {size})" : "";
                TempData["SuccessMessage"] = $"Успіх! Ми надішлемо вам листа, коли товар{sizeMsg} з'явиться.";
            }
            else
            {
                string sizeMsgInfo = !string.IsNullOrEmpty(size) ? " даного розміру" : "";
                TempData["InfoMessage"] = $"Ви вже підписані на сповіщення про цей товар{sizeMsgInfo}.";
            }

            return RedirectToAction("Details", new { id = productId });
        }
    }
}