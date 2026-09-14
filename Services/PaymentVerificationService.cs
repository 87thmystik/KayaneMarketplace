using Kayane.Data;
using Kayane.Models;
using Microsoft.EntityFrameworkCore;

namespace Kayane.Services;

public class PaymentVerificationService
{
    private readonly KayaneDb _context;
    private readonly PsbService _psbService;
    private readonly IEmailService _emailService;
    private readonly ILogger<PaymentVerificationService> _logger;

    public PaymentVerificationService(
        KayaneDb context,
        PsbService psbService,
        IEmailService emailService,
        ILogger<PaymentVerificationService> logger)
    {
        _context = context;
        _psbService = psbService;
        _emailService = emailService;
        _logger = logger;
    }

    public enum VerifyResult { Paid, Failed, AlreadyPaid, NotFound }

    public async Task<VerifyResult> VerifyAndFulfillAsync(string reference)
    {
        var payment = await _context.Payments
            .Include(p => p.Order)
                .ThenInclude(o => o!.OrderItems)
                    .ThenInclude(oi => oi.Product)
            .FirstOrDefaultAsync(p => p.PaymentReference == reference);

        if (payment == null || payment.Order == null)
        {
            _logger.LogWarning("Verification attempted for unknown reference {Ref}", reference);
            return VerifyResult.NotFound;
        }

        // Idempotency — if already fulfilled, don't re-credit wallets.
        if (payment.Status == PaymentStatus.Success)
            return VerifyResult.AlreadyPaid;

        // Ask the gateway.
        var verification = await _psbService.VerifyTransactionAsync(reference);

        if (!verification.IsSuccess)
        {
            await MarkFailedAsync(payment);
            return VerifyResult.Failed;
        }

        await MarkPaidAndCreditWalletsAsync(payment);
        return VerifyResult.Paid;
    }

    private async Task MarkFailedAsync(Payment payment)
    {
        payment.Status = PaymentStatus.Failed;
        payment.UpdatedAt = DateTime.UtcNow;

        if (payment.Order != null)
        {
            payment.Order.PaymentStatus = PaymentStatus.Failed;
            payment.Order.Status = OrderStatus.Cancelled;
            payment.Order.UpdatedAt = DateTime.UtcNow;

            // Restore stock for items in this order.
            foreach (var item in payment.Order.OrderItems)
            {
                if (item.Product != null)
                {
                    item.Product.Stock += item.Quantity;
                }
                item.Status = OrderItemStatus.Cancelled;
            }
        }

        await _context.SaveChangesAsync();
    }

    private async Task MarkPaidAndCreditWalletsAsync(Payment payment)
    {
        var order = payment.Order!;

        payment.Status = PaymentStatus.Success;
        payment.PaidAt = DateTime.UtcNow;
        payment.UpdatedAt = DateTime.UtcNow;

        order.PaymentStatus = PaymentStatus.Success;
        order.Status = OrderStatus.Paid;
        order.UpdatedAt = DateTime.UtcNow;

        // Credit each vendor's wallet for their share.
        foreach (var item in order.OrderItems)
        {
            if (item.Product == null) continue;

            item.Status = OrderItemStatus.Processing;

            var vendorId = item.Product.VendorId;

            var wallet = await _context.VendorWallets
                .FirstOrDefaultAsync(w => w.VendorId == vendorId);

            if (wallet == null)
            {
                wallet = new VendorWallet
                {
                    WalletId = Guid.NewGuid(),
                    VendorId = vendorId,
                    Balance = 0m,
                    TotalEarned = 0m,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.VendorWallets.Add(wallet);
            }

            wallet.Balance += item.TotalPrice;
            wallet.TotalEarned += item.TotalPrice;
            wallet.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        // Fire confirmation email — best effort, don't fail the transaction if SMTP is down.
        try
        {
            await _emailService.SendEmailAsync(
                order.CustomerEmail ?? string.Empty,
                $"Order Confirmation - #{order.OrderId.ToString()[..8]}",
                $"<h2>Thank you for your order, {order.CustomerName}!</h2>" +
                $"<p>Your payment was successful and your order is now being processed.</p>");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Confirmation email failed for order {OrderId}", order.OrderId);
        }
    }
}