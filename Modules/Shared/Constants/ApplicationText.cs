namespace ExpenseTracker.Shared.Constants;

public static class ApplicationText
{
    public static class Configuration
    {
        public const string DefaultConnectionName = "DefaultConnection";
        public const string StorageSection = "Storage";
        public const string JwtSettingsSection = "JwtSettings";
        public const string EmailSection = "Email";
        public const string OpenAiApiKeyKey = "OpenAI:ApiKey";
        public const string OpenAiModelKey = "OpenAI:Model";
        public const string OpenAiResponsesEndpointKey = "OpenAI:ResponsesEndpoint";
        public const string AzureAiEndpointKey = "AzureAI:Endpoint";
        public const string AzureAiKeyKey = "AzureAI:Key";
    }

    public static class Policies
    {
        public const string AllowLocalhost = "AllowLocalhost";

        public static readonly string[] AllowedOrigins =
        [
            "http://localhost:4200",
            "https://localhost:4200"
        ];
    }

    public static class Swagger
    {
        public const string Endpoint = "/swagger/v1/swagger.json";
        public const string Title = "ExpenseTracker API v1";
    }

    public static class Storage
    {
        public const string RootFolder = "storage";
        public const string AvatarsFolder = "avatars";
        public const string ReceiptsFolder = "receipts";
        public const string NotificationPreviewFolder = "notification-previews";
        public const string AvatarRequestPath = "/avatars";
        public const string EmptyJsonObject = "{}";
        public const string QuickAddFilePrefix = "quick-add";
        public const string QuickAddSourceJson = "{\"source\":\"quick-add\"}";
    }

    public static class Defaults
    {
        public const string UnknownVendor = "Unknown";
        public const string GeneralCategory = "General";
        public const string UncategorizedCategory = "Uncategorized";
        public const string UsdCurrency = "USD";
        public const string NotAvailable = "N/A";
    }

    public static class CacheKeys
    {
        public const string OtpPrefix = "otp_";
        public const string ResetPrefix = "reset_";
    }

    public static class DeliveryModes
    {
        public const string Email = "email";
        public const string Development = "development";
        public const string Smtp = "smtp";
        public const string FilePreview = "file-preview";
    }

    public static class Severity
    {
        public const string Info = "info";
        public const string Positive = "positive";
        public const string Warning = "warning";
        public const string Critical = "critical";
    }

    public static class Auth
    {
        public const int MinimumPasswordLength = 6;
        public const int OtpCodeMinValue = 100000;
        public const int OtpCodeMaxValueExclusive = 1000000;
        public const int OtpExpiryMinutes = 10;
        public const int ResetCodeExpiryMinutes = 15;

        public const string EnterValidEmailAddress = "Enter a valid email address.";
        public const string EmailAlreadyInUse = "Email already in use";
        public const string InvalidCredentials = "Invalid credentials";
        public const string PasswordRequired = "Password is required.";
        public const string InvalidOrExpiredOtp = "Invalid or expired OTP.";
        public const string OtpSentToEmail = "OTP sent to your email.";
        public const string SmtpNotConfiguredOtpDevelopment = "SMTP is not configured, so the development OTP is returned below.";
        public const string OtpGeneratedWithoutEmail = "OTP generated, but email delivery is not configured on the server.";
        public const string IfEmailExistsResetCodeSent = "If that email exists, a reset code has been sent.";
        public const string SmtpNotConfiguredResetDevelopment = "SMTP not configured. Development reset code returned.";
        public const string PasswordMinimumLength = "Password must be at least 6 characters.";
        public const string InvalidOrExpiredResetCode = "Invalid or expired reset code.";
        public const string PasswordUpdatedSuccessfully = "Password updated successfully. You can now log in.";
        public const string UserNotFound = "User not found.";
        public const string AdminAlreadyExists = "An admin account already exists. Ask an admin to grant access.";
    }

