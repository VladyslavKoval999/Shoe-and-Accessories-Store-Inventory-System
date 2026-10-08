# 👞 Shoe-and-Accessories-Store-Inventory-System

<div align="left">
  <img src="https://img.shields.io/badge/C%23-239120?style=for-the-badge&logo=c-sharp&logoColor=white" alt="C#" />
  <img src="https://img.shields.io/badge/.NET_8.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white" alt=".NET 8" />
  <img src="https://img.shields.io/badge/ASP.NET_Core_MVC-00547E?style=for-the-badge&logo=dot-net&logoColor=white" alt="ASP.NET Core MVC" />
  <img src="https://img.shields.io/badge/WPF-3C8A3E?style=for-the-badge&logo=windows&logoColor=white" alt="WPF" />
  <img src="https://img.shields.io/badge/SQLite-07405E?style=for-the-badge&logo=sqlite&logoColor=white" alt="SQLite" />
  <img src="https://img.shields.io/badge/Google_Gemini_API-8E75B2?style=for-the-badge&logo=google&logoColor=white" alt="Gemini AI" />
</div>

<br>

A comprehensive, dual-platform retail management ecosystem designed to automate business processes for the **"Blacksmith Store"** shoe and accessories brand.

The system bridges the gap between physical retail and e-commerce by providing a fast **WPF desktop application** for store staff and a responsive **ASP.NET Core MVC web application** for online customers. Both applications work with a unified **SQLite** database to maintain synchronized product, inventory, customer, and order information.

The project combines **Point of Sale (POS)** operations, inventory management, online order processing, customer loyalty functionality, automated reporting, email notifications, and an **AI-powered virtual assistant** into a single retail management ecosystem.

---

## 🚀 Key Features

### 🖥️ For Staff — WPF Desktop Application

Designed for daily operation of the physical store, including POS and back-office management.

* **Barcode Scanner Integration:** Instant product identification and automated cart calculation for fast checkout operations.
* **Point of Sale (POS):** Create sales, manage shopping carts, calculate totals, and process customer purchases.
* **Product & Inventory Management:** Add and edit products, manage stock quantities, product characteristics, and assortment information.
* **Role-Based Access Control:** Different functional modules are available according to the employee's role.
* **Returns Management:** Process product returns according to the corresponding business rules.
* **Online Order Fulfillment:** View and process orders placed through the web application.
* **Low-Stock Alerts:** Visual identification of products requiring stock replenishment.
* **Financial Analytics:** View sales information, statistics, and financial data.
* **Automated MS Word Reporting:** Generate formatted `.docx` financial and sales reports using Microsoft Office Interop.
* **Employee Management:** Administrative functionality for managing staff accounts and access.

### 🌐 For Customers — ASP.NET Core MVC Web Application

The web application provides customers with access to the online store and their personal information.

* **Product Catalog:** Browse available shoes and accessories with detailed product information.
* **Advanced Search & Filtering:** Filter products by multiple parameters, including size, season, brand, and color.
* **Shopping Cart:** Add products, modify quantities, and calculate the total order value.
* **Online Order Placement:** Create and submit orders directly through the web application.
* **Personal Customer Cabinet:** Access customer information, orders, loyalty information, and related functionality.
* **Dark/Light Theme:** User interface supporting both dark and light visual themes.
* **AI-Powered Virtual Assistant:** Integrated **Google Gemini API** providing Ukrainian-language product consultations and personalized shoe recommendations.
* **Automated Email Notifications:** Notifications related to newly available or restocked products.
* **"Blacksmith Club" Loyalty Program:** Customer loyalty system with a virtual 3D loyalty card and purchase progress tracking.
* **Discount Rewards:** A **15% discount coupon** is generated after every 10 purchased items.

### 🚚 Try-at-Home Delivery

The system includes a special business rule for local courier fitting:

* Customers can order a maximum of **4 pairs of shoes** for try-at-home fitting.
* The functionality is designed to allow customers to compare sizes and select the most suitable pair before completing the purchase.

---

## 👥 User Roles

The system provides role-based access to functionality depending on the user's responsibilities.

| Role              | Main Responsibilities                                                            |
| ----------------- | -------------------------------------------------------------------------------- |
| **Administrator** | Staff management, system administration, financial analytics                     |
| **Manager**       | Inventory management, assortment management, online order processing, statistics |
| **Seller**        | POS operations, product sales, returns                                           |
| **Customer**      | Product browsing, shopping cart, online orders, loyalty program                  |

---

## 🏗️ Application Architecture

The project consists of two interconnected applications:

```text
┌─────────────────────────────────────────────────────────────┐
│                    BLACKSMITH STORE                         │
│              Retail Management Ecosystem                    │
└─────────────────────────────────────────────────────────────┘
                              │
                ┌─────────────┴─────────────┐
                │                           │
                ▼                           ▼
┌─────────────────────────┐   ┌──────────────────────────────┐
│   WPF Desktop App       │   │ ASP.NET Core MVC Web App     │
│                         │   │                              │
│ • POS                   │   │ • Online Store               │
│ • Inventory             │   │ • Product Catalog            │
│ • Returns               │   │ • Customer Cabinet           │
│ • Staff Management      │   │ • Online Orders              │
│ • Financial Reports     │   │ • Blacksmith Club            │
│ • Barcode Scanner       │   │ • Gemini AI Assistant        │
└────────────┬────────────┘   └──────────────┬───────────────┘
             │                               │
             └───────────────┬───────────────┘
                             ▼
                 ┌────────────────────────┐
                 │    SQLite Database     │
                 │                        │
                 │ Products               │
                 │ Stock                  │
                 │ Orders                 │
                 │ Customers              │
                 │ Users                  │
                 │ Categories             │
                 │ Brands                 │
                 │ Sizes                  │
                 │ Colors                 │
                 │ etc.                   │
                 └────────────────────────┘
```

The system enforces separation of responsibilities between the customer-facing web application and the internal staff application.

The web application follows the **ASP.NET Core MVC** architectural approach, while the WPF application uses a layered structure for separating interface, application logic, and data-related operations.

---

## 🧩 System Analysis & Business Processes

The system was designed based on an analysis of the main business processes of a shoe and accessories retail store.

The project covers processes such as:

* Product and assortment management.
* Receiving and adding new goods.
* Inventory control.
* Monitoring low-stock products.
* Physical store sales.
* Barcode-based product identification.
* Product returns.
* Online order processing.
* Customer registration and management.
* Courier delivery and try-at-home orders.
* Loyalty program management.
* Sales and financial reporting.

Business processes were analyzed and modeled using system analysis and BPMN-based process modeling.

<details>
<summary><b>📂 Click to view Database Schema & BPMN Models</b></summary>

### Relational Database Schema

![Database Schema](db-schema.png)

### Business Process Model — Online Order Flow

![BPMN Web](bpmn-web.png)

</details>

---

## 🗄️ Database

The system uses a relational **SQLite** database as a shared data source for the desktop and web applications.

The database contains **several normalized tables**, including entities such as:

* `Users`
* `Categories`
* `Brands`
* `Colors`
* `Sizes`
* `Product_Subtypes`
* `Products`
* `Stock`
* `Orders`
* `Order_Items`
* `Customers`

The database stores information about:

* Products and their characteristics.
* Product categories and brands.
* Sizes and colors.
* Stock quantities and availability.
* Customer information.
* Orders and order items.
* Employee and user accounts.
* Loyalty program information.

### 📦 Product & Stock Management

The product structure contains information such as:

* Article number.
* Product name.
* Description.
* Base price.
* Product type.
* Season.
* Category.
* Brand.
* Product subtype.
* Product images.

Stock information includes:

* Product.
* Size.
* Color.
* Quantity.
* Availability status.

This structure allows the system to maintain detailed inventory information for individual product variants.

---

## 🔄 Order Processing & Transaction Safety

The system supports both physical and online sales.

Orders contain information about:

* Order date.
* Total amount.
* Payment method.
* Customer/user.
* Employee responsible for processing.
* Order status.
* Order source.
* Delivery address.
* Additional comments.

To improve reliability, order processing includes **database transaction and rollback mechanisms**.

If an operation fails during checkout, the transaction can be rolled back to help prevent inconsistent inventory data.

---

## 📊 Inventory & Management

Managers have access to tools for monitoring and controlling store inventory.

The system supports:

* Receiving new goods.
* Adding quantities to existing products.
* Adding new assortment positions.
* Editing product information.
* Monitoring current stock.
* Identifying products below the minimum stock level.
* Generating shortage information.
* Analyzing sales and assortment statistics.
* Monitoring online orders.
* Changing order statuses.
* Managing customer loyalty information.

Low-stock monitoring allows employees to identify products requiring replenishment.

---

## 🤖 Artificial Intelligence

The web application integrates the **Google Gemini API** as a virtual assistant.

The AI assistant is designed for customer interaction and product consultation.

It can provide:

* Ukrainian-language responses.
* Shoe and product recommendations.
* Assistance with product selection.
* Natural-language interaction with customers.

The AI functionality extends the traditional product catalog by providing an interactive way for customers to receive product-related assistance.

---

## 🏆 Blacksmith Club — Loyalty Program

The system includes a dedicated loyalty program called **Blacksmith Club**.

The program provides customers with:

* A virtual 3D loyalty card.
* Purchase progress tracking.
* Information about collected purchases.
* Automatic reward progression.
* Discount coupons.

A **15% discount coupon** is generated after every **10 purchased items**, according to the loyalty program rules.

