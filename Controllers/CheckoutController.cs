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

    [HttpGet]
    public IActionResult Index()
    {
        var cartItems = HttpContext.Session.Get<List<CartItemVM>>("Cart") ?? new List<CartItemVM>();
        if (!cartItems.Any())
        {
            TempData["ErrorMessage"] = "Your cart is empty.";
            return RedirectToAction("Index", "Cart");
        }

        var viewModel = new CheckoutVM
        {
            CustomerName = User.FindFirstValue(ClaimTypes.Name) ?? string.Empty,
            CustomerEmail = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty,
            Cart = new CartVM { Items = cartItems }
        };

        return View(viewModel);
    }

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

        model.Cart = new CartVM { Items = cartItems };

        if (!ModelState.IsValid)
        {
            return View("Index", model);
        }

        // Cash on delivery: skip the gateway entirely.
        if (model.SelectedPaymentMethod == PaymentMethod.CashOnDelivery)
        {
            return await ProcessCashOnDeliveryAsync(model, cartItems);
        }

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var userId = Guid.TryParse(
                User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedId)
                ? parsedId : Guid.Empty;

            decimal totalAmount = cartItems.Sum(i => i.Price * i.Quantity);
            var reference = $"KAY-{Guid.NewGuid():N}".ToUpperInvariant();

            var order = new Order
            {
                OrderId = Guid.NewGuid(),
                UserId = userId,
                CustomerName = model.CustomerName,
                CustomerEmail = model.CustomerEmail,
                CustomerPhone = model.CustomerPhone,
                ShippingAddress = model.ShippingAddress,
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
                {
                    throw new Exception($"Product '{item.ProductName}' is out of stock.");
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

            // Create the Payment record BEFORE calling the gateway.
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
                reference,
                order.TotalAmount,
                order.CustomerEmail ?? string.Empty,
                callbackUrl);

            if (!paymentResponse.Success)
            {
                throw new Exception(paymentResponse.Message ?? "Failed to initialize payment gateway.");
            }

            await transaction.CommitAsync();

            // Cart clears in Verify when payment actually succeeds.
            return Redirect(paymentResponse.RedirectUrl!);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            TempData["ErrorMessage"] = $"Checkout error: {ex.Message}";
            return View("Index", model);
        }
    }

    private async Task<IActionResult> ProcessCashOnDeliveryAsync(CheckoutVM model, List<CartItemVM> cartItems)
    {
        var userId = Guid.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedId)
            ? parsedId : Guid.Empty;

        decimal totalAmount = cartItems.Sum(i => i.Price * i.Quantity);

        var order = new Order
        {
            OrderId = Guid.NewGuid(),
            UserId = userId,
            CustomerName = model.CustomerName,
            CustomerEmail = model.CustomerEmail,
            CustomerPhone = model.CustomerPhone,
            ShippingAddress = model.ShippingAddress,
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

        await _context.SaveChangesAsync();
        HttpContext.Session.Remove("Cart");

        return RedirectToAction("Confirmation", new { id = order.OrderId });
    }

    // Gateway redirects here after the user completes (or abandons) payment.
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