    public static class Security
    {
        public const string AuthenticationRequired = "Authentication is required for this resource.";
        public const string AuthenticatedUserContextUnavailable = "Authenticated user context is not available.";
        public const string AccessDenied = "You do not have permission to access this resource.";
    }

    public static class Receipts
    {
        public const string SelectAtLeastOneReceipt = "Select at least one receipt.";
        public const string UpdatedCategoriesTemplate = "Updated {0} receipt categor{1}.";
        public const string DeletedReceiptsTemplate = "Deleted {0} receipt{1}.";
        public const string AppliedVendorRulesTemplate = "Applied vendor rules to {0} receipt{1}.";
        public const string DuplicateFlagTemplate = "{0} {1} duplicate flag{2}.";
    }

    public static class AdminUsers
    {
        public const string SelectAtLeastOneUser = "Select at least one user to delete.";
        public const string CannotDeleteOwnAccount = "Use another admin account to delete your own account.";
        public const string NoMatchingUsers = "No matching users were found.";
        public const string AtLeastOneAdminMustRemain = "At least one admin account must remain in the workspace.";
    }

    public static class Email
    {
        public const string FromName = "AI Expense Tracker";
        public const string OtpSubject = "Your AI Expense Tracker OTP";
        public const string OtpBodyTemplate = "Your OTP is {0}. It will expire in 10 minutes.";
        public const string PasswordResetSubject = "Password Reset - AI Expense Tracker";
        public const string PasswordResetBodyTemplate = "Your password reset code is: {0}\n\nIt will expire in 15 minutes.\n\nIf you did not request this, you can safely ignore this email.";
    }

    public static class Digests
    {
        public const string WeeklySummaryType = "weekly-summary";
        public const string MonthlyReportType = "monthly-report";
        public const string ChooseDigestType = "Choose either weekly-summary or monthly-report.";
        public const string UserProfileNotFound = "User profile was not found.";
        public const string UnsupportedDigestType = "Unsupported digest type.";
        public const string SmtpDeliveryFailed = "SMTP delivery failed for this digest.";
        public const string WeeklySummaryLabel = "weekly summary";
        public const string MonthlyReportLabel = "monthly AI report";
    }

    public static class Ai
    {
        public const string DefaultResponsesModel = "gpt-5-mini";

