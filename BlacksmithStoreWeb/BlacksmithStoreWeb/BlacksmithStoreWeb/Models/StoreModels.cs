using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BlacksmithStoreWeb.Models
{
    public class Role
    {
        [Key]
        public int RoleId { get; set; }
        public string RoleName { get; set; } = string.Empty;
    }

    public class User
    {
        [Key]
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public int RoleId { get; set; }
    }

    public class Category
    {
        [Key]
        public int CategoryId { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class Brand
    {
        [Key]
        public int BrandId { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class Color
    {
        [Key]
        public int ColorId { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class Size
    {
        [Key]
        public int SizeId { get; set; }
        public string Value { get; set; } = string.Empty;
    }

    public class ProductSubtype
    {
        [Key]
        public int SubtypeId { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class Product
    {
        [Key]
        public int ProductId { get; set; }
        public string Article { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal BasePrice { get; set; }
        public string? ProductType { get; set; }
        public string? Season { get; set; }
        public int CategoryId { get; set; }
        public int? BrandId { get; set; }
        public int? SubtypeId { get; set; }
        public string? Images { get; set; }
    }

    public class Stock
    {
        [Key]
        public int StockId { get; set; }
        public int ProductId { get; set; }
        public int? ColorId { get; set; }
        public int? SizeId { get; set; }
        public int Quantity { get; set; }
        public string AvailabilityStatus { get; set; } = string.Empty;
    }

    [Table("Orders")]
    public class Order
    {
        [Key]
        [Column("order_id")]
        public int OrderId { get; set; }

        [Column("user_id")]
        public int UserId { get; set; }

        [Column("order_date")]
        public DateTime OrderDate { get; set; }

        [Column("total_amount")]
        public decimal TotalAmount { get; set; }

        [Column("payment_method")]
        public string? PaymentMethod { get; set; }

        [Column("status")]
        public string? Status { get; set; }

        [Column("order_source")]
        public string? OrderSource { get; set; }

        [Column("delivery_address")]
        public string? DeliveryAddress { get; set; }

        [Column("comment")]
        public string? Comment { get; set; }
    }

    public class OrderItem
    {
        [Key]
        public int OrderItemId { get; set; }
        public int OrderId { get; set; }
        public int StockId { get; set; }
        public int Quantity { get; set; }
        public decimal PriceAtTimeOfSale { get; set; }
    }

    public class Customer
    {
        [Key]
        public int CustomerId { get; set; }
        public int? UserId { get; set; }
        public string? LoyaltyCardNumber { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Email { get; set; }
        public int PurchasedItemsCount { get; set; }
        public int AvailableBonuses { get; set; }
    }
}