using Kayane.Data;
using Kayane.Filters;
using Kayane.Models;
using Kayane.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Kayane.Controllers;

[Authorize]
[ApprovedVendor]
public class VendorWalletController : Controller
{
    private readonly KayaneDb _context;

    public VendorWalletController(KayaneDb context)
    {
        _context = context;
    }

    // GET: /VendorWallet
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var vendor = HttpContext.Items["CurrentVendor"] as Vendor;
        if (vendor == null) return Challenge();

        var wallet = await _context.VendorWallets
            .FirstOrDefaultAsync(w => w.VendorId == vendor.VendorId);

        if (wallet == null)
        {
            wallet = new VendorWallet
            {
                WalletId = Guid.NewGuid(),
                VendorId = vendor.VendorId,
                Balance = 0m,
                TotalEarned = 0m,
                UpdatedAt = DateTime.UtcNow
            };
            _context.VendorWallets.Add(wallet);
            await _context.SaveChangesAsync();
        }

        var payouts = await _context.PayoutTransactions
            .Where(p => p.VendorId == vendor.VendorId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        var totalEarnings = await _context.OrderItems
            .Include(oi => oi.Order)
            .Where(oi => oi.Product.VendorId == vendor.VendorId &&
                         oi.Order.PaymentStatus == PaymentStatus.Success)
            .SumAsync(oi => (decimal?)oi.TotalPrice) ?? 0m;

        var viewModel = new VendorWalletVM
        {
            Balance = wallet.Balance,
            TotalEarnings = totalEarnings,
            AccountNumber = vendor.AccountNumber ?? string.Empty,
            BankCode = vendor.BankCode ?? string.Empty,
            PayoutHistory = payouts
        };

        return View(viewModel);
    }

    // POST: /VendorWallet/RequestPayout
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestPayout(RequestPayoutInputModel model)
    {
        var vendor = HttpContext.Items["CurrentVendor"] as Vendor;
        if (vendor == null) return Challenge();

        if (model.Amount <= 0)
        {
            TempData["ErrorMessage"] = "Enter a valid payout amount greater than zero.";
            return RedirectToAction(nameof(Index));
        }

        if (string.IsNullOrWhiteSpace(vendor.AccountNumber) ||
            string.IsNullOrWhiteSpace(vendor.BankCode))
        {
            TempData["ErrorMessage"] =
                "Please update your bank account details before requesting a payout.";
            return RedirectToAction(nameof(Index));
        }

        var wallet = await _context.VendorWallets
            .FirstOrDefaultAsync(w => w.VendorId == vendor.VendorId);

        if (wallet == null || wallet.Balance < model.Amount)
        {
            TempData["ErrorMessage"] = "Insufficient wallet balance for this withdrawal.";
            return RedirectToAction(nameof(Index));
        }

        // Deduct and hold until admin approves or rejects.
        wallet.Balance -= model.Amount;
        wallet.UpdatedAt = DateTime.UtcNow;

        var payout = new PayoutTransaction
        {
            PayoutId = Guid.NewGuid(),
            VendorId = vendor.VendorId,
            Amount = model.Amount,
            Reference = $"PO-{Guid.NewGuid().ToString()[..12].ToUpperInvariant()}",
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };

        _context.PayoutTransactions.Add(payout);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] =
            "Payout request submitted. Awaiting admin approval.";
        return RedirectToAction(nameof(Index));
    }
}