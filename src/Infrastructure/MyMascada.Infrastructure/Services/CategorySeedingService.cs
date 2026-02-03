using Microsoft.EntityFrameworkCore;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;
using MyMascada.Infrastructure.Data;

namespace MyMascada.Infrastructure.Services;

/// <summary>
/// Service for seeding default categories for new users.
/// Supports multiple locales with canonical keys for stable identification.
/// </summary>
public class CategorySeedingService : ICategorySeedingService
{
    private readonly ApplicationDbContext _context;

    private static readonly IReadOnlyList<string> SupportedLocales = new[] { "en", "pt-BR" };

    public CategorySeedingService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> UserHasCategoriesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.Categories
            .AnyAsync(c => c.UserId == userId, cancellationToken);
    }

    public IReadOnlyList<string> GetAvailableLocales() => SupportedLocales;

    public async Task<int> CreateDefaultCategoriesAsync(Guid userId, string locale = "en", CancellationToken cancellationToken = default)
    {
        // Don't create if user already has categories
        if (await UserHasCategoriesAsync(userId, cancellationToken))
            return 0;

        var resolvedLocale = SupportedLocales.Contains(locale) ? locale : "en";
        var categories = BuildCategoryTree(userId, resolvedLocale);

        await _context.Categories.AddRangeAsync(categories, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return categories.Count;
    }

    // =====================================================================
    // Seed data definition
    // =====================================================================

    /// <summary>
    /// Locale-specific name and description for a category.
    /// </summary>
    private sealed record LocaleText(string Name, string Description);

    /// <summary>
    /// Template for a single category (parent or child).
    /// </summary>
    private sealed record CategorySeed(
        string CanonicalKey,
        string Icon,
        string Color,
        CategoryType Type,
        Dictionary<string, LocaleText> Locales,
        List<CategorySeed>? Children = null);

    /// <summary>
    /// Master seed data: canonical key is the stable identifier; icons and colors
    /// are locale-independent; names and descriptions vary by locale.
    /// </summary>
    private static readonly List<CategorySeed> SeedData = new()
    {
        // =================================================================
        // INCOME CATEGORIES
        // =================================================================
        new("income", "💰", "#4CAF50", CategoryType.Income,
            new()
            {
                ["en"] = new("Income", "All sources of income"),
                ["pt-BR"] = new("Receitas", "Todas as fontes de receita")
            },
            new()
            {
                new("salary", "💼", "#66BB6A", CategoryType.Income,
                    new()
                    {
                        ["en"] = new("Salary", "Regular employment income"),
                        ["pt-BR"] = new("Salário", "Renda de emprego regular")
                    }),
                new("freelance", "🎯", "#81C784", CategoryType.Income,
                    new()
                    {
                        ["en"] = new("Freelance", "Contract and freelance work"),
                        ["pt-BR"] = new("Freelance", "Trabalho autônomo e freelance")
                    }),
                new("business_income", "🏢", "#A5D6A7", CategoryType.Income,
                    new()
                    {
                        ["en"] = new("Business Income", "Revenue from business activities"),
                        ["pt-BR"] = new("Renda Empresarial", "Receita de atividades empresariais")
                    }),
                new("investment_income", "📈", "#C8E6C9", CategoryType.Income,
                    new()
                    {
                        ["en"] = new("Investment Income", "Dividends, interest, capital gains"),
                        ["pt-BR"] = new("Renda de Investimentos", "Dividendos, juros, ganhos de capital")
                    }),
                new("side_hustle", "⚡", "#E8F5E8", CategoryType.Income,
                    new()
                    {
                        ["en"] = new("Side Hustle", "Income from side projects"),
                        ["pt-BR"] = new("Renda Extra", "Renda de projetos paralelos")
                    }),
                new("gifts_bonuses", "🎁", "#F1F8E9", CategoryType.Income,
                    new()
                    {
                        ["en"] = new("Gifts & Bonuses", "Monetary gifts and work bonuses"),
                        ["pt-BR"] = new("Presentes & Bônus", "Presentes em dinheiro e bônus de trabalho")
                    }),
                new("other_income", "➕", "#F9FBE7", CategoryType.Income,
                    new()
                    {
                        ["en"] = new("Other Income", "Miscellaneous income sources"),
                        ["pt-BR"] = new("Outras Receitas", "Fontes diversas de receita")
                    })
            }),

        // =================================================================
        // HOUSING & UTILITIES
        // =================================================================
        new("housing_utilities", "🏠", "#2196F3", CategoryType.Expense,
            new()
            {
                ["en"] = new("Housing & Utilities", "Housing costs and utilities"),
                ["pt-BR"] = new("Moradia & Serviços", "Custos de moradia e serviços")
            },
            new()
            {
                new("rent_mortgage", "🏘️", "#42A5F5", CategoryType.Expense,
                    new()
                    {
                        ["en"] = new("Rent/Mortgage", "Monthly housing payments"),
                        ["pt-BR"] = new("Aluguel/Financiamento", "Pagamentos mensais de moradia")
                    }),
                new("electricity", "⚡", "#64B5F6", CategoryType.Expense,
                    new()
                    {
                        ["en"] = new("Electricity", "Electric utility bills"),
                        ["pt-BR"] = new("Energia Elétrica", "Contas de energia elétrica")
                    }),
                new("gas_utility", "🔥", "#90CAF9", CategoryType.Expense,
                    new()
                    {
                        ["en"] = new("Gas", "Natural gas and heating"),
                        ["pt-BR"] = new("Gás", "Gás natural e aquecimento")
                    }),
                new("water_sewer", "💧", "#BBDEFB", CategoryType.Expense,
                    new()
                    {
                        ["en"] = new("Water & Sewer", "Water and sewer utilities"),
                        ["pt-BR"] = new("Água & Esgoto", "Serviços de água e esgoto")
                    }),
                new("internet_cable", "📡", "#E3F2FD", CategoryType.Expense,
                    new()
                    {
                        ["en"] = new("Internet & Cable", "Internet and TV services"),
                        ["pt-BR"] = new("Internet & TV", "Serviços de internet e TV")
                    }),
                new("trash_recycling", "🗑️", "#F3E5F5", CategoryType.Expense,
                    new()
                    {
                        ["en"] = new("Trash & Recycling", "Waste management services"),
                        ["pt-BR"] = new("Lixo & Reciclagem", "Serviços de coleta de lixo")
                    }),
                new("home_insurance", "🛡️", "#E8EAF6", CategoryType.Expense,
                    new()
                    {
                        ["en"] = new("Home Insurance", "Property and home insurance"),
                        ["pt-BR"] = new("Seguro Residencial", "Seguro de propriedade e residência")
                    }),
                new("home_maintenance", "🔧", "#C5CAE9", CategoryType.Expense,
                    new()
                    {
                        ["en"] = new("Home Maintenance", "Repairs and maintenance"),
                        ["pt-BR"] = new("Manutenção do Lar", "Reparos e manutenção")
                    })
            }),

        // =================================================================
        // TRANSPORTATION
        // =================================================================
        new("transportation", "🚗", "#FF9800", CategoryType.Expense,
            new()
            {
                ["en"] = new("Transportation", "Vehicle and transportation costs"),
                ["pt-BR"] = new("Transporte", "Custos de veículo e transporte")
            },
            new()
            {
                new("gas_fuel", "⛽", "#FFB74D", CategoryType.Expense,
                    new()
                    {
                        ["en"] = new("Gas & Fuel", "Vehicle fuel costs"),
                        ["pt-BR"] = new("Combustível", "Custos de combustível do veículo")
                    }),
                new("car_payment", "🚙", "#FFCC02", CategoryType.Expense,
                    new()
                    {
                        ["en"] = new("Car Payment", "Vehicle loan payments"),
                        ["pt-BR"] = new("Parcela do Carro", "Pagamentos de financiamento do veículo")
                    }),
                new("car_insurance", "🛡️", "#FFD54F", CategoryType.Expense,
                    new()
                    {
                        ["en"] = new("Car Insurance", "Vehicle insurance premiums"),
                        ["pt-BR"] = new("Seguro do Carro", "Prêmios de seguro do veículo")
                    }),
                new("car_maintenance", "🔧", "#FFE082", CategoryType.Expense,
                    new()
                    {
                        ["en"] = new("Car Maintenance", "Vehicle repairs and service"),
                        ["pt-BR"] = new("Manutenção do Carro", "Reparos e serviços do veículo")
                    }),
                new("public_transit", "🚌", "#FFECB3", CategoryType.Expense,
                    new()
                    {
                        ["en"] = new("Public Transit", "Bus, train, subway fares"),
                        ["pt-BR"] = new("Transporte Público", "Passagens de ônibus, trem e metrô")
                    }),
                new("rideshare", "🚕", "#FFF3E0", CategoryType.Expense,
                    new()
                    {
                        ["en"] = new("Rideshare", "Uber, Lyft, taxi costs"),
                        ["pt-BR"] = new("Transporte por App", "Custos de Uber, 99, táxi")
                    }),
                new("parking", "🅿️", "#FFF8E1", CategoryType.Expense,
                    new()
                    {
                        ["en"] = new("Parking", "Parking fees and permits"),
                        ["pt-BR"] = new("Estacionamento", "Taxas e licenças de estacionamento")
                    }),
                new("vehicle_registration", "📋", "#FFFDE7", CategoryType.Expense,
                    new()
                    {
                        ["en"] = new("Vehicle Registration", "DMV fees and registration"),
                        ["pt-BR"] = new("Licenciamento", "Taxas de licenciamento e IPVA")
                    })
            }),

        // =================================================================
        // FOOD & DINING
        // =================================================================
        new("food_dining", "🍽️", "#4CAF50", CategoryType.Expense,
            new()
            {
                ["en"] = new("Food & Dining", "Food and restaurant expenses"),
                ["pt-BR"] = new("Alimentação", "Despesas com alimentação e restaurantes")
            },
            new()
            {
                new("groceries", "🛒", "#66BB6A", CategoryType.Expense,
                    new()
                    {
                        ["en"] = new("Groceries", "Supermarket and food shopping"),
                        ["pt-BR"] = new("Supermercado", "Compras de supermercado e alimentos")
                    }),
                new("restaurants", "🍴", "#81C784", CategoryType.Expense,
                    new()
                    {
                        ["en"] = new("Restaurants", "Dining out and takeout"),
                        ["pt-BR"] = new("Restaurantes", "Refeições fora e delivery")
                    }),
                new("fast_food", "🍔", "#A5D6A7", CategoryType.Expense,
                    new()
                    {
                        ["en"] = new("Fast Food", "Quick service restaurants"),
                        ["pt-BR"] = new("Fast Food", "Restaurantes de serviço rápido")
                    }),
                new("coffee_shops", "☕", "#C8E6C9", CategoryType.Expense,
                    new()
                    {
                        ["en"] = new("Coffee Shops", "Coffee and café purchases"),
                        ["pt-BR"] = new("Cafeterias", "Compras em cafeterias e padarias")
                    }),
                new("delivery", "🚚", "#E8F5E8", CategoryType.Expense,
                    new()
                    {
                        ["en"] = new("Delivery", "Food delivery services"),
                        ["pt-BR"] = new("Delivery", "Serviços de entrega de comida")
                    }),
                new("meal_kits", "📦", "#F1F8E9", CategoryType.Expense,
                    new()
                    {
                        ["en"] = new("Meal Kits", "Subscription meal services"),
                        ["pt-BR"] = new("Kits de Refeição", "Serviços de assinatura de refeições")
                    }),
                new("work_lunches", "🥪", "#F9FBE7", CategoryType.Expense,
                    new()
                    {
                        ["en"] = new("Work Lunches", "Meals during work hours"),
                        ["pt-BR"] = new("Almoço de Trabalho", "Refeições durante o horário de trabalho")
                    })
            }),

        // =================================================================
        // HEALTH & MEDICAL
        // =================================================================
        new("health_medical", "🏥", "#E91E63", CategoryType.Expense,
            new()
            {
                ["en"] = new("Health & Medical", "Healthcare and medical expenses"),
                ["pt-BR"] = new("Saúde & Médico", "Despesas com saúde e médicas")
            },
            new()
            {
                new("doctor_visits", "👨‍⚕️", "#F06292", CategoryType.Expense,
                    new()
                    {
                        ["en"] = new("Doctor Visits", "Medical appointments and checkups"),
                        ["pt-BR"] = new("Consultas Médicas", "Consultas e exames médicos")
                    }),
                new("dental_care", "🦷", "#F48FB1", CategoryType.Expense,
                    new()
                    {
                        ["en"] = new("Dental Care", "Dental appointments and procedures"),
                        ["pt-BR"] = new("Dentista", "Consultas e procedimentos dentários")
                    }),
                new("prescriptions", "💊", "#F8BBD9", CategoryType.Expense,
                    new()
                    {
                        ["en"] = new("Prescriptions", "Prescription medications"),
                        ["pt-BR"] = new("Medicamentos", "Medicamentos com receita")
                    }),
                new("health_insurance", "🛡️", "#FCE4EC", CategoryType.Expense,
                    new()
                    {
                        ["en"] = new("Health Insurance", "Medical insurance premiums"),
                        ["pt-BR"] = new("Plano de Saúde", "Mensalidades do plano de saúde")
                    }),
                new("vision_care", "👓", "#FDF2F8", CategoryType.Expense,
                    new()
                    {
                        ["en"] = new("Vision Care", "Eye exams and glasses/contacts"),
                        ["pt-BR"] = new("Oftalmologia", "Exames e óculos/lentes de contato")
                    }),
                new("mental_health", "🧠", "#F3E5F5", CategoryType.Expense,
                    new()
                    {
                        ["en"] = new("Mental Health", "Therapy and counseling services"),
                        ["pt-BR"] = new("Saúde Mental", "Terapia e serviços de aconselhamento")
                    }),
                new("fitness", "💪", "#E8EAF6", CategoryType.Expense,
                    new()
                    {
                        ["en"] = new("Fitness", "Gym memberships and fitness"),
                        ["pt-BR"] = new("Academia", "Mensalidades de academia e fitness")
                    }),
                new("alternative_medicine", "🌿", "#C5CAE9", CategoryType.Expense,
                    new()
                    {
                        ["en"] = new("Alternative Medicine", "Chiropractic, acupuncture, etc."),
                        ["pt-BR"] = new("Medicina Alternativa", "Quiropraxia, acupuntura, etc.")
                    })
            }),

        // =================================================================
        // PERSONAL CARE
        // =================================================================
        new("personal_care", "💄", "#9C27B0", CategoryType.Expense,
            new()
            {
                ["en"] = new("Personal Care", "Personal grooming and care"),
                ["pt-BR"] = new("Cuidados Pessoais", "Higiene e cuidados pessoais")
            },
            new()
            {
                new("haircuts_styling", "💇", "#BA68C8", CategoryType.Expense,
                    new()
                    {
                        ["en"] = new("Haircuts & Styling", "Hair salon and barber visits"),
                        ["pt-BR"] = new("Cortes & Penteados", "Visitas ao salão e barbearia")
                    }),
                new("skincare_cosmetics", "💅", "#CE93D8", CategoryType.Expense,
                    new()
                    {
                        ["en"] = new("Skincare & Cosmetics", "Beauty products and treatments"),
                        ["pt-BR"] = new("Cosméticos & Beleza", "Produtos de beleza e tratamentos")
                    }),
                new("clothing", "👕", "#E1BEE7", CategoryType.Expense,
                    new()
                    {
                        ["en"] = new("Clothing", "Apparel and accessories"),
                        ["pt-BR"] = new("Roupas", "Vestuário e acessórios")
                    }),
                new("dry_cleaning", "🧥", "#F3E5F5", CategoryType.Expense,
                    new()
                    {
                        ["en"] = new("Dry Cleaning", "Laundry and dry cleaning services"),
                        ["pt-BR"] = new("Lavanderia", "Serviços de lavanderia e lavagem a seco")
                    }),
                new("personal_hygiene", "🧴", "#F8BBD9", CategoryType.Expense,
                    new()
                    {
                        ["en"] = new("Personal Hygiene", "Toiletries and hygiene products"),
                        ["pt-BR"] = new("Higiene Pessoal", "Produtos de higiene pessoal")
                    }),
                new("spa_wellness", "🧖", "#FCE4EC", CategoryType.Expense,
                    new()
                    {
                        ["en"] = new("Spa & Wellness", "Massage and wellness treatments"),
                        ["pt-BR"] = new("Spa & Bem-Estar", "Massagens e tratamentos de bem-estar")
                    })
            }),

        // =================================================================
        // ENTERTAINMENT
        // =================================================================
        new("entertainment", "🎬", "#673AB7", CategoryType.Expense,
            new()
            {
                ["en"] = new("Entertainment", "Entertainment and leisure activities"),
                ["pt-BR"] = new("Entretenimento", "Atividades de entretenimento e lazer")
            },
            new()
            {
                new("streaming_services", "📺", "#9575CD", CategoryType.Expense,
                    new()
                    {
                        ["en"] = new("Streaming Services", "Netflix, Spotify, gaming subscriptions"),
                        ["pt-BR"] = new("Serviços de Streaming", "Assinaturas de Netflix, Spotify, jogos")
                    }),
                new("movies_theater", "🎭", "#B39DDB", CategoryType.Expense,
                    new()
                    {
                        ["en"] = new("Movies & Theater", "Cinema tickets and live shows"),
                        ["pt-BR"] = new("Cinema & Teatro", "Ingressos de cinema e shows ao vivo")
                    }),
                new("concerts_events", "🎵", "#D1C4E9", CategoryType.Expense,
                    new()
                    {
                        ["en"] = new("Concerts & Events", "Live music and entertainment events"),
                        ["pt-BR"] = new("Shows & Eventos", "Shows ao vivo e eventos de entretenimento")
                    }),
                new("gaming", "🎮", "#EDE7F6", CategoryType.Expense,
                    new()
                    {
                        ["en"] = new("Gaming", "Video games and gaming equipment"),
                        ["pt-BR"] = new("Jogos", "Videogames e equipamentos de jogos")
                    }),
                new("books_media", "📚", "#F3E5F5", CategoryType.Expense,
                    new()
                    {
                        ["en"] = new("Books & Media", "Books, audiobooks, magazines"),
                        ["pt-BR"] = new("Livros & Mídia", "Livros, audiolivros, revistas")
                    }),
                new("hobbies", "🎨", "#E8EAF6", CategoryType.Expense,
                    new()
                    {
                        ["en"] = new("Hobbies", "Craft supplies and hobby equipment"),
                        ["pt-BR"] = new("Hobbies", "Materiais de artesanato e equipamentos de hobby")
                    }),
                new("sports_recreation", "⚽", "#C5CAE9", CategoryType.Expense,
                    new()
                    {
                        ["en"] = new("Sports & Recreation", "Sports equipment and activities"),
                        ["pt-BR"] = new("Esportes & Lazer", "Equipamentos esportivos e atividades")
                    })
            }),

        // =================================================================
        // TRAVEL & VACATION
        // =================================================================
        new("travel_vacation", "✈️", "#00BCD4", CategoryType.Expense,
            new()
            {
                ["en"] = new("Travel & Vacation", "Travel and vacation expenses"),
                ["pt-BR"] = new("Viagens & Férias", "Despesas com viagens e férias")
            },
            new()
            {
                new("flights", "🛫", "#26C6DA", CategoryType.Expense,
                    new()
                    {
                        ["en"] = new("Flights", "Airline tickets and airfare"),
                        ["pt-BR"] = new("Passagens Aéreas", "Bilhetes e tarifas aéreas")
                    }),
                new("hotels_lodging", "🏨", "#4DD0E1", CategoryType.Expense,
                    new()
                    {
                        ["en"] = new("Hotels & Lodging", "Accommodation expenses"),
                        ["pt-BR"] = new("Hotéis & Hospedagem", "Despesas com hospedagem")
                    }),
                new("car_rentals", "🚗", "#80DEEA", CategoryType.Expense,
                    new()
                    {
                        ["en"] = new("Car Rentals", "Vehicle rental costs"),
                        ["pt-BR"] = new("Aluguel de Carros", "Custos de aluguel de veículos")
                    }),
                new("travel_food", "🍽️", "#B2EBF2", CategoryType.Expense,
                    new()
                    {
                        ["en"] = new("Travel Food", "Meals and dining while traveling"),
                        ["pt-BR"] = new("Alimentação em Viagem", "Refeições durante viagens")
                    }),
                new("activities_tours", "🗺️", "#E0F2F1", CategoryType.Expense,
                    new()
                    {
                        ["en"] = new("Activities & Tours", "Tourist activities and guided tours"),
                        ["pt-BR"] = new("Atividades & Passeios", "Atividades turísticas e passeios guiados")
                    }),
                new("travel_insurance", "🛡️", "#F1F8E9", CategoryType.Expense,
                    new()
                    {
                        ["en"] = new("Travel Insurance", "Trip and travel insurance"),
                        ["pt-BR"] = new("Seguro Viagem", "Seguro de viagem")
                    }),
                new("souvenirs", "🎁", "#E8F5E8", CategoryType.Expense,
                    new()
                    {
                        ["en"] = new("Souvenirs", "Travel mementos and gifts"),
                        ["pt-BR"] = new("Lembranças", "Souvenirs e presentes de viagem")
                    })
            }),

        // =================================================================
        // FINANCIAL SERVICES
        // =================================================================
        new("financial_services", "🏦", "#795548", CategoryType.Expense,
            new()
            {
                ["en"] = new("Financial Services", "Banking and financial service fees"),
                ["pt-BR"] = new("Serviços Financeiros", "Taxas de serviços bancários e financeiros")
            },
            new()
            {
                new("bank_fees", "💳", "#A1887F", CategoryType.Expense,
                    new()
                    {
                        ["en"] = new("Bank Fees", "Account maintenance and ATM fees"),
                        ["pt-BR"] = new("Taxas Bancárias", "Tarifas de manutenção de conta e caixas eletrônicos")
                    }),
                new("investment_fees", "📊", "#BCAAA4", CategoryType.Expense,
                    new()
                    {
                        ["en"] = new("Investment Fees", "Brokerage and investment costs"),
                        ["pt-BR"] = new("Taxas de Investimento", "Custos de corretagem e investimentos")
                    }),
                new("credit_card_fees", "💳", "#D7CCC8", CategoryType.Expense,
                    new()
                    {
                        ["en"] = new("Credit Card Fees", "Annual fees and interest charges"),
                        ["pt-BR"] = new("Taxas de Cartão", "Anuidade e encargos de juros")
                    }),
                new("tax_preparation", "📋", "#EFEBE9", CategoryType.Expense,
                    new()
                    {
                        ["en"] = new("Tax Preparation", "Tax filing and preparation services"),
                        ["pt-BR"] = new("Declaração de Impostos", "Serviços de declaração e preparação de impostos")
                    }),
                new("financial_planning", "📈", "#F5F5F5", CategoryType.Expense,
                    new()
                    {
                        ["en"] = new("Financial Planning", "Financial advisor and planning fees"),
                        ["pt-BR"] = new("Planejamento Financeiro", "Taxas de consultoria e planejamento financeiro")
                    }),
                new("insurance_premiums", "🛡️", "#FAFAFA", CategoryType.Expense,
                    new()
                    {
                        ["en"] = new("Insurance Premiums", "Life and other insurance payments"),
                        ["pt-BR"] = new("Prêmios de Seguro", "Pagamentos de seguro de vida e outros")
                    }),
                new("legal_services", "⚖️", "#ECEFF1", CategoryType.Expense,
                    new()
                    {
                        ["en"] = new("Legal Services", "Attorney and legal consultation fees"),
                        ["pt-BR"] = new("Serviços Jurídicos", "Honorários advocatícios e consultas jurídicas")
                    })
            }),

        // =================================================================
        // DEBT PAYMENTS
        // =================================================================
        new("debt_payments", "💳", "#F44336", CategoryType.Expense,
            new()
            {
                ["en"] = new("Debt Payments", "Debt service and loan payments"),
                ["pt-BR"] = new("Pagamento de Dívidas", "Pagamento de dívidas e empréstimos")
            },
            new()
            {
                new("credit_card_payments", "💳", "#EF5350", CategoryType.Expense,
                    new()
                    {
                        ["en"] = new("Credit Card Payments", "Credit card minimum and extra payments"),
                        ["pt-BR"] = new("Pagamento de Cartão", "Pagamentos mínimos e extras do cartão de crédito")
                    }),
                new("student_loans", "🎓", "#E57373", CategoryType.Expense,
                    new()
                    {
                        ["en"] = new("Student Loans", "Education loan payments"),
                        ["pt-BR"] = new("Empréstimo Estudantil", "Pagamentos de empréstimos educacionais")
                    }),
                new("personal_loans", "💰", "#EF9A9A", CategoryType.Expense,
                    new()
                    {
                        ["en"] = new("Personal Loans", "Personal and signature loan payments"),
                        ["pt-BR"] = new("Empréstimos Pessoais", "Pagamentos de empréstimos pessoais")
                    }),
                new("other_debt", "📋", "#FFCDD2", CategoryType.Expense,
                    new()
                    {
                        ["en"] = new("Other Debt", "Miscellaneous debt payments"),
                        ["pt-BR"] = new("Outras Dívidas", "Pagamentos de dívidas diversas")
                    })
            }),

        // =================================================================
        // TECHNOLOGY
        // =================================================================
        new("technology", "📱", "#607D8B", CategoryType.Expense,
            new()
            {
                ["en"] = new("Technology", "Technology and communication expenses"),
                ["pt-BR"] = new("Tecnologia", "Despesas com tecnologia e comunicação")
            },
            new()
            {
                new("phone_bill", "📞", "#78909C", CategoryType.Expense,
                    new()
                    {
                        ["en"] = new("Phone Bill", "Mobile and landline phone services"),
                        ["pt-BR"] = new("Conta de Telefone", "Serviços de telefone celular e fixo")
                    }),
                new("software_subscriptions", "💻", "#90A4AE", CategoryType.Expense,
                    new()
                    {
                        ["en"] = new("Software Subscriptions", "Apps and software licenses"),
                        ["pt-BR"] = new("Assinaturas de Software", "Aplicativos e licenças de software")
                    }),
                new("electronics", "🖥️", "#B0BEC5", CategoryType.Expense,
                    new()
                    {
                        ["en"] = new("Electronics", "Computers, phones, gadgets"),
                        ["pt-BR"] = new("Eletrônicos", "Computadores, celulares, gadgets")
                    }),
                new("cloud_storage", "☁️", "#CFD8DC", CategoryType.Expense,
                    new()
                    {
                        ["en"] = new("Cloud Storage", "Online storage and backup services"),
                        ["pt-BR"] = new("Armazenamento em Nuvem", "Serviços de armazenamento e backup online")
                    }),
                new("tech_support", "🔧", "#ECEFF1", CategoryType.Expense,
                    new()
                    {
                        ["en"] = new("Tech Support", "Computer repair and tech services"),
                        ["pt-BR"] = new("Suporte Técnico", "Reparos de computador e serviços de tecnologia")
                    }),
                new("domain_hosting", "🌐", "#F5F5F5", CategoryType.Expense,
                    new()
                    {
                        ["en"] = new("Domain & Hosting", "Website and domain costs"),
                        ["pt-BR"] = new("Domínio & Hospedagem", "Custos de website e domínio")
                    })
            }),

        // =================================================================
        // FAMILY & CHILDREN
        // =================================================================
        new("family_children", "👨‍👩‍👧‍👦", "#8BC34A", CategoryType.Expense,
            new()
            {
                ["en"] = new("Family & Children", "Family and childcare expenses"),
                ["pt-BR"] = new("Família & Filhos", "Despesas com família e cuidados infantis")
            },
            new()
            {
                new("childcare", "👶", "#9CCC65", CategoryType.Expense,
                    new()
                    {
                        ["en"] = new("Childcare", "Daycare and babysitting costs"),
                        ["pt-BR"] = new("Cuidados Infantis", "Custos de creche e babá")
                    }),
                new("school_supplies", "📚", "#AED581", CategoryType.Expense,
                    new()
                    {
                        ["en"] = new("School Supplies", "Educational materials and supplies"),
                        ["pt-BR"] = new("Material Escolar", "Materiais educacionais e suprimentos")
                    }),
                new("childrens_clothing", "👕", "#C5E1A5", CategoryType.Expense,
                    new()
                    {
                        ["en"] = new("Children's Clothing", "Kids' apparel and shoes"),
                        ["pt-BR"] = new("Roupas Infantis", "Vestuário e calçados infantis")
                    }),
                new("toys_games", "🧸", "#DCEDC8", CategoryType.Expense,
                    new()
                    {
                        ["en"] = new("Toys & Games", "Children's toys and entertainment"),
                        ["pt-BR"] = new("Brinquedos & Jogos", "Brinquedos e entretenimento infantil")
                    }),
                new("childrens_activities", "⚽", "#E6EE9C", CategoryType.Expense,
                    new()
                    {
                        ["en"] = new("Children's Activities", "Sports, lessons, and extracurriculars"),
                        ["pt-BR"] = new("Atividades Infantis", "Esportes, aulas e atividades extracurriculares")
                    }),
                new("baby_supplies", "🍼", "#F0F4C3", CategoryType.Expense,
                    new()
                    {
                        ["en"] = new("Baby Supplies", "Diapers, formula, baby care items"),
                        ["pt-BR"] = new("Produtos para Bebê", "Fraldas, fórmula, itens de cuidados para bebê")
                    }),
                new("pet_care", "🐕", "#F9FBE7", CategoryType.Expense,
                    new()
                    {
                        ["en"] = new("Pet Care", "Veterinary and pet supplies"),
                        ["pt-BR"] = new("Cuidados com Pets", "Veterinário e suprimentos para animais")
                    })
            }),

        // =================================================================
        // GIFTS & DONATIONS
        // =================================================================
        new("gifts_donations", "🎁", "#FF5722", CategoryType.Expense,
            new()
            {
                ["en"] = new("Gifts & Donations", "Gifts and charitable giving"),
                ["pt-BR"] = new("Presentes & Doações", "Presentes e contribuições beneficentes")
            },
            new()
            {
                new("birthday_gifts", "🎂", "#FF7043", CategoryType.Expense,
                    new()
                    {
                        ["en"] = new("Birthday Gifts", "Birthday presents and celebrations"),
                        ["pt-BR"] = new("Presentes de Aniversário", "Presentes e comemorações de aniversário")
                    }),
                new("holiday_gifts", "🎄", "#FF8A65", CategoryType.Expense,
                    new()
                    {
                        ["en"] = new("Holiday Gifts", "Christmas, holiday presents"),
                        ["pt-BR"] = new("Presentes de Natal", "Presentes de Natal e festas")
                    }),
                new("wedding_gifts", "💒", "#FFAB91", CategoryType.Expense,
                    new()
                    {
                        ["en"] = new("Wedding Gifts", "Wedding and special occasion gifts"),
                        ["pt-BR"] = new("Presentes de Casamento", "Presentes de casamento e ocasiões especiais")
                    }),
                new("charitable_donations", "❤️", "#FFCCBC", CategoryType.Expense,
                    new()
                    {
                        ["en"] = new("Charitable Donations", "Donations to charities and causes"),
                        ["pt-BR"] = new("Doações Beneficentes", "Doações para instituições de caridade")
                    }),
                new("religious_donations", "⛪", "#FBE9E7", CategoryType.Expense,
                    new()
                    {
                        ["en"] = new("Religious Donations", "Tithing and religious contributions"),
                        ["pt-BR"] = new("Doações Religiosas", "Dízimos e contribuições religiosas")
                    }),
                new("political_donations", "🗳️", "#FFCCBC", CategoryType.Expense,
                    new()
                    {
                        ["en"] = new("Political Donations", "Political campaign contributions"),
                        ["pt-BR"] = new("Doações Políticas", "Contribuições para campanhas políticas")
                    })
            }),

        // =================================================================
        // MISCELLANEOUS
        // =================================================================
        new("miscellaneous", "❓", "#9E9E9E", CategoryType.Expense,
            new()
            {
                ["en"] = new("Miscellaneous", "Miscellaneous and uncategorized expenses"),
                ["pt-BR"] = new("Diversos", "Despesas diversas e não categorizadas")
            },
            new()
            {
                new("atm_withdrawals", "🏧", "#BDBDBD", CategoryType.Expense,
                    new()
                    {
                        ["en"] = new("ATM Withdrawals", "Cash withdrawals and ATM fees"),
                        ["pt-BR"] = new("Saques em Caixa", "Saques em dinheiro e taxas de caixa eletrônico")
                    }),
                new("postage_shipping", "📮", "#E0E0E0", CategoryType.Expense,
                    new()
                    {
                        ["en"] = new("Postage & Shipping", "Mailing and shipping costs"),
                        ["pt-BR"] = new("Correios & Envios", "Custos de correios e envios")
                    }),
                new("office_supplies", "📎", "#EEEEEE", CategoryType.Expense,
                    new()
                    {
                        ["en"] = new("Office Supplies", "Work-related supplies and equipment"),
                        ["pt-BR"] = new("Material de Escritório", "Suprimentos e equipamentos de trabalho")
                    }),
                new("uncategorized", "🤷", "#F5F5F5", CategoryType.Expense,
                    new()
                    {
                        ["en"] = new("Uncategorized", "Expenses that need categorization"),
                        ["pt-BR"] = new("Sem Categoria", "Despesas que precisam de categorização")
                    })
            }),

        // =================================================================
        // TRANSFERS
        // =================================================================
        new("transfers", "🔄", "#607D8B", CategoryType.Transfer,
            new()
            {
                ["en"] = new("Transfers", "Money transfers between accounts"),
                ["pt-BR"] = new("Transferências", "Transferências de dinheiro entre contas")
            },
            new()
            {
                new("account_transfers", "↔️", "#78909C", CategoryType.Transfer,
                    new()
                    {
                        ["en"] = new("Account Transfers", "Transfers between your accounts"),
                        ["pt-BR"] = new("Transferências entre Contas", "Transferências entre suas contas")
                    }),
                new("savings_transfer", "🏦", "#90A4AE", CategoryType.Transfer,
                    new()
                    {
                        ["en"] = new("Savings Transfer", "Transfers to savings accounts"),
                        ["pt-BR"] = new("Transferência para Poupança", "Transferências para contas de poupança")
                    }),
                new("investment_transfer", "📈", "#B0BEC5", CategoryType.Transfer,
                    new()
                    {
                        ["en"] = new("Investment Transfer", "Transfers to investment accounts"),
                        ["pt-BR"] = new("Transferência para Investimentos", "Transferências para contas de investimento")
                    }),
                new("payment_to_others", "👥", "#CFD8DC", CategoryType.Transfer,
                    new()
                    {
                        ["en"] = new("Payment to Others", "Transfers to other people"),
                        ["pt-BR"] = new("Pagamento a Terceiros", "Transferências para outras pessoas")
                    })
            })
    };

    // =====================================================================
    // Category tree builder
    // =====================================================================

    /// <summary>
    /// Builds the full list of Category entities from the seed data for a given user and locale.
    /// </summary>
    private static List<Category> BuildCategoryTree(Guid userId, string locale)
    {
        var categories = new List<Category>();
        int sortOrder = 1;

        foreach (var parentSeed in SeedData)
        {
            var text = GetLocaleText(parentSeed, locale);

            var parent = new Category
            {
                Name = text.Name,
                Description = text.Description,
                Type = parentSeed.Type,
                UserId = userId,
                SortOrder = sortOrder++,
                Icon = parentSeed.Icon,
                Color = parentSeed.Color,
                CanonicalKey = parentSeed.CanonicalKey,
                IsSystemCategory = false,
                IsActive = true
            };
            categories.Add(parent);

            if (parentSeed.Children is not null)
            {
                foreach (var childSeed in parentSeed.Children)
                {
                    var childText = GetLocaleText(childSeed, locale);

                    categories.Add(new Category
                    {
                        Name = childText.Name,
                        Description = childText.Description,
                        Type = childSeed.Type,
                        UserId = userId,
                        ParentCategory = parent,
                        SortOrder = sortOrder++,
                        Icon = childSeed.Icon,
                        Color = childSeed.Color,
                        CanonicalKey = childSeed.CanonicalKey,
                        IsSystemCategory = false,
                        IsActive = true
                    });
                }
            }
        }

        return categories;
    }

    /// <summary>
    /// Resolves the locale-specific text for a seed entry, falling back to English.
    /// </summary>
    private static LocaleText GetLocaleText(CategorySeed seed, string locale)
    {
        return seed.Locales.TryGetValue(locale, out var text)
            ? text
            : seed.Locales["en"];
    }
}
