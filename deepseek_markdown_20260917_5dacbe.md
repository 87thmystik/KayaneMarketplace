# Kayane Marketplace

A multi-vendor e-commerce platform built on ASP.NET Core MVC, PostgreSQL, and Tailwind CSS. Vendors can list products, customers can buy from multiple vendors in a single checkout, and admins moderate the entire marketplace.

---

## Features

### For Buyers
- Register, sign in, and browse a paginated product catalog
- Search and filter by category
- Add to cart (session-based)
- Checkout with delivery details and payment gateway
- Order tracking with per-item shipping status
- Leave reviews on purchased products (verified purchase only)

### For Vendors
- Business registration with bank details and logo/banner upload
- Application review workflow (pending → active via admin approval)
- Product creation with image upload, pricing, and stock management
- Order fulfillment dashboard (pending → processing → shipped → delivered)
- Wallet with automatic earnings credit on successful payment
- Payout requests with admin approval and automatic bank transfer via 9PSB

### For Admins
- Dashboard with platform-wide metrics
- Vendor approval and suspension
- Product moderation queue (approve/reject with rejection reasons)
- Category management
- Payout review and processing
- Audit trail of admin actions

### Security
- Cookie authentication with role-based authorization
- BCrypt/PasswordHasher password hashing with automatic rehashing
- Rate limiting on login, registration, and password reset
- Password reset via single-use expiring tokens (SHA-256 hashed in DB)
- HMAC-SHA512 webhook signature verification
- Anti-forgery tokens on all POST endpoints

---

## Tech Stack

| Layer | Technology |
|---|---|
| Framework | ASP.NET Core MVC (.NET 10) |
| Database | PostgreSQL via Entity Framework Core (Npgsql) |
| Auth | Cookie authentication + custom `IAuthService` |
| Frontend | Tailwind CSS v4 + Alpine.js |
| Payments | 9PSB (sandbox integration) |
| Email | SMTP (Mailtrap for dev, SendGrid/Mailgun for prod) |
| Build | `dotnet` CLI + `@tailwindcss/cli` |

---

## Prerequisites

- **.NET SDK 10.0** or later — [download](https://dotnet.microsoft.com/download)
- **PostgreSQL 14+** — running locally or accessible
- **Node.js 18+** (only for Tailwind CLI)
- **Git**

Verify:

```powershell
dotnet --version
psql --version
node --version
```

---

## Local Setup

### 1. Clone and restore

```powershell
git clone <your-repo-url>
cd Kayane
dotnet restore
```

### 2. Create the database

```sql
-- In psql or pgAdmin:
CREATE DATABASE "KayaneDb";
```

### 3. Configure `appsettings.json`

Edit `appsettings.json` in the project root:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=KayaneDb;Username=postgres;Password=YOUR_PASSWORD"
  },
  "Smtp": {
    "Host": "sandbox.smtp.mailtrap.io",
    "Port": "587",
    "User": "YOUR_MAILTRAP_USER",
    "Pass": "YOUR_MAILTRAP_PASS",
    "FromEmail": "no-reply@kayane.com",
    "FromName": "Kayane Marketplace"
  },
  "Psb": {
    "BaseUrl": "https://sandbox.9psb.com.ng/",
    "ApiKey": "YOUR_9PSB_API_KEY",
    "WebhookSecret": "YOUR_WEBHOOK_HMAC_SECRET"
  },
  "SeedAdmin": {
    "Email": "admin@kayane.com",
    "Password": "ChangeMeOnFirstRun123!",
    "Phone": "07000000000",
    "Name": "System Administrator"
  }
}
```

> ⚠️ **Do not commit real credentials to git.** For production, use environment variables or a secrets manager. See the Deployment section below.

### 4. Apply migrations

```powershell
dotnet ef database update
```

### 5. Build Tailwind CSS

One-time build:
```powershell
npx @tailwindcss/cli -i ./wwwroot/css/input.css -o ./wwwroot/css/site.css --minify
```

Watch mode during development:
```powershell
npx @tailwindcss/cli -i ./wwwroot/css/input.css -o ./wwwroot/css/site.css --watch
```

### 6. Run the app

```powershell
dotnet run
```

The app starts on `https://localhost:7039` (check the terminal for the exact URL).

On first startup:
- A default **admin user** is created from the `SeedAdmin` config section
- Watch the terminal for the admin email and password

### 7. Create some test data

Once logged in as admin, create a few categories at `/Admin/AdminCategories` — vendors need them to list products.

Recommended starter categories:
- Electronics
- Fashion
- Home & Kitchen
- Books
- Sports & Outdoors

