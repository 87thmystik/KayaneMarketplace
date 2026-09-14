namespace Kayane.Models;

public enum UserRole { User, Vendor, Admin }
public enum VendorStatus { Pending, Active, Suspended, Rejected, Approved }
public enum ProductStatus { Pending, Approved, Rejected }
public enum OrderStatus { Pending, Paid, Processing, Shipped, Completed, Cancelled }
public enum PaymentMethod { Paystack, CashOnDelivery, BankTransfer, Wallet }
public enum PaymentStatus { Pending, Success, Failed, Refunded, Completed, Paid }
public enum NotificationType { OrderUpdate, Promotion, SystemAlert, General }
public enum ClaimType { VendorStatus, ProductStatus, OrderStatus }
public enum OrderItemStatus { Pending, Processing, Shipped, Delivered, Cancelled, Completed }