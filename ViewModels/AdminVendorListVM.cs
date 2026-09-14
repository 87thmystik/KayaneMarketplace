using System.Collections.Generic;
using Kayane.Models;

namespace Kayane.ViewModels
{
    public class AdminVendorListVM
    {
        public List<AdminVendorListItemVM> Vendors { get; set; } = new();
        public VendorStatus? SelectedStatus { get; set; }
        public string? SearchKeyword { get; set; }
        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; }
        public int TotalCount { get; set; }
        public bool HasPreviousPage => CurrentPage > 1;
        public bool HasNextPage => CurrentPage < TotalPages;
    }
}