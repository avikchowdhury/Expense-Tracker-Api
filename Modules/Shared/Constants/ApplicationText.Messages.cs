namespace ExpenseTracker.Shared.Constants;

public static partial class ApplicationText
{
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
}
