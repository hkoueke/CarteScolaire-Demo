namespace CarteScolaire.Data.Responses;

/// <summary>
/// A simple enum representing available genders
/// </summary>
public enum Gender
{
    Male, Female, Unspecified
}

public static class GenderExtensions
{
    public static Gender ToGender(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? Gender.Unspecified
            : value.ToLowerInvariant() switch
            {
                "m" => Gender.Male,
                "f" => Gender.Female,
                _ => Gender.Unspecified
            };
    }
}

