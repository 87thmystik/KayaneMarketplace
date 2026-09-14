using Kayane.Data;
using Kayane.Models;
using Kayane.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Kayane.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class AdminPayoutsController : Controller
{
    private readonly KayaneDb _context;
    private readonly PsbService _psbService;
    private readonly ILogger<AdminPayoutsController> _logger;

    public AdminPayoutsController(
        KayaneDb context,
        PsbService psbService,
        ILogger<AdminPayoutsController> logger)
    {
        _context = context;
        _psbService = psbService;
        _logger = logger;
    }

    // GET: /Admin/AdminPayouts
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var payouts = await _context.PayoutTransactions
            .Include(p => p.Vendor)
            .AsNoTracking()
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        return View(payouts);
    }

    // POST: /Admin/AdminPayouts/ProcessPayout
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ProcessPayout(Guid payoutId, string actionType)
    {
        var payout = await _context.PayoutTransactions
            .Include(p => p.Vendor)
            .FirstOrDefaultAsync(p => p.PayoutId == payoutId);

        if (payout == null) return NotFound();

        if (payout.Status != "Pending")
        {
            TempData["ErrorMessage"] = "This payout request has already been processed.";
            return RedirectToAction(nameof(Index));
        }

        var vendor = payout.Vendor;

        if (actionType.Equals("Approve", StringComparison.OrdinalIgnoreCase))
        {
            // Attempt 9PSB transfer. Amount was already deducted from the wallet
            // when the vendor submitted the request (see VendorController.RequestPayout).
            if (vendor == null ||
                string.IsNullOrWhiteSpace(vendor.AccountNumber) ||
                string.IsNullOrWhiteSpace(vendor.BankCode))
            {
                TempData["ErrorMessage"] = "Vendor bank details are incomplete.";
                return RedirectToAction(nameof(Index));
            }

            var reference = $"PO-{Guid.NewGuid().ToString()[..12].ToUpperInvariant()}";

            bool transferSuccess = await _psbService.ProcessPayoutAsync(
                vendor.BankCode,
                vendor.AccountNumber,
                payout.Amount,
                $"Payout to {vendor.BusinessName}",
                reference
            );

            if (!transferSuccess)
            {
                payout.Status = "Failed";
                _logger.LogWarning(
                    "9PSB transfer failed for payout {PayoutId}, vendor {VendorId}, amount {Amount}",
                    payout.PayoutId, vendor.VendorId, payout.Amount);

                // Refund the held balance — vendor didn't receive the money.
                var wallet = await _context.VendorWallets
                    .FirstOrDefaultAsync(w => w.VendorId == vendor.VendorId);
                if (wallet != null)
                {
                    wallet.Balance += payout.Amount;
                    wallet.UpdatedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();
                TempData["ErrorMessage"] =
                    $"9PSB transfer failed for {vendor.BusinessName}. Funds refunded to wallet.";
                return RedirectToAction(nameof(Index));
            }

            payout.Status = "Approved";
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] =
                $"Payout of ₦{payout.Amount:N2} for {vendor.BusinessName} approved and transferred.";
        }
        else if (actionType.Equals("Reject", StringComparison.OrdinalIgnoreCase))
        {
            payout.Status = "Rejected";

            // Refund the held balance.
            var wallet = await _context.VendorWallets
                .FirstOrDefaultAsync(w => w.VendorId == payout.VendorId);
            if (wallet != null)
            {
                wallet.Balance += payout.Amount;
                wallet.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            TempData["ErrorMessage"] =
                $"Payout for {vendor?.BusinessName} rejected. Funds refunded to wallet.";
        }

        return RedirectToAction(nameof(Index));
    }
}