---

## Default Accounts (dev only)

| Role | Email | Password |
|---|---|---|
| Admin | From `SeedAdmin` config | From `SeedAdmin` config |

Buyer and vendor accounts are created through the UI at `/Auth/Register`.

---

## Project Structure

```
Kayane/
├── Areas/
│   └── Admin/                          # Admin area
│       ├── Controllers/
│       │   ├── AdminCategoriesController.cs
│       │   ├── AdminPayoutsController.cs
│       │   ├── AdminProductsController.cs
│       │   └── DashboardController.cs
│       └── Views/                      # Admin views (with own _ViewStart/_ViewImports)
├── Controllers/
│   ├── AuthController.cs               # Login, register, password reset
│   ├── CartController.cs               # Session cart
│   ├── CheckoutController.cs           # Order placement + payment init
│   ├── CustomerOrdersController.cs     # Buyer order history
│   ├── HomeController.cs
│   ├── ProductReviewsController.cs
│   ├── ShopController.cs               # Public product catalog
│   ├── StoreController.cs              # Vendor storefronts (/store/{slug})
│   ├── StorefrontController.cs         # Product details + add-to-cart
│   ├── VendorController.cs             # Vendor dashboard + products + orders
│   ├── VendorWalletController.cs       # Wallet + payout requests
│   └── WebhooksController.cs           # Payment webhook (HMAC verified)
├── Data/
│   ├── KayaneDb.cs                     # EF DbContext
│   └── KayaneDbFactory.cs              # Design-time factory
├── Filters/
│   ├── ApprovedVendorFilter.cs         # Injects CurrentVendor
│   └── NoCacheFilter.cs
├── Helpers/
│   └── SlugHelper.cs
├── Migrations/
├── Models/
│   ├── User.cs, Vendor.cs, Product.cs, Category.cs
│   ├── Order.cs, OrderItem.cs, Payment.cs
│   ├── VendorWallet.cs, PayoutTransaction.cs
│   ├── ProductReview.cs, Notification.cs
│   ├── AdminAction.cs, AuditLog.cs
│   ├── PsbVirtualAccount.cs
│   └── Enums.cs
├── Services/
│   ├── AuthService.cs
│   ├── EmailService.cs
│   ├── PsbService.cs                   # 9PSB API client
│   └── PaymentVerificationService.cs   # Idempotent payment fulfillment
├── ViewModels/
├── Views/
├── wwwroot/
│   ├── css/
│   │   ├── input.css                   # Tailwind source
│   │   └── site.css                    # Compiled output (gitignored is fine)
│   ├── js/
│   └── uploads/
│       ├── products/                   # Vendor-uploaded product images
│       └── vendors/                    # Vendor logos + banners
├── appsettings.json
├── Program.cs
└── Kayane.csproj
```

---

## Key Workflows

### Order lifecycle

```
Buyer adds to cart → Checkout → Payment initialized (9PSB)
    ↓
Payment succeeds (browser redirect or webhook)
    ↓
PaymentVerificationService.VerifyAndFulfillAsync():
   - Marks payment success
   - Credits vendor wallets
   - Sends confirmation emails (buyer + vendors)
    ↓
Vendor marks items Shipped (with carrier + tracking) → email to buyer
    ↓
Vendor marks items Delivered → email to buyer
    ↓
When all items in an order are Delivered → order status flips to Completed
```

### Payout flow

```
Vendor requests payout → wallet balance deducted (held)
    ↓
Admin sees request at /Admin/AdminPayouts
    ↓
Approve → 9PSB transfer → on success, status = Approved
                       → on failure, wallet refunded, status = Failed
Reject → wallet refunded, status = Rejected
```

### Vendor approval

```
Vendor registers at /Auth/RegisterVendor → User + Vendor created, status = Pending
    ↓
Admin approves at /Admin/Dashboard/Vendors → status = Active
    ↓
ApprovedVendorFilter now allows access to /Vendor/*
```

---

## Configuration Reference

### `appsettings.json` sections

| Key | Purpose |
|---|---|
| `ConnectionStrings:DefaultConnection` | PostgreSQL connection string |
| `Smtp:Host` / `Port` / `User` / `Pass` | Outbound email server |
| `Smtp:FromEmail` / `FromName` | "From" identity on outgoing mail |
| `Psb:BaseUrl` | 9PSB API base URL (`sandbox` or production) |
| `Psb:ApiKey` | 9PSB bearer token |
| `Psb:WebhookSecret` | HMAC secret for validating incoming webhooks |
| `SeedAdmin:Email` / `Password` | First admin user (created only if no admin exists) |

