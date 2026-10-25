using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Staybnb.Models;

namespace Staybnb.ViewModels;

public class HostApplicationViewModel
{
    public static readonly string[] PropertyTypes =
        { "Apartment", "House", "Villa", "Cabin", "Cottage", "Guesthouse", "Room" };

    [Required, StringLength(100)]
    public string Title { get; set; } = string.Empty;

    [Required, StringLength(4000, MinimumLength = 20)]
    public string Description { get; set; } = string.Empty;

    [Required, Display(Name = "Property type")]
    public string PropertyType { get; set; } = "Apartment";

    [Required, StringLength(200)]
    public string Address { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string City { get; set; } = string.Empty;

    [Range(1, 50), Display(Name = "Max guests")] public int MaxGuests { get; set; } = 2;
    [Range(0, 50)] public int Bedrooms { get; set; } = 1;
    [Range(0, 50)] public int Beds { get; set; } = 1;
    [Range(0, 50)] public int Bathrooms { get; set; } = 1;

    [Range(50, 1000000), Display(Name = "Price per night (R)")]
    public decimal PricePerNight { get; set; }

    [Range(0, 100000), Display(Name = "Cleaning fee (R)")]
    public decimal CleaningFee { get; set; }

    public List<int> SelectedAmenityIds { get; set; } = new();

    [Display(Name = "Property images")]
    public List<IFormFile> Images { get; set; } = new();

    [ValidateNever]
    public List<Amenity> AvailableAmenities { get; set; } = new();
}