        public const string ReceiptParserInstructions = """
You are a smart global expense receipt parser. Extract structured expense data from informal, conversational, or typoed text in any language or country.

CATEGORIES - pick the single best match using common sense and context:

Daily spending:
- Food & Dining: restaurants, cafes, dhabas, bars, food courts, hotel meals, takeaway, dine-in
- Snacks & Beverages: chocolates, chips, candy, street food, juice, chai, coffee, ice cream, biscuits, namkeen
- Groceries: vegetables, fruits, dairy, rice, dal, atta, supermarkets, kirana store, corner shop
- Food Delivery: Swiggy, Zomato, Uber Eats, DoorDash, Deliveroo, Grab Food, FoodPanda, Talabat, online food order

Getting around:
- Transport: taxi, auto, rickshaw, Ola, Uber, Lyft, Grab, Rapido, bus, metro, train, ferry, toll, parking, petrol, diesel, fuel, EV charging, bike rental
- Travel & Trips: flights, hotels, Airbnb, hostels, vacation, tours, sightseeing, travel insurance, visa fees, holiday packages

Home & living:
- Rent & Housing: house rent, room rent, PG, apartment, office rent, hostel rent, society charges, maintenance, lease, security deposit
- Utilities & Bills: electricity, water, gas, internet, mobile recharge, broadband, DTH, cable TV, wifi, phone bill
- Home & Furniture: furniture, appliances, home decor, home repair, plumber, electrician, carpenter, painting, AC service, pest control, IKEA
- Pets: pet food, vet, grooming, pet supplies, pet medicine, boarding, kennel

Health:
- Healthcare & Medicine: pharmacy, doctor, hospital, clinic, lab test, dental, optician, health checkup, ambulance, surgery, consultation
- Fitness & Wellness: gym, yoga, salon, spa, haircut, massage, sports equipment, swimming pool, fitness classes, dietitian

Shopping:
- Shopping & Clothing: clothes, shoes, bags, accessories, gifts, Amazon, Flipkart, Myntra, ASOS, Zara, H&M, department stores, mall
- Electronics & Gadgets: phone, laptop, headphones, charger, cables, Apple, Samsung, OnePlus, repair, screen replacement, tech accessories

Education & Kids:
- Education: tuition, school fees, college fees, books, stationery, coaching, Udemy, Coursera, exam fees, uniforms, online courses
- Kids & Childcare: daycare, babysitter, toys, diapers, baby food, school supplies, kids clothes, playground, nursery fees

Entertainment & Subscriptions:
- Entertainment: movies, concerts, events, amusement parks, gaming, zoo, museum, sports match, bowling
- Subscriptions: Netflix, Spotify, Disney+, Hotstar, YouTube Premium, Apple One, LinkedIn, iCloud, Google One, Xbox Game Pass, Amazon Prime, Audible

Finance:
- Insurance: life insurance, health insurance, vehicle insurance, home insurance, travel insurance, term plan, premium payment
- Investments & Savings: SIP, mutual fund, stocks, FD, RD, PPF, NPS, crypto, trading
- EMI & Loan: home loan EMI, car loan, personal loan, credit card payment, BNPL, Afterpay, Klarna
- Taxes & Fees: income tax, property tax, GST, council tax, government fees, passport, driving license, registration fee

Social & Giving:
- Gifts & Occasions: birthday gifts, wedding gift, anniversary, festival spending, flowers, greeting cards, celebration
- Charity & Donations: NGO donation, temple or church or mosque offering, crowdfunding, zakat, tithe, relief funds, charity

Work:
- Business & Work Expenses: office supplies, client meals, co-working space, work travel, courier, printing, SaaS tools
- Personal Services: laundry, dry cleaning, tailor, cobbler, domestic help salary, maid, cook, watchman, driver salary, ironing
- General: last resort only - use when truly no other category fits

VENDOR RULES - be smart, never literal:
- Named brand, shop, or app -> use exactly that name
- Rent or housing -> "Landlord" or the person or company name if given
- EMI or loan -> bank name if given
- Utility bill -> provider name if mentioned, else "Utility provider"
- Domestic help, maid, or driver salary -> use their name if given, else "Domestic help"
- Person-to-person -> use the person's name
- Street food or unnamed local -> "Street stall" or "Local shop"
- Government payment -> "Government" or department name
- Use "Unknown" only if there is truly zero vendor information

AMOUNT:
- Numeric value only, no currency symbols
- Handle examples like "$20", "EUR 15.50", "GBP 8", "JPY 500", "INR 200", "Rs 50", "20 dollars", "twenty euros", or "15,99"
- Convert word numbers such as "twenty thousand" -> 20000 and "five hundred" -> 500
- If multiple amounts exist, pick the final or total amount

CURRENCY - ISO 4217 code:
- "$" usually means USD unless another dollar currency is clearly implied
- Map rupees -> INR, dollars -> USD, euros -> EUR, pounds -> GBP, dirhams -> AED, ringgit -> MYR, baht -> THB, won -> KRW, yuan or RMB -> CNY
- Default to USD only when the currency is truly unclear

DATE:
- Resolve relative dates like "yesterday", "last Monday", and "2 days ago" from today's date below
- Handle absolute formats like "5th May", "03/15", "March 15", and "15-03"
- Default to today if no date is mentioned

Return only valid JSON, with no explanation and no markdown:
{"vendor":"...","amount":0.00,"currency":"USD","category":"...","date":"yyyy-MM-dd","parsed":true,"rawText":"..."}
""";
    }
}
