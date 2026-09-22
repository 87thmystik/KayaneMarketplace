using Kayane.Data;
using Kayane.Helpers;
using Kayane.Models;
using Kayane.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Kayane.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class AdminCategoriesController : Controller
{
    private readonly KayaneDb _context;
    private readonly IAdminAuditService _audit;

    public AdminCategoriesController(KayaneDb context, IAdminAuditService audit)
    {
        _context = context;
        _audit = audit;
    }

    // GET: /Admin/AdminCategories
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var categories = await _context.Categories
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new Kayane.ViewModels.CategoryListItemVM
            {
                CategoryId = c.CategoryId,
                Name = c.Name,
                Slug = c.Slug,
                ProductCount = c.Products.Count
            })
            .ToListAsync();

        return View(categories);
    }

    // POST: /Admin/AdminCategories/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["ErrorMessage"] = "Category name is required.";
            return RedirectToAction(nameof(Index));
        }

        var trimmed = name.Trim();
        var slug = SlugHelper.GenerateSlug(trimmed);

        if (await _context.Categories.AnyAsync(c => c.Slug == slug || c.Name == trimmed))
        {
            TempData["ErrorMessage"] = $"Category '{trimmed}' already exists.";
            return RedirectToAction(nameof(Index));
        }

        var category = new Category
        {
            CategoryId = Guid.NewGuid(),
            Name = trimmed,
            Slug = slug
        };

        _context.Categories.Add(category);

        await _audit.LogAsync("category_created", "Category", category.CategoryId, new
        {
            name = category.Name,
            slug = category.Slug
        });

        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = $"Category '{trimmed}' created.";
        return RedirectToAction(nameof(Index));
    }

    // POST: /Admin/AdminCategories/Delete
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid categoryId)
    {
        var category = await _context.Categories
            .Include(c => c.Products)
            .FirstOrDefaultAsync(c => c.CategoryId == categoryId);

        if (category == null) return NotFound();

        if (category.Products.Any())
        {
            TempData["ErrorMessage"] =
                $"Cannot delete '{category.Name}' — {category.Products.Count} product(s) use it.";
            return RedirectToAction(nameof(Index));
        }

        _context.Categories.Remove(category);

        await _audit.LogAsync("category_deleted", "Category", category.CategoryId, new
        {
            name = category.Name
        });

        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = $"Category '{category.Name}' deleted.";
        return RedirectToAction(nameof(Index));
    }
}