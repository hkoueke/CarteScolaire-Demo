namespace CarteScolaire.DataImpl.Services.TokenProviders;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// Configuration options for the TokenProvider service, specifying the endpoint 
/// and CSS selector used for CSRF token extraction.
/// </summary>
public class TokenProviderOptions
{
    /// <summary>
    /// Configuration section name used for binding, typically the name of this class.
    /// </summary>
    public const string SectionName = nameof(TokenProviderOptions);

    /// <summary>
    /// Gets or sets the relative path on the site to fetch for the token.
    /// </summary>
    /// <remarks>
    /// This value must be non-empty (MinLength 1) and up to 50 characters long.
    /// </remarks>
    [Required, MinLength(1), MaxLength(50)]
    public string TokenEndpointPath { get; set; } = null!;

    /// <summary>
    /// Gets or sets the CSS selector used to find the hidden input element containing the token.
    /// </summary>
    /// <remarks>
    /// This value must be non-empty (MinLength 1) and up to 50 characters long.
    /// </remarks>
    [Required, MinLength(1), MaxLength(50)]
    public string TokenSelector { get; set; } = null!;
}