using Kayane.Data;
using Kayane.Extensions;
using Kayane.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Kayane.Controllers;

public class CartController : Controller
{
    private readonly KayaneDb _context;
    private const string CartSessionKey = "Cart";

    public CartController(KayaneDb context)
    {
        _context = context;
    }

    [HttpGet]
    public IActionResult Index()
    {
        var vm = new CartVM { Items = GetCart() };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddToCart(Guid productId, int quantity = 1)
    {
        var product = await _context.Products
            .Include(p => p.Vendor)
            .FirstOrDefaultAsync(p => p.ProductId == productId);

        if (product == null) return NotFound();

        var qty = Math.Max(1, quantity);
        if (product.Stock < qty)
        {
            TempData["ErrorMessage"] = "Not enough stock available.";
            return RedirectToAction(nameof(Index));
        }

        var cart = GetCart();
        var existing = cart.FirstOrDefault(i => i.ProductId == productId);

        if (existing != null)
        {
            if (product.Stock < existing.Quantity + qty)
            {
                TempData["ErrorMessage"] = "Cannot add more items than available stock.";
                return RedirectToAction(nameof(Index));
            }
            existing.Quantity += qty;
        }
        else
        {
            var vendorName = product.Vendor?.BusinessName ?? "Kayane Partner";
            cart.Add(new CartItemVM
            {
                ProductId = product.ProductId,
                VendorId = product.VendorId,
                ProductName = product.Name,
                Name = product.Name,
                VendorName = vendorName,
                BusinessName = vendorName,
                Price = product.Price,
                Quantity = qty,
                ImageUrl = product.ImageUrl ?? string.Empty
            });
        }

        SaveCart(cart);
        TempData["SuccessMessage"] = $"Added '{product.Name}' to your cart.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult UpdateQuantity(Guid productId, int quantity)
    {
        var cart = GetCart();
        var item = cart.FirstOrDefault(i => i.ProductId == productId);

        if (item != null)
        {
            if (quantity <= 0)
                cart.Remove(item);
            else
                item.Quantity = quantity;

            SaveCart(cart);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult RemoveItem(Guid productId)
    {
        var cart = GetCart();
        cart.RemoveAll(i => i.ProductId == productId);
        SaveCart(cart);
        return RedirectToAction(nameof(Index));
    }

    private List<CartItemVM> GetCart()
        => HttpContext.Session.Get<List<CartItemVM>>(CartSessionKey) ?? new List<CartItemVM>();

    private void SaveCart(List<CartItemVM> cart)
        => HttpContext.Session.Set(CartSessionKey, cart);
}