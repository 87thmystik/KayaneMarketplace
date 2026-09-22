using Kayane.Data;
using Kayane.Extensions;
using Kayane.Models;
using Kayane.Services;
using Kayane.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Kayane.Controllers;

[Authorize]
public class CheckoutController : Controller
{
    private readonly KayaneDb _context;
    private readonly PsbService _psbService;
    private readonly PaymentVerificationService _verifier;

    public CheckoutController(
        KayaneDb context,
        PsbService psbService,
        PaymentVerificationService verifier)
    {
        _context = context;
        _psbService = psbService;
        _verifier = verifier;
    }

    // GET: /Checkout
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var cartItems = HttpContext.Session.Get<List<CartItemVM>>("Cart") ?? new List<CartItemVM>();
        if (!cartItems.Any())
        {
            TempData["ErrorMessage"] = "Your cart is empty.";
            return RedirectToAction("Index", "Cart");
        }

        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        Guid.TryParse(userIdClaim, out var userId);

        var savedAddresses = await _context.BuyerAddresses
            .AsNoTracking()
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.IsDefault)
            .ThenByDescending(a => a.CreatedAt)
            .ToListAsync();

        var defaultAddress = savedAddresses.FirstOrDefault(a => a.IsDefault);

        var viewModel = new CheckoutVM
        {
            CustomerName = defaultAddress?.RecipientName ?? User.FindFirstValue(ClaimTypes.Name) ?? string.Empty,
            CustomerEmail = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty,
            CustomerPhone = defaultAddress?.Phone ?? string.Empty,
            ShippingAddress = defaultAddress?.FullAddress ?? string.Empty,
            Cart = new CartVM { Items = cartItems },
            SavedAddresses = savedAddresses,
            SelectedAddressId = defaultAddress?.AddressId
        };

        return View(viewModel);
    }

    // POST: /Checkout/Process
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Process(CheckoutVM model)
    {
        var cartItems = HttpContext.Session.Get<List<CartItemVM>>("Cart") ?? new List<CartItemVM>();
        if (!cartItems.Any())
        {
            TempData["ErrorMessage"] = "Your cart is empty.";
            return RedirectToAction("Index", "Cart");
        }

        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            TempData["ErrorMessage"] = "Session expired. Please sign in again.";
            return RedirectToAction("Index", "Cart");
        }

        // Reload saved addresses so the view can redisplay on validation failure
        var savedAddresses = await _context.BuyerAddresses
            .AsNoTracking()
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.IsDefault)
            .ThenByDescending(a => a.CreatedAt)
            .ToListAsync();

        model.Cart = new CartVM { Items = cartItems };
        model.SavedAddresses = savedAddresses;

        // Resolve the final shipping details
        string finalName;
        string finalPhone;
        string finalAddress;

        if (model.SelectedAddressId.HasValue)
        {
            var selected = savedAddresses.FirstOrDefault(a => a.AddressId == model.SelectedAddressId.Value);
            if (selected == null)
            {
                ModelState.AddModelError(string.Empty, "The selected address could not be found.");
                return View("Index", model);
            }

            finalName = selected.RecipientName;
            finalPhone = selected.Phone;
            finalAddress = selected.FullAddress;

            // Clear manual field requirements — saved address supersedes them
            ModelState.Remove(nameof(model.CustomerName));
            ModelState.Remove(nameof(model.CustomerPhone));
            ModelState.Remove(nameof(model.ShippingAddress));
        }
        else
        {
            // Using a manually-entered address — validate required fields
            if (string.IsNullOrWhiteSpace(model.CustomerName))
                ModelState.AddModelError(nameof(model.CustomerName), "Full name is required.");
            if (string.IsNullOrWhiteSpace(model.CustomerPhone))
                ModelState.AddModelError(nameof(model.CustomerPhone), "Phone number is required.");
            if (string.IsNullOrWhiteSpace(model.ShippingAddress))
                ModelState.AddModelError(nameof(model.ShippingAddress), "Shipping address is required.");

            finalName = model.CustomerName;
            finalPhone = model.CustomerPhone;
            finalAddress = model.ShippingAddress;
        }

        if (!ModelState.IsValid)
        {
            return View("Index", model);
        }

        // Cash on Delivery path — skip the gateway entirely
        if (model.SelectedPaymentMethod == PaymentMethod.CashOnDelivery)
        {
            return await ProcessCashOnDeliveryAsync(
                model, cartItems, userId, finalName, finalPhone, finalAddress);
        }

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            decimal totalAmount = cartItems.Sum(i => i.Price * i.Quantity);
            var reference = $"KAY-{Guid.NewGuid():N}".ToUpperInvariant();

            var order = new Order
            {
                OrderId = Guid.NewGuid(),
                UserId = userId,
                CustomerName = finalName,
                CustomerEmail = model.CustomerEmail,
                CustomerPhone = finalPhone,
                ShippingAddress = finalAddress,
                TotalAmount = totalAmount,
                Status = OrderStatus.Pending,
                PaymentMethod = model.SelectedPaymentMethod,
                PaymentStatus = PaymentStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };
            _context.Orders.Add(order);

            foreach (var item in cartItems)
            {
                var product = await _context.Products.FindAsync(item.ProductId);
                if (product == null || product.Stock < item.Quantity)
                    throw new Exception($"Product '{item.ProductName}' is out of stock.");

                product.Stock -= item.Quantity;

                _context.OrderItems.Add(new OrderItem
                {
                    OrderItemId = Guid.NewGuid(),
                    OrderId = order.OrderId,
                    ProductId = product.ProductId,
                    Quantity = item.Quantity,
                    UnitPrice = product.Price,
                    TotalPrice = product.Price * item.Quantity,
                    Status = OrderItemStatus.Pending
                });
            }

            // Save the new address if the buyer opted in
            if (!model.SelectedAddressId.HasValue && model.SaveNewAddress)
            {
                var newAddress = new BuyerAddress
                {
                    AddressId = Guid.NewGuid(),
                    UserId = userId,
                    Label = "Home",
                    RecipientName = finalName,
                    Phone = finalPhone,
                    AddressLine1 = finalAddress,
                    City = "",
                    State = "",
                    IsDefault = !await _context.BuyerAddresses.AnyAsync(a => a.UserId == userId),
                    CreatedAt = DateTime.UtcNow
                };
                _context.BuyerAddresses.Add(newAddress);
            }

            _context.Payments.Add(new Payment
            {
                PaymentId = Guid.NewGuid(),
                OrderId = order.OrderId,
                Amount = totalAmount,
                PaymentProvider = "9PSB",
                PaymentReference = reference,
                Status = PaymentStatus.Pending,
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            var callbackUrl = Url.Action(
                "Verify", "Checkout",
                new { reference = reference },
                Request.Scheme)!;

            var paymentResponse = await _psbService.InitializeTransactionAsync(
                reference, order.TotalAmount, order.CustomerEmail ?? "", callbackUrl);

            if (!paymentResponse.Success)
                throw new Exception(paymentResponse.Message ?? "Failed to initialize payment gateway.");

            await transaction.CommitAsync();

            // Cart clears in Verify when payment actually succeeds
            return Redirect(paymentResponse.RedirectUrl!);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            TempData["ErrorMessage"] = $"Checkout error: {ex.Message}";
            return View("Index", model);
        }
    }

    private async Task<IActionResult> ProcessCashOnDeliveryAsync(
        CheckoutVM model,
        List<CartItemVM> cartItems,
        Guid userId,
        string finalName,
        string finalPhone,
        string finalAddress)
    {
        decimal totalAmount = cartItems.Sum(i => i.Price * i.Quantity);

        var order = new Order
        {
            OrderId = Guid.NewGuid(),
            UserId = userId,
            CustomerName = finalName,
            CustomerEmail = model.CustomerEmail,
            CustomerPhone = finalPhone,
            ShippingAddress = finalAddress,
            TotalAmount = totalAmount,
            Status = OrderStatus.Pending,
            PaymentMethod = PaymentMethod.CashOnDelivery,
            PaymentStatus = PaymentStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        _context.Orders.Add(order);

        foreach (var item in cartItems)
        {
            var product = await _context.Products.FindAsync(item.ProductId);
            if (product == null || product.Stock < item.Quantity)
            {
                TempData["ErrorMessage"] = $"Product '{item.ProductName}' is out of stock.";
                return RedirectToAction("Index", "Cart");
            }

            product.Stock -= item.Quantity;

            _context.OrderItems.Add(new OrderItem
            {
                OrderItemId = Guid.NewGuid(),
                OrderId = order.OrderId,
                ProductId = product.ProductId,
                Quantity = item.Quantity,
                UnitPrice = product.Price,
                TotalPrice = product.Price * item.Quantity,
                Status = OrderItemStatus.Pending
            });
        }

        if (!model.SelectedAddressId.HasValue && model.SaveNewAddress)
        {
            var newAddress = new BuyerAddress
            {
                AddressId = Guid.NewGuid(),
                UserId = userId,
                Label = "Home",
                RecipientName = finalName,
                Phone = finalPhone,
                AddressLine1 = finalAddress,
                City = "",
                State = "",
                IsDefault = !await _context.BuyerAddresses.AnyAsync(a => a.UserId == userId),
                CreatedAt = DateTime.UtcNow
            };
            _context.BuyerAddresses.Add(newAddress);
        }

        await _context.SaveChangesAsync();
        HttpContext.Session.Remove("Cart");

        return RedirectToAction("Confirmation", new { id = order.OrderId });
    }

    // GET: /Checkout/Verify
    [HttpGet]
    public async Task<IActionResult> Verify(string reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            TempData["ErrorMessage"] = "Invalid payment reference.";
            return RedirectToAction("Index", "Cart");
        }

        var result = await _verifier.VerifyAndFulfillAsync(reference);

        var payment = await _context.Payments
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PaymentReference == reference);

        if (payment == null)
        {
            TempData["ErrorMessage"] = "Payment record not found.";
            return RedirectToAction("Index", "Cart");
        }

        switch (result)
        {
            case PaymentVerificationService.VerifyResult.Paid:
            case PaymentVerificationService.VerifyResult.AlreadyPaid:
                HttpContext.Session.Remove("Cart");
                return RedirectToAction("Confirmation", new { id = payment.OrderId });

            case PaymentVerificationService.VerifyResult.Failed:
            default:
                TempData["ErrorMessage"] = "Payment verification failed. Please try again.";
                return RedirectToAction("Index", "Cart");
        }
    }

    // GET: /Checkout/Confirmation/{id}
    [HttpGet]
    public async Task<IActionResult> Confirmation(Guid id)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdClaim, out var userId)) return Challenge();

        var order = await _context.Orders
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
            .FirstOrDefaultAsync(o => o.OrderId == id && o.UserId == userId);

        if (order == null) return NotFound();

        return View(order);
    }
}