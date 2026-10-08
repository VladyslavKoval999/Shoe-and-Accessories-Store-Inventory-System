using Microsoft.AspNetCore.Mvc;
using BlacksmithStoreWeb.Data;
using BlacksmithStoreWeb.Models;
using BlacksmithStoreWeb.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BlacksmithStoreWeb.Models
{
    public class UpdateCartQuantityRequest
    {
        public int Id { get; set; }
        public string? Size { get; set; }
        public int Delta { get; set; }
    }
}

namespace BlacksmithStoreWeb.Controllers
{
    public class CartController : Controller
    {
        private readonly StoreContext _context;

        public CartController(StoreContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            var cart = HttpContext.Session.GetObjectFromJson<List<CartItem>>("Cart") ?? new List<CartItem>();
            return View(cart);
        }

        [HttpGet]
        public IActionResult GetCartCount()
        {
            var cart = HttpContext.Session.GetObjectFromJson<List<CartItem>>("Cart") ?? new List<CartItem>();
            return Json(new { count = cart.Sum(c => c.Quantity) });
        }

        [HttpGet]
        public IActionResult GetAvailableStock(int id, string? size)
        {
            size = string.IsNullOrEmpty(size) ? null : size;

            var sizeRecord = _context.Sizes.FirstOrDefault(s => s.Value == size);
            var stockList = _context.Stocks.Where(s => s.ProductId == id).ToList();

            Stock? stockRecord = sizeRecord != null
                ? stockList.FirstOrDefault(s => s.SizeId == sizeRecord.SizeId)
                : stockList.FirstOrDefault();

            return Json(new { stock = stockRecord?.Quantity ?? 0 });
        }

        [HttpGet]
        public IActionResult GetItemCartCount(int id, string? size)
        {
            size = string.IsNullOrEmpty(size) ? null : size;

            var cart = HttpContext.Session.GetObjectFromJson<List<CartItem>>("Cart") ?? new List<CartItem>();
            var item = cart.FirstOrDefault(c => c.ProductId == id && c.Size == size);
            return Json(new { count = item?.Quantity ?? 0 });
        }

        [HttpPost]
        public IActionResult UpdateQuantity([FromBody] UpdateCartQuantityRequest request)
        {
            request.Size = string.IsNullOrEmpty(request.Size) ? null : request.Size;

            var cart = HttpContext.Session.GetObjectFromJson<List<CartItem>>("Cart") ?? new List<CartItem>();
            var item = cart.FirstOrDefault(c => c.ProductId == request.Id && c.Size == request.Size);

            if (item != null)
            {
                if (request.Delta > 0)
                {
                    int currentTotalItems = cart.Sum(x => x.Quantity);
                    if (currentTotalItems + request.Delta > 10)
                    {
                        return Json(new { success = false, message = $"Кур'єр доставляє максимум 10 товарів. У кошику вже: {currentTotalItems} шт." });
                    }

                    var sizeRecord = _context.Sizes.FirstOrDefault(s => s.Value == request.Size);
                    var stockList = _context.Stocks.Where(s => s.ProductId == request.Id).ToList();
                    Stock? stockRecord = sizeRecord != null
                        ? stockList.FirstOrDefault(s => s.SizeId == sizeRecord.SizeId)
                        : stockList.FirstOrDefault();

                    int availableStock = stockRecord?.Quantity ?? 0;

                    if (item.Quantity + request.Delta > availableStock)
                    {
                        item.Quantity = availableStock;
                        HttpContext.Session.SetObjectAsJson("Cart", cart);
                        return Json(new { success = false, message = $"На складі залишилось лише {availableStock} шт." });
                    }
                }

                item.Quantity += request.Delta;

                if (item.Quantity <= 0)
                {
                    cart.Remove(item);
                }

                HttpContext.Session.SetObjectAsJson("Cart", cart);
            }

            return Json(new { success = true, cartCount = cart.Sum(c => c.Quantity) });
        }

