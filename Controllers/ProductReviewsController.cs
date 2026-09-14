using Kayane.Data;
using Kayane.Models;
using Kayane.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Kayane.Controllers;

[Authorize]
public class ProductReviewsController : Controller
{
    private readonly KayaneDb _context;

    public ProductReviewsController(KayaneDb context)
    {
        _context = context;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(SubmitReviewVM model)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdStr, out var userId)) return Challenge();

        // Verify that the user has successfully purchased this product
        var hasPurchased = await _context.OrderItems
            .Include(oi => oi.Order)
            .AnyAsync(oi => oi.ProductId == model.ProductId &&
                            oi.Order.UserId == userId &&
                            oi.Order.PaymentStatus == PaymentStatus.Success);

        if (!hasPurchased)
        {
            TempData["ErrorMessage"] = "You can only review products you have successfully purchased.";
            return RedirectToAction("Details", "Store", new { id = model.ProductId });
        }

        // Check if user already reviewed this product
        var existingReview = await _context.ProductReviews
            .FirstOrDefaultAsync(r => r.ProductId == model.ProductId && r.UserId == userId);

        if (existingReview != null)
        {
            existingReview.Rating = model.Rating;
            existingReview.Comment = model.Comment;
            existingReview.CreatedAt = DateTime.UtcNow;
        }
        else
        {
            var review = new ProductReview
            {
                ReviewId = Guid.NewGuid(),
                ProductId = model.ProductId,
                UserId = userId,
                CustomerName = User.FindFirstValue(ClaimTypes.Name) ?? "Customer",
                Rating = model.Rating,
                Comment = model.Comment,
                CreatedAt = DateTime.UtcNow
            };
            _context.ProductReviews.Add(review);
        }

        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = "Your review has been submitted successfully.";
        return RedirectToAction("Details", "Store", new { id = model.ProductId });
    }
}