---

## 📧 Email Notifications

The system includes automated email functionality.

Email notifications are used for product availability and restocking scenarios.

This allows customers to receive information when previously unavailable products become available again.

The email functionality is integrated with the web application and configured through SMTP settings.

---

## 🧪 Testing & Reliability

The system was tested against several important business scenarios, including:

* Multi-parameter product filtering.
* Barcode-based product identification.
* Shopping cart quantity validation.
* Successful order creation.
* Inventory updates after purchases.
* Customer purchase information updates.
* Product and stock management operations.
* Transaction rollback when an operation fails.
* Online order processing.

Particular attention was paid to synchronization between the physical store and the online store because both applications operate with the same database.

---

## 📸 Application Showcase

### 🌐 Customer Web Experience & AI Assistant

![Web Catalog](web-catalog.png)

### 👤 Personal Customer Cabinet

![Client Cabinet](web-cabinet.png)

### 🛒 Seller POS System with Barcode Support

![WPF POS](wpf-pos.png)

### 📊 Administrator Financial Dashboard

![WPF Admin](wpf-admin.png)

---

## 🛠️ Technologies Used

| Technology                                      | Purpose                                     |
| ----------------------------------------------- | ------------------------------------------- |
| **C#**                                          | Main programming language                   |
| **.NET 8.0**                                    | Application platform                        |
| **WPF**                                         | Desktop application for store employees     |
| **ASP.NET Core MVC**                            | Customer-facing web application             |
| **SQLite**                                      | Relational database                         |
| **Entity Framework Core**                       | Database access for the web application     |
| **System.Data.SQLite**                          | Database access for the desktop application |
| **Google Gemini API**                           | AI-powered virtual assistant                |
| **Microsoft Office Interop Word**               | Automated `.docx` report generation         |
| **Microsoft.AspNetCore.Authentication.Cookies** | Web authentication                          |
| **System.Net.Http**                             | HTTP communication                          |
| **System.Text.Json**                            | JSON processing                             |
| **Visual Studio 2022**                          | Development environment                     |

---

## 📁 Project Structure

The solution contains two main applications:

```text
BlacksmithStore
│
├── BlacksmithStore
│   └── WPF Desktop Application
│       ├── POS
│       ├── Inventory Management
│       ├── Returns
│       ├── Staff Management
│       ├── Financial Reporting
│       └── Barcode Scanner Integration
│
├── BlacksmithStoreWeb
│   └── ASP.NET Core MVC Web Application
│       ├── Controllers
│       ├── Models
│       ├── Views
│       ├── Services
│       ├── Customer Cabinet
│       ├── Product Catalog
│       ├── Shopping Cart
│       ├── Online Orders
│       └── Gemini AI Assistant
│
└── BlacksmithStore.sln
```

---

## ⚙️ How to Run Locally

### Requirements

* **Windows OS** — required for the WPF application.
* **Visual Studio 2022**.
* **.NET 8.0 Desktop & Web development workloads**.
* Required NuGet packages.
* **SQLite**.

### Installation

1. Clone the repository:

```bash
git clone https://github.com/VladyslavKoval999/Shoe-and-Accessories-Store-Inventory-System.git
```

2. Open the solution:

```text
BlacksmithStore.sln
```

in **Visual Studio 2022**.

3. Restore the required NuGet packages.

4. To run the Web App:

   * Set `BlacksmithStoreWeb` as the startup project.
   * Run using IIS Express or Kestrel.

5. To run the POS App:

   * Set `BlacksmithStore` as the startup project.
   * Press `F5`.

---

## 🔐 Configuration & Security Notice

> 🔒 **Security Notice:** For security reasons and to prevent email/API abuse, the `Google Gemini API Key` and `SMTP Email Password` have been removed from the public repository. To test AI chat and email features, please insert your own API keys into the respective configuration files or service classes.

### Test Credentials — WPF Application

For demonstration and testing purposes:

* **Role:** Administrator
* **Login:** `VLADadmin`
* **Password:** `1-_aD!.min-)V1ad4,s`

> ⚠️ These credentials are intended only for local demonstration and testing purposes.

---

## 🎯 Project Goals

The main goal of the project is to develop a software system capable of supporting the main operational processes of a shoe and accessories retail business.

The system combines:

* Physical store management.
* Point of Sale operations.
* Inventory control.
* E-commerce functionality.
* Customer management.
* Loyalty program.
* Online order processing.
* Automated reporting.
* AI-powered customer assistance.

The combination of desktop and web applications allows the system to cover both **internal store operations** and **customer-facing online services** within one integrated ecosystem.

---

## 👨‍💻 Author

**Vladyslav Koval** — *Junior Software Developer*

[LinkedIn Profile](https://linkedin.com/in/vladyslav-koval2007)
