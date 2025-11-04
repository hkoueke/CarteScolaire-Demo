using System.ComponentModel.DataAnnotations;

namespace CarteScolaire.DataImpl.Services.HtmlParsers;

/// <summary>
/// Defines the CSS selectors used to extract specific data fields 
/// from a parsed HTML document, typically representing student or result information.
/// </summary>
internal sealed class SelectorOptions
{
    /// <summary>
    /// Gets or sets the primary CSS selector used to identify the container element 
    /// for a single result or student record within the document.
    /// </summary>
    /// <remarks>
    /// This value must be non-empty (MinLength 1) and up to 50 characters long.
    /// </remarks>
    [Required, MinLength(1), MaxLength(50)]
    public string ResultSelector { get; set; } = null!;

    /// <summary>
    /// Gets or sets the CSS selector for extracting the unique registration or 
    /// identification number of the student.
    /// </summary>
    /// <remarks>
    /// This value must be non-empty (MinLength 1) and up to 50 characters long.
    /// </remarks>
    [Required, MinLength(1), MaxLength(50)]
    public string RegistrationIdSelector { get; set; } = null!;

    /// <summary>
    /// Gets or sets the CSS selector for extracting the student's full name.
    /// </summary>
    /// <remarks>
    /// This value must be non-empty (MinLength 1) and up to 50 characters long.
    /// </remarks>
    [Required, MinLength(1), MaxLength(50)]
    public string NameSelector { get; set; } = null!;

    /// <summary>
    /// Gets or sets the CSS selector for extracting the student's date of birth or enrollment year information.
    /// </summary>
    /// <remarks>
    /// This value must be non-empty (MinLength 1) and up to 50 characters long.
    /// </remarks>
    [Required, MinLength(1), MaxLength(50)]
    public string DateOfBirthSelector { get; set; } = null!;

    /// <summary>
    /// Gets or sets the CSS selector for extracting the name of the school or institution.
    /// </summary>
    /// <remarks>
    /// This value must be non-empty (MinLength 1) and up to 50 characters long.
    /// </remarks>
    [Required, MinLength(1), MaxLength(50)]
    public string SchoolNameSelector { get; set; } = null!;

    /// <summary>
    /// Gets or sets the CSS selector for extracting the academic grade or class level of the student.
    /// </summary>
    /// <remarks>
    /// This value must be non-empty (MinLength 1) and up to 50 characters long.
    /// </remarks>
    [Required, MinLength(1), MaxLength(50)]
    public string GradeSelector { get; set; } = null!;

    /// <summary>
    /// Gets or sets the CSS selector for extracting the student's gender information.
    /// </summary>
    /// <remarks>
    /// This value must be non-empty (MinLength 1) and up to 50 characters long.
    /// </remarks>
    [Required, MinLength(1), MaxLength(50)]
    public string GenderSelector { get; set; } = null!;
}
