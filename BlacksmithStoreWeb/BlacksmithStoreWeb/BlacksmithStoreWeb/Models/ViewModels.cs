using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace BlacksmithStoreWeb.Models
{
    public class AddToCartRequest
    {
        public int Id { get; set; }
        public string? Size { get; set; }
        public int Quantity { get; set; } = 1;
    }

    public class CartItem
    {
        public int ProductId { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string? Image { get; set; }
        public int Quantity { get; set; }
        public string? Size { get; set; }
        public decimal TotalPrice => Price * Quantity;
    }

    public class HomeViewModel
    {
        public List<Product> NewArrivals { get; set; } = new List<Product>();
        public List<Product> MensShoes { get; set; } = new List<Product>();
        public List<Product> WomensShoes { get; set; } = new List<Product>();
        public List<Product> Accessories { get; set; } = new List<Product>();
    }

    public class ProductDetailsViewModel
    {
        public Product Product { get; set; } = new Product();
        public List<string> AvailableSizes { get; set; } = new List<string>();
    }

    public class CatalogViewModel
    {
        public List<Product> Products { get; set; } = new List<Product>();
        public List<Brand> Brands { get; set; } = new List<Brand>();
        public List<Category> Categories { get; set; } = new List<Category>();
        public List<ProductSubtype> Subtypes { get; set; } = new List<ProductSubtype>();
        public List<Size> Sizes { get; set; } = new List<Size>();
        public List<Color> Colors { get; set; } = new List<Color>();
        public List<string> Seasons { get; set; } = new List<string>();

        public int? CategoryId { get; set; }
        public int? SelectedSubtypeId { get; set; }
        public int? SelectedSizeId { get; set; }
        public int? SelectedColorId { get; set; }

        public string? SelectedSeason { get; set; }
        public List<int> SelectedBrands { get; set; } = new List<int>();
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public string? SearchQuery { get; set; }
        public bool InStockOnly { get; set; }

        public bool ShowSizeFilter { get; set; } = true;
        public string? ProductType { get; set; }
    }

    public class CheckoutViewModel
    {
        [Required(ErrorMessage = "Будь ласка, вкажіть вулицю та будинок")]
        public string StreetAddress { get; set; } = string.Empty;
        public string? Apartment { get; set; }
        public string? CourierComment { get; set; }

        public bool UseBonus { get; set; }
        public int AvailableBonuses { get; set; }
        public decimal CartTotal { get; set; }
        public decimal DeliveryCost { get; set; } = 50m;
    }

    public class LoginViewModel
    {
        [Required(ErrorMessage = "Введіть логін")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Введіть пароль")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;
    }

    public class RegisterViewModel
    {
        [Required(ErrorMessage = "Вкажіть логін")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Вкажіть ім'я")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Вкажіть прізвище")]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Вкажіть номер телефону")]
        [Phone]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Вкажіть електронну пошту")]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Вкажіть пароль")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Повторіть пароль")]
        [DataType(DataType.Password)]
        [Compare("Password", ErrorMessage = "Паролі не співпадають")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public class OrderHistoryItemDto
    {
        public string ProductName { get; set; } = string.Empty;
        public string? ProductImage { get; set; }
        public string? Size { get; set; }
        public string? Color { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
    }

    public class OrderHistoryViewModel
    {
        public int OrderId { get; set; }
        public DateTime OrderDate { get; set; }
        public decimal TotalAmount { get; set; }
        public string? PaymentMethod { get; set; }
        public string? Status { get; set; }
        public string? DeliveryAddress { get; set; }
        public string? Comment { get; set; }
        public int TotalItemsCount { get; set; }
        public List<OrderHistoryItemDto> Items { get; set; } = new List<OrderHistoryItemDto>();
    }

    public class ProfileViewModel
    {
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public DateTime CardCreationDate { get; set; }

        public string Role { get; set; } = string.Empty;
        public string LoyaltyCardNumber { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string? Email { get; set; }
        public int PurchasedItemsCount { get; set; }
        public int AvailableBonuses { get; set; }

        public List<OrderHistoryViewModel> RecentOrders { get; set; } = new List<OrderHistoryViewModel>();

        public ChangePasswordViewModel ChangePassword { get; set; } = new ChangePasswordViewModel();

        public int ProgressPercentage => (PurchasedItemsCount % 10) * 10;
        public int ItemsToNextBonus => 10 - (PurchasedItemsCount % 10);
    }

    public class ChangePasswordViewModel
    {
        [Required(ErrorMessage = "Введіть поточний пароль")]
        [DataType(DataType.Password)]
        public string OldPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Введіть новий пароль")]
        [DataType(DataType.Password)]
        public string NewPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Повторіть новий пароль")]
        [DataType(DataType.Password)]
        [Compare("NewPassword", ErrorMessage = "Паролі не співпадають")]
        public string ConfirmNewPassword { get; set; } = string.Empty;
    }

    public class ErrorViewModel
    {
        public string? RequestId { get; set; }
        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
    }
}