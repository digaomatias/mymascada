using MediatR;
using MyMascada.Application.Common.Interfaces;

namespace MyMascada.Application.Features.Categories.Commands;

/// <summary>
/// One-time command to backfill CanonicalKey for existing users who already have
/// English-seeded categories but were created before the CanonicalKey field existed.
/// </summary>
public record BackfillCanonicalKeysCommand : IRequest<BackfillCanonicalKeysResult>;

public class BackfillCanonicalKeysResult
{
    public int UsersProcessed { get; set; }
    public int CategoriesUpdated { get; set; }
}

public class BackfillCanonicalKeysCommandHandler : IRequestHandler<BackfillCanonicalKeysCommand, BackfillCanonicalKeysResult>
{
    private readonly ICategoryRepository _categoryRepository;
    private readonly IApplicationLogger<BackfillCanonicalKeysCommandHandler> _logger;

    /// <summary>
    /// Static mapping of English seed category names to their canonical keys.
    /// Used for case-insensitive matching against existing category names.
    /// </summary>
    private static readonly Dictionary<string, string> EnglishNameToCanonicalKey =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // Parent categories
            ["Income"] = "income",
            ["Housing & Utilities"] = "housing_utilities",
            ["Transportation"] = "transportation",
            ["Food & Dining"] = "food_dining",
            ["Health & Medical"] = "health_medical",
            ["Personal Care"] = "personal_care",
            ["Entertainment"] = "entertainment",
            ["Travel & Vacation"] = "travel_vacation",
            ["Financial Services"] = "financial_services",
            ["Debt Payments"] = "debt_payments",
            ["Technology"] = "technology",
            ["Family & Children"] = "family_children",
            ["Gifts & Donations"] = "gifts_donations",
            ["Miscellaneous"] = "miscellaneous",
            ["Transfers"] = "transfers",

            // Subcategories - Income
            ["Salary"] = "salary",
            ["Freelance"] = "freelance",
            ["Business Income"] = "business_income",
            ["Investment Income"] = "investment_income",
            ["Side Hustle"] = "side_hustle",
            ["Gifts & Bonuses"] = "gifts_bonuses",
            ["Other Income"] = "other_income",

            // Subcategories - Housing & Utilities
            ["Rent/Mortgage"] = "rent_mortgage",
            ["Electricity"] = "electricity",
            ["Gas"] = "gas_utility",
            ["Water & Sewer"] = "water_sewer",
            ["Internet & Cable"] = "internet_cable",
            ["Trash & Recycling"] = "trash_recycling",
            ["Home Insurance"] = "home_insurance",
            ["Home Maintenance"] = "home_maintenance",

            // Subcategories - Transportation
            ["Gas & Fuel"] = "gas_fuel",
            ["Car Payment"] = "car_payment",
            ["Car Insurance"] = "car_insurance",
            ["Car Maintenance"] = "car_maintenance",
            ["Public Transit"] = "public_transit",
            ["Rideshare"] = "rideshare",
            ["Parking"] = "parking",
            ["Vehicle Registration"] = "vehicle_registration",

            // Subcategories - Food & Dining
            ["Groceries"] = "groceries",
            ["Restaurants"] = "restaurants",
            ["Fast Food"] = "fast_food",
            ["Coffee Shops"] = "coffee_shops",
            ["Delivery"] = "delivery",
            ["Meal Kits"] = "meal_kits",
            ["Work Lunches"] = "work_lunches",

            // Subcategories - Health & Medical
            ["Doctor Visits"] = "doctor_visits",
            ["Dental Care"] = "dental_care",
            ["Prescriptions"] = "prescriptions",
            ["Health Insurance"] = "health_insurance",
            ["Vision Care"] = "vision_care",
            ["Mental Health"] = "mental_health",
            ["Fitness"] = "fitness",
            ["Alternative Medicine"] = "alternative_medicine",

            // Subcategories - Personal Care
            ["Haircuts & Styling"] = "haircuts_styling",
            ["Skincare & Cosmetics"] = "skincare_cosmetics",
            ["Clothing"] = "clothing",
            ["Dry Cleaning"] = "dry_cleaning",
            ["Personal Hygiene"] = "personal_hygiene",
            ["Spa & Wellness"] = "spa_wellness",

            // Subcategories - Entertainment
            ["Streaming Services"] = "streaming_services",
            ["Movies & Theater"] = "movies_theater",
            ["Concerts & Events"] = "concerts_events",
            ["Gaming"] = "gaming",
            ["Books & Media"] = "books_media",
            ["Hobbies"] = "hobbies",
            ["Sports & Recreation"] = "sports_recreation",

