using Microsoft.AspNetCore.Mvc;
using BlacksmithStoreWeb.Data;
using BlacksmithStoreWeb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using System.Text.Json;
using System.Linq;
using System;
using System.Collections.Generic;

namespace BlacksmithStoreWeb.Controllers
{
    [Authorize]
    public class OrderController : Controller
    {
        private readonly StoreContext _context;

        public OrderController(StoreContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Checkout()
        {
            var cartJson = HttpContext.Session.GetString("Cart");
            if (string.IsNullOrEmpty(cartJson)) return RedirectToAction("Catalog", "Home");

            var cart = JsonSerializer.Deserialize<List<CartItem>>(cartJson);
            decimal cartTotal = cart?.Sum(i => i.Price * i.Quantity) ?? 0;

            var username = User.Identity?.Name;
            if (string.IsNullOrEmpty(username)) return RedirectToAction("Login", "Account");

            var user = _context.Users.FirstOrDefault(u => u.Username == username);
            if (user == null) return RedirectToAction("Login", "Account");

            var customer = _context.Customers.FirstOrDefault(c => c.UserId == user.UserId);

            var model = new CheckoutViewModel
            {
                AvailableBonuses = customer?.AvailableBonuses ?? 0,
                CartTotal = cartTotal
            };

            return View(model);
        }

        [HttpPost]
        public IActionResult Checkout(CheckoutViewModel model)
        {
            var cartJson = HttpContext.Session.GetString("Cart");
            if (string.IsNullOrEmpty(cartJson)) return RedirectToAction("Catalog", "Home");

            var cart = JsonSerializer.Deserialize<List<CartItem>>(cartJson);
            var username = User.Identity?.Name;

            if (string.IsNullOrEmpty(username)) return RedirectToAction("Login", "Account");

            var user = _context.Users.FirstOrDefault(u => u.Username == username);
            if (user == null) return RedirectToAction("Login", "Account");

            var customer = _context.Customers.FirstOrDefault(c => c.UserId == user.UserId);

            decimal cartTotal = cart?.Sum(i => i.Price * i.Quantity) ?? 0;
            model.CartTotal = cartTotal;

            if (ModelState.IsValid && cart != null && cart.Any())
            {
                decimal discount = 0;
                string paymentMethodInfo = "Готівка/Картка при отриманні";

                if (model.UseBonus && customer != null && customer.AvailableBonuses > 0)
                {
                    discount = cartTotal * 0.15m;
                    customer.AvailableBonuses -= 1;
                    paymentMethodInfo += " (Використано бонус)";
                }

                decimal finalTotal = cartTotal - discount + model.DeliveryCost;

                string fullAddress = $"м. Харків, {model.StreetAddress}";
                if (!string.IsNullOrWhiteSpace(model.Apartment)) fullAddress += $", кв. {model.Apartment}";

                var order = new Order
                {
                    UserId = user.UserId,
                    OrderDate = DateTime.Now,
                    TotalAmount = finalTotal,
                    PaymentMethod = paymentMethodInfo,
                    Status = "Очікує збору",
                    OrderSource = "Веб-сайт",
                    DeliveryAddress = fullAddress,
                    Comment = model.CourierComment
                };

                _context.Orders.Add(order);
                _context.SaveChanges();

                foreach (var item in cart)
                {
                    var sizeRecord = _context.Sizes.FirstOrDefault(s => s.Value == item.Size);

                    var stock = _context.Stocks.FirstOrDefault(s => s.ProductId == item.ProductId &&
                        (sizeRecord == null || s.SizeId == sizeRecord.SizeId));

                    if (stock == null)
                    {
                        stock = _context.Stocks.FirstOrDefault(s => s.ProductId == item.ProductId);
                    }

                    int currentStockId = stock?.StockId ?? 1;

                    var orderItem = new OrderItem
                    {
                        OrderId = order.OrderId,
                        StockId = currentStockId,
                        Quantity = item.Quantity,
                        PriceAtTimeOfSale = item.Price
                    };

                    _context.OrderItems.Add(orderItem);

                    if (stock != null)
                    {
                        stock.Quantity = Math.Max(0, stock.Quantity - item.Quantity);
                    }
                }

                _context.SaveChanges();
                HttpContext.Session.Remove("Cart");

                return RedirectToAction("Success", new { orderId = order.OrderId });
            }

            model.AvailableBonuses = customer?.AvailableBonuses ?? 0;
            return View(model);
        }

        public IActionResult Success(int orderId)
        {
            ViewBag.OrderId = orderId;
            return View();
        }
    }
}