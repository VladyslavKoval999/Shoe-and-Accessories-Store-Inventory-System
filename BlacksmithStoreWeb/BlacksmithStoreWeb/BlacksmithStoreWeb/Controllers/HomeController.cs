using Microsoft.AspNetCore.Mvc;
using BlacksmithStoreWeb.Models;
using BlacksmithStoreWeb.Data;
using System.Linq;
using System.Collections.Generic;
using System;
using Microsoft.AspNetCore.Http;
using System.Text.Json;

namespace BlacksmithStoreWeb.Controllers
{
    public class HomeController : Controller
    {
        private readonly StoreContext _context;

        public HomeController(StoreContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            var vm = new HomeViewModel();
            vm.NewArrivals = _context.Products.OrderByDescending(p => p.ProductId).Take(5).ToList();
            vm.MensShoes = _context.Products.Where(p => p.CategoryId == 1 && p.ProductType == "Взуття").Take(5).ToList();
            vm.WomensShoes = _context.Products.Where(p => p.CategoryId == 2 && p.ProductType == "Взуття").Take(5).ToList();
            vm.Accessories = _context.Products.Where(p => p.ProductType == "Аксесуар").Take(5).ToList();

            return View(vm);
        }

        public IActionResult NewArrivals()
        {
            var newProducts = _context.Products
                .OrderByDescending(p => p.ProductId)
                .Take(12)
                .ToList();

            return View(newProducts);
        }

        [HttpGet]
        public IActionResult Favorites()
        {
            var favJson = HttpContext.Session.GetString("Favorites");
            var favIds = string.IsNullOrEmpty(favJson)
                ? new List<int>()
                : JsonSerializer.Deserialize<List<int>>(favJson) ?? new List<int>();

            var favoriteProducts = _context.Products
                .Where(p => favIds.Contains(p.ProductId))
                .ToList();

            return View(favoriteProducts);
        }

        [HttpPost]
        public IActionResult ToggleFavorite(int id)
        {
            var favJson = HttpContext.Session.GetString("Favorites");
            var favIds = string.IsNullOrEmpty(favJson)
                ? new List<int>()
                : JsonSerializer.Deserialize<List<int>>(favJson) ?? new List<int>();

            bool isAdded;
            if (favIds.Contains(id))
            {
                favIds.Remove(id);
                isAdded = false;
            }
            else
            {
                favIds.Add(id);
                isAdded = true;
            }

            HttpContext.Session.SetString("Favorites", JsonSerializer.Serialize(favIds));
            return Json(new { success = true, isAdded = isAdded });
        }

        public IActionResult Details(int id)
        {
            var product = _context.Products.FirstOrDefault(p => p.ProductId == id);
            if (product == null) return NotFound();

            var sizes = (from st in _context.Stocks
                         join s in _context.Sizes on st.SizeId equals s.SizeId
                         where st.ProductId == id && st.Quantity > 0
                         select s.Value).Distinct().OrderBy(v => v).ToList();

            var vm = new ProductDetailsViewModel
            {
                Product = product,
                AvailableSizes = sizes
            };

            return View(vm);
        }

