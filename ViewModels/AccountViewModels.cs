using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.ViewModels;

/// <summary>Fields accepted by the sign-in form.</summary>
public sealed class LoginViewModel
{
    [Required(ErrorMessage = "Enter your work email address.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [StringLength(256)]
    [Display(Name = "Work email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter your password.")]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Keep me signed in")]
    public bool RememberMe { get; set; }

    /// <summary>Preserved only when it is verified as local by the controller.</summary>
    public string? ReturnUrl { get; set; }
}

/// <summary>Fields required to create a standard sales-executive account.</summary>
public sealed class RegisterViewModel
{
    [Required(ErrorMessage = "Enter your first name.")]
    [StringLength(100, MinimumLength = 1)]
    [Display(Name = "First name")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter your last name.")]
    [StringLength(100, MinimumLength = 1)]
    [Display(Name = "Last name")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter your work email address.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [StringLength(256)]
    [Display(Name = "Work email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Create a password.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Your password must be between {2} and {1} characters.")]
    [RegularExpression(
        @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^a-zA-Z0-9]).{8,100}$",
        ErrorMessage = "Use an uppercase letter, lowercase letter, number, and symbol.")]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirm your password.")]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "The passwords do not match.")]
    [Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}

/// <summary>Fields required to rotate the current user's password.</summary>
public sealed class ChangePasswordViewModel
{
    [Required(ErrorMessage = "Enter your current password.")]
    [DataType(DataType.Password)]
    [Display(Name = "Current password")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter a new password.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Your new password must be between {2} and {1} characters.")]
    [RegularExpression(
        @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^a-zA-Z0-9]).{8,100}$",
        ErrorMessage = "Use an uppercase letter, lowercase letter, number, and symbol.")]
    [DataType(DataType.Password)]
    [Display(Name = "New password")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirm your new password.")]
    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "The new passwords do not match.")]
    [Display(Name = "Confirm new password")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