        [HttpPost]
        public IActionResult AddToCart([FromBody] AddToCartRequest request)
        {
            request.Size = string.IsNullOrEmpty(request.Size) ? null : request.Size;

            var product = _context.Products.FirstOrDefault(p => p.ProductId == request.Id);
            if (product != null)
            {
                var cart = HttpContext.Session.GetObjectFromJson<List<CartItem>>("Cart") ?? new List<CartItem>();
                var existingItem = cart.FirstOrDefault(c => c.ProductId == request.Id && c.Size == request.Size);

                int requestedQty = request.Quantity > 0 ? request.Quantity : 1;
                int currentItemCartQty = existingItem?.Quantity ?? 0;

                int currentTotalItems = cart.Sum(c => c.Quantity);
                if (currentTotalItems + requestedQty > 10)
                {
                    return Json(new { success = false, message = $"Кур'єр доставляє максимум 10 товарів. У кошику вже: {currentTotalItems} шт." });
                }

                var sizeRecord = _context.Sizes.FirstOrDefault(s => s.Value == request.Size);
                var stockList = _context.Stocks.Where(s => s.ProductId == request.Id).ToList();

                Stock? stockRecord = sizeRecord != null
                    ? stockList.FirstOrDefault(s => s.SizeId == sizeRecord.SizeId)
                    : stockList.FirstOrDefault();

                int availableStock = stockRecord?.Quantity ?? 0;

                if (currentItemCartQty + requestedQty > availableStock)
                {
                    if (existingItem != null && availableStock > 0)
                    {
                        existingItem.Quantity = availableStock;
                        HttpContext.Session.SetObjectAsJson("Cart", cart);
                    }
                    else if (existingItem == null && availableStock > 0)
                    {
                        cart.Add(new CartItem
                        {
                            ProductId = product.ProductId,
                            Name = product.Name ?? "Товар",
                            Price = product.BasePrice,
                            Image = product.Images,
                            Quantity = availableStock,
                            Size = request.Size
                        });
                        HttpContext.Session.SetObjectAsJson("Cart", cart);
                    }

                    string sizeInfo = sizeRecord != null ? $" (Розмір: {request.Size})" : "";
                    string errorMsg = availableStock == 0
                        ? $"На жаль, товар{sizeInfo} закінчився."
                        : $"На складі залишилось лише {availableStock} шт.{sizeInfo}.";

                    return Json(new { success = false, message = errorMsg });
                }

                if (existingItem != null)
                {
                    existingItem.Quantity += requestedQty;
                }
                else
                {
                    cart.Add(new CartItem
                    {
                        ProductId = product.ProductId,
                        Name = product.Name ?? "Товар",
                        Price = product.BasePrice,
                        Image = product.Images,
                        Quantity = requestedQty,
                        Size = request.Size
                    });
                }

                HttpContext.Session.SetObjectAsJson("Cart", cart);
                return Json(new { success = true, productName = product.Name, cartCount = cart.Sum(c => c.Quantity) });
            }
            return Json(new { success = false, message = "Товар не знайдено." });
        }

        [HttpGet]
        public IActionResult GetCartPartial()
        {
            var cart = HttpContext.Session.GetObjectFromJson<List<CartItem>>("Cart") ?? new List<CartItem>();
            return PartialView("_CartOffcanvas", cart);
        }

        [HttpPost]
        public IActionResult RemoveFromCart(int id, string? size)
        {
            size = string.IsNullOrEmpty(size) ? null : size;

            var cart = HttpContext.Session.GetObjectFromJson<List<CartItem>>("Cart") ?? new List<CartItem>();
            var item = cart.FirstOrDefault(c => c.ProductId == id && c.Size == size);

            if (item != null)
            {
                cart.Remove(item);
                HttpContext.Session.SetObjectAsJson("Cart", cart);
            }

            return Json(new { success = true, cartCount = cart.Sum(c => c.Quantity) });
        }

        [HttpPost]
        public IActionResult ClearAjax()
        {
            HttpContext.Session.Remove("Cart");
            return Json(new { success = true });
        }

        public IActionResult Clear()
        {
            HttpContext.Session.Remove("Cart");
            return RedirectToAction("Index");
        }

        [HttpPost]
        public IActionResult Checkout(string? paymentMethod, string? deliveryAddress, string? comment)
        {
            var user = _context.Users.FirstOrDefault(u => u.Username == User.Identity!.Name);
            if (user == null) return RedirectToAction("Login", "Account");

            var cart = HttpContext.Session.GetObjectFromJson<List<CartItem>>("Cart") ?? new List<CartItem>();
            if (!cart.Any()) return RedirectToAction("Index");

            string fullPaymentInfo = string.IsNullOrEmpty(paymentMethod) ? "Готівка/Картка кур'єру" : paymentMethod;
            if (!string.IsNullOrEmpty(deliveryAddress)) fullPaymentInfo += $". Адреса доставки: {deliveryAddress}";
            if (!string.IsNullOrEmpty(comment)) fullPaymentInfo += $" | Ком: {comment}";

            var order = new Order
            {
                UserId = user.UserId,
                OrderDate = DateTime.Now,
                TotalAmount = cart.Sum(i => i.Price * i.Quantity),
                PaymentMethod = fullPaymentInfo,
                Status = "Очікує збору",
                OrderSource = "Сайт"
            };

            _context.Orders.Add(order);
            _context.SaveChanges();
            HttpContext.Session.Remove("Cart");

            TempData["SuccessMessage"] = "Замовлення успішно оформлено!";
            return RedirectToAction("Profile", "Account");
        }
    }
}