        [HttpGet]
        public IActionResult Catalog(
            int? categoryId,
            List<int>? brands,
            decimal? minPrice,
            decimal? maxPrice,
            string? search,
            int? selectedSubtypeId,
            string? selectedSeason,
            int? selectedSizeId,
            int? selectedColorId,
            bool inStockOnly,
            string? productType)
        {
            var query = _context.Products.AsQueryable();

            if (!string.IsNullOrEmpty(productType)) query = query.Where(p => p.ProductType == productType);
            if (categoryId.HasValue) query = query.Where(p => p.CategoryId == categoryId.Value);

            if (!string.IsNullOrWhiteSpace(search))
            {
                string sLower = search.Trim().ToLower();
                var matchingBrandIds = _context.Brands
                    .Where(b => b.Name.ToLower().Contains(sLower))
                    .Select(b => b.BrandId)
                    .ToList();

                query = query.Where(p =>
                    (p.Name != null && p.Name.ToLower().Contains(sLower)) ||
                    (p.Article != null && p.Article.ToLower().Contains(sLower)) ||
                    (p.BrandId.HasValue && matchingBrandIds.Contains(p.BrandId.Value))
                );
            }

            if (brands != null && brands.Any()) query = query.Where(p => p.BrandId.HasValue && brands.Contains(p.BrandId.Value));
            if (minPrice.HasValue) query = query.Where(p => p.BasePrice >= minPrice.Value);
            if (maxPrice.HasValue) query = query.Where(p => p.BasePrice <= maxPrice.Value);
            if (!string.IsNullOrEmpty(selectedSeason)) query = query.Where(p => p.Season == selectedSeason);
            if (selectedSubtypeId.HasValue) query = query.Where(p => p.SubtypeId == selectedSubtypeId.Value);

            if (inStockOnly || selectedSizeId.HasValue || selectedColorId.HasValue)
            {
                var stockQuery = _context.Stocks.AsQueryable();

                if (inStockOnly) stockQuery = stockQuery.Where(s => s.Quantity > 0);
                if (selectedSizeId.HasValue) stockQuery = stockQuery.Where(s => s.SizeId == selectedSizeId.Value);
                if (selectedColorId.HasValue) stockQuery = stockQuery.Where(s => s.ColorId == selectedColorId.Value);

                var validProductIds = stockQuery.Select(s => s.ProductId).Distinct();
                query = query.Where(p => validProductIds.Contains(p.ProductId));
            }

            var products = query.ToList();

            var vm = new CatalogViewModel
            {
                CategoryId = categoryId,
                SelectedSubtypeId = selectedSubtypeId,
                SelectedSeason = selectedSeason,
                SelectedSizeId = selectedSizeId,
                SelectedColorId = selectedColorId,
                SelectedBrands = brands ?? new List<int>(),
                MinPrice = minPrice,
                MaxPrice = maxPrice,
                SearchQuery = search,
                InStockOnly = inStockOnly,
                ProductType = productType,
                Products = products
            };

            vm.Categories = _context.Categories.OrderBy(c => c.CategoryId).ToList();

            vm.Brands = _context.Brands.OrderBy(b => b.Name).ToList();

            var subQuery = _context.ProductSubtypes.AsQueryable();
            if (!string.IsNullOrEmpty(productType))
            {
                var validSubtypeIds = _context.Products
                    .Where(p => p.ProductType == productType && p.SubtypeId != null)
                    .Select(p => p.SubtypeId.Value)
                    .Distinct()
                    .ToList();
                subQuery = subQuery.Where(s => validSubtypeIds.Contains(s.SubtypeId));
            }
            vm.Subtypes = subQuery.OrderBy(s => s.Name).ToList();

            vm.Seasons = _context.Products
                .Where(p => !string.IsNullOrEmpty(p.Season))
                .Select(p => p.Season!)
                .Distinct()
                .OrderBy(s => s)
                .ToList();

            vm.Colors = _context.Colors.OrderBy(c => c.ColorId).ToList();

            vm.Sizes = _context.Sizes.ToList()
                .OrderBy(s => { double d; return double.TryParse(s.Value, out d) ? d : 9999; })
                .ToList();

            bool showSizeFilter = true;
            if (productType == "Аксесуар") showSizeFilter = false;
            else if (string.IsNullOrEmpty(productType) && selectedSubtypeId.HasValue)
            {
                bool hasShoes = _context.Products.Any(p => p.SubtypeId == selectedSubtypeId.Value && p.ProductType == "Взуття");
                if (!hasShoes) showSizeFilter = false;
            }

            vm.ShowSizeFilter = showSizeFilter;
            if (!showSizeFilter) vm.SelectedSizeId = null;

            return View(vm);
        }

        [HttpGet]
        public IActionResult LiveSearch(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return Json(new List<object>());

            string sLower = query.Trim().ToLower();

            var matchingBrandIds = _context.Brands
                .Where(b => b.Name.ToLower().Contains(sLower))
                .Select(b => b.BrandId)
                .ToList();

            var productsQuery = _context.Products
                .Where(p =>
                    (p.Name != null && p.Name.ToLower().Contains(sLower)) ||
                    (p.Article != null && p.Article.ToLower().Contains(sLower)) ||
                    (p.BrandId.HasValue && matchingBrandIds.Contains(p.BrandId.Value))
                )
                .Take(15)
                .ToList();

            var results = productsQuery.Select(p => new {
                id = p.ProductId,
                name = p.Name,
                price = Math.Round(p.BasePrice),
                image = string.IsNullOrEmpty(p.Images)
                    ? "/Images/placeholder.jpg"
                    : "/Images/Product/" + System.IO.Path.GetFileName(p.Images.Split(',')[0].Trim())
            }).ToList();

            return Json(results);
        }
    }
}