            // Subcategories - Travel & Vacation
            ["Flights"] = "flights",
            ["Hotels & Lodging"] = "hotels_lodging",
            ["Car Rentals"] = "car_rentals",
            ["Travel Food"] = "travel_food",
            ["Activities & Tours"] = "activities_tours",
            ["Travel Insurance"] = "travel_insurance",
            ["Souvenirs"] = "souvenirs",

            // Subcategories - Financial Services
            ["Bank Fees"] = "bank_fees",
            ["Investment Fees"] = "investment_fees",
            ["Credit Card Fees"] = "credit_card_fees",
            ["Tax Preparation"] = "tax_preparation",
            ["Financial Planning"] = "financial_planning",
            ["Insurance Premiums"] = "insurance_premiums",
            ["Legal Services"] = "legal_services",

            // Subcategories - Debt Payments
            ["Credit Card Payments"] = "credit_card_payments",
            ["Student Loans"] = "student_loans",
            ["Personal Loans"] = "personal_loans",
            ["Other Debt"] = "other_debt",

            // Subcategories - Technology
            ["Phone Bill"] = "phone_bill",
            ["Software Subscriptions"] = "software_subscriptions",
            ["Electronics"] = "electronics",
            ["Cloud Storage"] = "cloud_storage",
            ["Tech Support"] = "tech_support",
            ["Domain & Hosting"] = "domain_hosting",

            // Subcategories - Family & Children
            ["Childcare"] = "childcare",
            ["School Supplies"] = "school_supplies",
            ["Children's Clothing"] = "childrens_clothing",
            ["Toys & Games"] = "toys_games",
            ["Children's Activities"] = "childrens_activities",
            ["Baby Supplies"] = "baby_supplies",
            ["Pet Care"] = "pet_care",

            // Subcategories - Gifts & Donations
            ["Birthday Gifts"] = "birthday_gifts",
            ["Holiday Gifts"] = "holiday_gifts",
            ["Wedding Gifts"] = "wedding_gifts",
            ["Charitable Donations"] = "charitable_donations",
            ["Religious Donations"] = "religious_donations",
            ["Political Donations"] = "political_donations",

            // Subcategories - Miscellaneous
            ["ATM Withdrawals"] = "atm_withdrawals",
            ["Postage & Shipping"] = "postage_shipping",
            ["Office Supplies"] = "office_supplies",
            ["Uncategorized"] = "uncategorized",

            // Subcategories - Transfers
            ["Account Transfers"] = "account_transfers",
            ["Savings Transfer"] = "savings_transfer",
            ["Investment Transfer"] = "investment_transfer",
            ["Payment to Others"] = "payment_to_others",
        };

    public BackfillCanonicalKeysCommandHandler(
        ICategoryRepository categoryRepository,
        IApplicationLogger<BackfillCanonicalKeysCommandHandler> logger)
    {
        _categoryRepository = categoryRepository;
        _logger = logger;
    }

    public async Task<BackfillCanonicalKeysResult> Handle(BackfillCanonicalKeysCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting canonical key backfill for categories with null CanonicalKey");

        var categories = await _categoryRepository.GetCategoriesWithNullCanonicalKeyAsync();
        var categoryList = categories.ToList();

        _logger.LogInformation("Found {Count} categories with null CanonicalKey", categoryList.Count);

        var usersProcessed = new HashSet<Guid>();
        int categoriesUpdated = 0;

        foreach (var category in categoryList)
        {
            if (EnglishNameToCanonicalKey.TryGetValue(category.Name, out var canonicalKey))
            {
                category.CanonicalKey = canonicalKey;
                category.UpdatedAt = DateTime.UtcNow;
                categoriesUpdated++;

                if (category.UserId.HasValue)
                {
                    usersProcessed.Add(category.UserId.Value);
                }

                _logger.LogDebug(
                    "Matched category '{Name}' (Id={Id}) to canonical key '{CanonicalKey}'",
                    category.Name, category.Id, canonicalKey);
            }
        }

        if (categoriesUpdated > 0)
        {
            await _categoryRepository.SaveChangesAsync();
        }

        _logger.LogInformation(
            "Canonical key backfill completed: {UsersProcessed} users processed, {CategoriesUpdated} categories updated",
            usersProcessed.Count, categoriesUpdated);

        return new BackfillCanonicalKeysResult
        {
            UsersProcessed = usersProcessed.Count,
            CategoriesUpdated = categoriesUpdated
        };
    }
}