---

## Deployment

### Secrets in production

**Never commit real credentials.** For deployment, override config via environment variables. .NET maps `Smtp:Host` to env var `Smtp__Host` (double underscore).

Example for a Linux host:

```bash
export ConnectionStrings__DefaultConnection="Host=...;Database=KayaneDb;..."
export Smtp__Host="smtp.sendgrid.net"
export Smtp__User="apikey"
export Smtp__Pass="SG.xxxxx"
export Psb__ApiKey="sk_live_xxxxx"
export Psb__WebhookSecret="whsec_xxxxx"
export SeedAdmin__Password="A-strong-random-password"
```

For Azure App Service, use **Configuration → Application settings**. For Railway/Render, use their env var UI.

### Database migrations in production

```bash
dotnet ef migrations bundle
./efbundle --connection "Host=...;Database=..."
```

Or run `dotnet ef database update` from a deployment slot with access.

### 9PSB go-live checklist

- [ ] Create a real 9PSB merchant account
- [ ] Update `Psb:BaseUrl` to the production endpoint
- [ ] Update `Psb:ApiKey` with the live key
- [ ] Set `Psb:WebhookSecret` to the value 9PSB provides
- [ ] Register your webhook URL (`https://yourdomain.com/api/webhooks/paystack` or `/api/webhooks/9psb`) with 9PSB
- [ ] Verify a small live transaction end-to-end

### SMTP go-live checklist

- [ ] Sign up with SendGrid / Mailgun / Amazon SES
- [ ] Verify your sender domain (add SPF + DKIM DNS records)
- [ ] Update `Smtp:*` config values
- [ ] Test that emails land in a real inbox (not spam)

### Recommended prod settings

```csharp
// Program.cs
app.UseHttpsRedirection();
app.UseHsts();
app.UseExceptionHandler("/Home/Error");
```

And in `AddSession`:
```csharp
options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
```

---

## Known Limitations / TODOs

- **9PSB integration is stubbed** — the API key in `appsettings.json` is a placeholder. Checkout cannot complete a real payment until real credentials are configured and the 9PSB request/response shape is verified against their docs.
- **Product image resizing** — uploads are stored at full size. For production, add resizing (e.g., ImageSharp) on upload.
- **No email verification** on signup — users can register with unverified emails.
- **Search is basic** — uses `Contains` on name and description. Consider full-text search for scale.
- **No pagination on vendor/admin order lists** — fine up to a few thousand orders.
- **Audit log is partial** — `AdminAction` table exists but only writes on vendor/product status changes; payout approvals aren't logged yet.
- **Review aggregates recomputed per request** — no cached `Product.AverageRating`. Fine at small scale, consider denormalizing later.

---

## Testing

There's no automated test suite yet. Manual smoke test:

1. **Buyer flow:** register → browse `/Shop` → add to cart → checkout
2. **Vendor flow:** register at `/Auth/RegisterVendor` → admin approves → add product with image → admin approves product → product appears on `/Shop`
3. **Order flow:** buy the product → vendor marks Processing → Shipped (with tracking) → Delivered → buyer sees status updates
4. **Payout flow:** vendor wallet credited on payment → vendor requests payout → admin approves → 9PSB transfer
5. **Rate limit:** submit 6 bad logins in a minute → "Slow Down" page
6. **Password reset:** request reset → check Mailtrap → click link → set new password

---

## Common Issues

**"The view 'X' was not found"** — check that the view file is in `Views/{ControllerName}/X.cshtml` (folder name must match controller name minus `Controller`). For admin views, they must be in `Areas/Admin/Views/{ControllerName}/`.

**"Vendor status is Pending"** — admin hasn't approved the vendor yet. Log in as admin, go to `/Admin/Dashboard/Vendors`, click Activate.

**Emails not arriving** — check that `Smtp:Host`, `User`, and `Pass` are set. If using Mailtrap, emails land in the Mailtrap web inbox, not a real inbox.

**Payment fails at 9PSB** — the API key is a placeholder. Replace with real sandbox credentials and verify the request shape against 9PSB docs (kobo vs naira, endpoint paths, webhook signature scheme).

**`dotnet ef` errors** — run from the project directory (where `Kayane.csproj` is). If "no design-time factory" error, ensure `Data/KayaneDbFactory.cs` exists.

**Tailwind build fails** — you need Tailwind v4. Run `npm install -D tailwindcss@latest @tailwindcss/cli@latest`. `input.css` should be just `@import "tailwindcss";`.

---

## License

Private. All rights reserved.

---

## Contact

Project owner: [your name / email]