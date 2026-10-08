using Microsoft.EntityFrameworkCore;
using BlacksmithStoreWeb.Models;

namespace BlacksmithStoreWeb.Data
{
    public class StoreContext : DbContext
    {
        public StoreContext() { }
        public StoreContext(DbContextOptions<StoreContext> options) : base(options) { }

        public DbSet<Role> Roles { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Brand> Brands { get; set; }
        public DbSet<Color> Colors { get; set; }
        public DbSet<Size> Sizes { get; set; }
        public DbSet<ProductSubtype> ProductSubtypes { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<Stock> Stocks { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<RestockRequest> RestockRequests { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseSqlite(@"Data Source=D:\Все для навчання\4_Курс\BlacksmithStore\BlacksmithStore\bin\x64\Debug\Blacksmith_StoreBD");
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Role>().ToTable("Roles");
            modelBuilder.Entity<Role>().HasKey(r => r.RoleId);
            modelBuilder.Entity<Role>().Property(r => r.RoleId).HasColumnName("role_id");
            modelBuilder.Entity<Role>().Property(r => r.RoleName).HasColumnName("role_name");

            modelBuilder.Entity<User>().ToTable("Users");
            modelBuilder.Entity<User>().HasKey(u => u.UserId);
            modelBuilder.Entity<User>().Property(u => u.UserId).HasColumnName("user_id");
            modelBuilder.Entity<User>().Property(u => u.Username).HasColumnName("username");
            modelBuilder.Entity<User>().Property(u => u.FullName).HasColumnName("full_name");
            modelBuilder.Entity<User>().Property(u => u.PasswordHash).HasColumnName("password_hash");
            modelBuilder.Entity<User>().Property(u => u.RoleId).HasColumnName("role_id");

            modelBuilder.Entity<Category>().ToTable("Categories");
            modelBuilder.Entity<Category>().HasKey(c => c.CategoryId);
            modelBuilder.Entity<Category>().Property(c => c.CategoryId).HasColumnName("category_id");
            modelBuilder.Entity<Category>().Property(c => c.Name).HasColumnName("name");

            modelBuilder.Entity<Brand>().ToTable("Brands");
            modelBuilder.Entity<Brand>().HasKey(b => b.BrandId);
            modelBuilder.Entity<Brand>().Property(b => b.BrandId).HasColumnName("brand_id");
            modelBuilder.Entity<Brand>().Property(b => b.Name).HasColumnName("name");

            modelBuilder.Entity<Color>().ToTable("Colors");
            modelBuilder.Entity<Color>().HasKey(c => c.ColorId);
            modelBuilder.Entity<Color>().Property(c => c.ColorId).HasColumnName("color_id");
            modelBuilder.Entity<Color>().Property(c => c.Name).HasColumnName("name");

            modelBuilder.Entity<Size>().ToTable("Sizes");
            modelBuilder.Entity<Size>().HasKey(s => s.SizeId);
            modelBuilder.Entity<Size>().Property(s => s.SizeId).HasColumnName("size_id");
            modelBuilder.Entity<Size>().Property(s => s.Value).HasColumnName("value");

            modelBuilder.Entity<ProductSubtype>().ToTable("Product_Subtypes");
            modelBuilder.Entity<ProductSubtype>().HasKey(ps => ps.SubtypeId);
            modelBuilder.Entity<ProductSubtype>().Property(ps => ps.SubtypeId).HasColumnName("subtype_id");
            modelBuilder.Entity<ProductSubtype>().Property(ps => ps.Name).HasColumnName("name");

            modelBuilder.Entity<Product>().ToTable("Products");
            modelBuilder.Entity<Product>().HasKey(p => p.ProductId);
            modelBuilder.Entity<Product>().Property(p => p.ProductId).HasColumnName("product_id");
            modelBuilder.Entity<Product>().Property(p => p.Article).HasColumnName("article");
            modelBuilder.Entity<Product>().Property(p => p.Name).HasColumnName("name");
            modelBuilder.Entity<Product>().Property(p => p.Description).HasColumnName("description");
            modelBuilder.Entity<Product>().Property(p => p.BasePrice).HasColumnName("base_price");
            modelBuilder.Entity<Product>().Property(p => p.ProductType).HasColumnName("product_type");
            modelBuilder.Entity<Product>().Property(p => p.Season).HasColumnName("season");
            modelBuilder.Entity<Product>().Property(p => p.CategoryId).HasColumnName("category_id");
            modelBuilder.Entity<Product>().Property(p => p.BrandId).HasColumnName("brand_id");
            modelBuilder.Entity<Product>().Property(p => p.SubtypeId).HasColumnName("subtype_id");
            modelBuilder.Entity<Product>().Property(p => p.Images).HasColumnName("images");

            modelBuilder.Entity<Stock>().ToTable("Stock");
            modelBuilder.Entity<Stock>().HasKey(s => s.StockId);
            modelBuilder.Entity<Stock>().Property(s => s.StockId).HasColumnName("stock_id");
            modelBuilder.Entity<Stock>().Property(s => s.ProductId).HasColumnName("product_id");
            modelBuilder.Entity<Stock>().Property(s => s.ColorId).HasColumnName("color_id");
            modelBuilder.Entity<Stock>().Property(s => s.SizeId).HasColumnName("size_id");
            modelBuilder.Entity<Stock>().Property(s => s.Quantity).HasColumnName("quantity");
            modelBuilder.Entity<Stock>().Property(s => s.AvailabilityStatus).HasColumnName("availability_status");

            modelBuilder.Entity<Order>().ToTable("Orders");
            modelBuilder.Entity<Order>().HasKey(o => o.OrderId);
            modelBuilder.Entity<Order>().Property(o => o.OrderId).HasColumnName("order_id");
            modelBuilder.Entity<Order>().Property(o => o.OrderDate).HasColumnName("order_date");
            modelBuilder.Entity<Order>().Property(o => o.UserId).HasColumnName("user_id");
            modelBuilder.Entity<Order>().Property(o => o.TotalAmount).HasColumnName("total_amount");
            modelBuilder.Entity<Order>().Property(o => o.PaymentMethod).HasColumnName("payment_method");
            modelBuilder.Entity<Order>().Property(o => o.Status).HasColumnName("status");
            modelBuilder.Entity<Order>().Property(o => o.OrderSource).HasColumnName("order_source");
            modelBuilder.Entity<Order>().Property(o => o.DeliveryAddress).HasColumnName("delivery_address");
            modelBuilder.Entity<Order>().Property(o => o.Comment).HasColumnName("comment");

            modelBuilder.Entity<OrderItem>().ToTable("Order_Items");
            modelBuilder.Entity<OrderItem>().HasKey(oi => oi.OrderItemId);
            modelBuilder.Entity<OrderItem>().Property(oi => oi.OrderItemId).HasColumnName("order_item_id");
            modelBuilder.Entity<OrderItem>().Property(oi => oi.OrderId).HasColumnName("order_id");
            modelBuilder.Entity<OrderItem>().Property(oi => oi.StockId).HasColumnName("stock_id");
            modelBuilder.Entity<OrderItem>().Property(oi => oi.Quantity).HasColumnName("quantity");
            modelBuilder.Entity<OrderItem>().Property(oi => oi.PriceAtTimeOfSale).HasColumnName("price_at_time_of_sale");

            modelBuilder.Entity<Customer>().ToTable("Customers");
            modelBuilder.Entity<Customer>().HasKey(c => c.CustomerId);
            modelBuilder.Entity<Customer>().Property(c => c.CustomerId).HasColumnName("customer_id");
            modelBuilder.Entity<Customer>().Property(c => c.UserId).HasColumnName("user_id");
            modelBuilder.Entity<Customer>().Property(c => c.LoyaltyCardNumber).HasColumnName("loyalty_card_number");
            modelBuilder.Entity<Customer>().Property(c => c.PhoneNumber).HasColumnName("phone_number");
            modelBuilder.Entity<Customer>().Property(c => c.Email).HasColumnName("email");
            modelBuilder.Entity<Customer>().Property(c => c.PurchasedItemsCount).HasColumnName("purchased_items_count");
            modelBuilder.Entity<Customer>().Property(c => c.AvailableBonuses).HasColumnName("available_bonuses");

            modelBuilder.Entity<RestockRequest>().ToTable("Restock_Requests");
            modelBuilder.Entity<RestockRequest>().HasKey(r => r.RequestId);
            modelBuilder.Entity<RestockRequest>().Property(r => r.RequestId).HasColumnName("request_id");
            modelBuilder.Entity<RestockRequest>().Property(r => r.UserId).HasColumnName("user_id");
            modelBuilder.Entity<RestockRequest>().Property(r => r.ProductId).HasColumnName("product_id");
            modelBuilder.Entity<RestockRequest>().Property(r => r.Size).HasColumnName("size");
            modelBuilder.Entity<RestockRequest>().Property(r => r.RequestDate).HasColumnName("request_date");
            modelBuilder.Entity<RestockRequest>().Property(r => r.IsFulfilled).HasColumnName("is_fulfilled");
        }
    }
}