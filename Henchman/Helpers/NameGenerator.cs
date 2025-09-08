using System.Linq;
using System.Reflection;
using Lumina.Excel.Sheets;
using Lumina.Text.ReadOnly;

namespace Henchman.Helpers;

internal static class NameGenerator
{
    private static readonly Random Rand = new();

    private static readonly string[] Prefix =
    [
            "A", "Ad", "Al", "Am", "An", "Ar", "Be", "Bl", "Br", "Ch", "Cl", "De", "Ed", "El", "Em", "Fa", "Fl", "Fr", "Gr", "Jo", "Ki", "La", "Ma", "Na", "O",
            "Ol", "Or", "Pa", "Re", "Sh", "Si", "Ta", "Th", "Tr", "Va"
    ];

    private static readonly string[] Middle =
            ["ad", "al", "am", "an", "ar", "as", "at", "el", "en", "er", "es", "et", "ei", "il", "in", "ir", "ol", "on", "or", "os", "ur"];

    private static readonly string[] FeminineSuffix =
    [
            "a", "ina", "ie", "ara", "eera", "aea", "osa", "ya", "tha", "aya", "ana", "ielle", "ia", "ora", "iss", "ea", "ene", "ice", "ra", "ka", "nira",
            "wen", "elle", "lina", "wyn", "lora", "vina", "yn"
    ];

    private static readonly string[] MasculineSuffix =
            ["an", "nik", "anar", "ton", "or", "ant", "er", "dir", "ius", "ric", "no", "ien", "ard", "len", "ian", "en", "o", "as", "on", "rus", "us"];

    private static readonly List<PropertyInfo> RaceLastNames = typeof(CharaMakeName)
                                                              .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                                              .Where(p => p.Name.EndsWith("LastName")  &&
                                                                          !p.Name.Contains("Lalafell") &&
                                                                          !p.Name.Contains("SeaWolf")  &&
                                                                          !p.Name.Contains("Viera"))
                                                              .ToList();

    private static readonly List<PropertyInfo> FemaleFirstNames = typeof(CharaMakeName)
                                                              .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                                              .Where(p => p.Name.EndsWith("Female")  &&
                                                                          !p.Name.Contains("Lalafell") &&
                                                                          !p.Name.Contains("SeaWolf")  &&
                                                                          !p.Name.Contains("Viera"))
                                                              .ToList();

    private static readonly List<PropertyInfo> MaleFirstNames = typeof(CharaMakeName)
                                                                 .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                                                 .Where(p => p.Name.EndsWith("Male")    &&
                                                                             !p.Name.Contains("Lalafell") &&
                                                                             !p.Name.Contains("SeaWolf")  &&
                                                                             !p.Name.Contains("Viera"))
                                                                 .ToList();

    internal static string                              GenerateFeminineName      => GeneratePrefixMiddle(1, 3) + FeminineSuffix[Rand.Next(FeminineSuffix.Length)];
    internal static string                              GenerateMasculineName     => GeneratePrefixMiddle(1, 3) + MasculineSuffix[Rand.Next(MasculineSuffix.Length)];
    internal static (string FirstName, string LastName) GetFullMasculineName => GenerateFullName(GenerateMasculineName);
    internal static (string FirstName, string LastName) GetFullFeminineName  => GenerateFullName(GenerateFeminineName);

    internal static (string FirstName, string LastName) GenerateFullName(string firstName)
    {
        var    firstNameLength        = firstName.Length;
        var    possibleLastNameLength = 20 - firstNameLength;
        string lastName;
        do
        {
            lastName = GetLastName();
        }
        while (lastName.Length > possibleLastNameLength);

        return (firstName, lastName);
    }

    internal static string GetFirstName(string lastName, bool male)
    {
        var    lastNameLength        = lastName.Length;
        var    possibleFirstNameLength = 20 - lastNameLength;
        string firstName;
        do
        {
            firstName = male
                                ? GetMasculineName()
                                : GetFeminineName();
        }
        while (firstName.Length > possibleFirstNameLength);

        return firstName;
    }

    internal static string GetFeminineName()
    {
        var nameSheet     = Svc.Data.GetExcelSheet<CharaMakeName>();
        var nameRowAmount = nameSheet.Count;

        var    randomLastNameColumn = FemaleFirstNames[Rand.Next(FemaleFirstNames.Count)];
        string firstName;

        do
        {
            var lastNameObject = randomLastNameColumn.GetValue(nameSheet.GetRow((uint)Rand.Next(nameRowAmount)));
            firstName = ((ReadOnlySeString)lastNameObject!).ExtractText();
        }
        while (string.IsNullOrEmpty(firstName) || firstName == "Ilcum");

        return firstName;
    }

    internal static string GetMasculineName()
    {
        var nameSheet     = Svc.Data.GetExcelSheet<CharaMakeName>();
        var nameRowAmount = nameSheet.Count;

        var    randomLastNameColumn = MaleFirstNames[Rand.Next(MaleFirstNames.Count)];
        string firstName;

        do
        {
            var lastNameObject = randomLastNameColumn.GetValue(nameSheet.GetRow((uint)Rand.Next(nameRowAmount)));
            firstName = ((ReadOnlySeString)lastNameObject!).ExtractText();
        }
        while (string.IsNullOrEmpty(firstName));

        return firstName;
    }

    internal static string GetLastName()
    {
        var nameSheet     = Svc.Data.GetExcelSheet<CharaMakeName>();
        var nameRowAmount = nameSheet.Count;

        var    randomLastNameColumn = RaceLastNames[Rand.Next(RaceLastNames.Count)];
        string lastName;

        do
        {
            var lastNameObject = randomLastNameColumn.GetValue(nameSheet.GetRow((uint)Rand.Next(nameRowAmount)));
            lastName = ((ReadOnlySeString)lastNameObject!).ExtractText();
        }
        while (string.IsNullOrEmpty(lastName));

        return lastName;
    }

    internal static string GetLastName(string firstName)
    {
        var    firstNameLength         = firstName.Length;
        var    possibleLastNameLength = 20 - firstNameLength;
        var    nameSheet               = Svc.Data.GetExcelSheet<CharaMakeName>();
        var    nameRowAmount           = nameSheet.Count;
        var    randomLastNameColumn    = RaceLastNames[Rand.Next(RaceLastNames.Count)];
        string lastName;
        do
        {
            var lastNameObject = randomLastNameColumn.GetValue(nameSheet.GetRow((uint)Rand.Next(nameRowAmount)));
            lastName = ((ReadOnlySeString)lastNameObject!).ExtractText();
        }
        while (string.IsNullOrEmpty(lastName) && lastName.Length > possibleLastNameLength);
        
        return lastName;
    }


    private static string GeneratePrefixMiddle(int min, int max)
    {
        var middleAmount = Rand.Next(min, max);
        var name         = Prefix[Rand.Next(Prefix.Length)];
        for (var i = 0; i < middleAmount; i++)
            name += Middle[Rand.Next(Middle.Length)];
        return name;
    }
}
