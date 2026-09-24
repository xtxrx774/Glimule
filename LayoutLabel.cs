using System.Globalization;

namespace Glimule;

internal static class LayoutLabel
{
    private static readonly Dictionary<string, string> Native = new(StringComparer.OrdinalIgnoreCase)
    {
        ["EN"] = "A",
        ["RU"] = "РУ",
        ["UK"] = "УК",
        ["BE"] = "БЕ",
        ["BG"] = "БГ",
        ["SR"] = "СР",
        ["MK"] = "МК",
        ["MN"] = "МН",
        ["KK"] = "КК",
        ["KY"] = "КЫ",
        ["TG"] = "ТЖ",
        ["TT"] = "ТТ",
        ["ZH"] = "中",
        ["JA"] = "あ",
        ["KO"] = "한",
        ["AR"] = "ع",
        ["HE"] = "ע",
        ["YI"] = "יי",
        ["FA"] = "ف",
        ["UR"] = "ا",
        ["PS"] = "پ",
        ["SD"] = "س",
        ["UG"] = "ئ",
        ["TH"] = "ก",
        ["LO"] = "ລ",
        ["KM"] = "ខ",
        ["MY"] = "မ",
        ["HI"] = "हि",
        ["MR"] = "म",
        ["NE"] = "ने",
        ["SA"] = "सं",
        ["BN"] = "বা",
        ["PA"] = "ਪ",
        ["GU"] = "ગુ",
        ["OR"] = "ଓ",
        ["TA"] = "த",
        ["TE"] = "త",
        ["KN"] = "ಕ",
        ["ML"] = "മ",
        ["SI"] = "සි",
        ["AM"] = "አ",
        ["TI"] = "ት",
        ["EL"] = "ΕΛ",
        ["HY"] = "Հ",
        ["KA"] = "ქ",
        ["BO"] = "བོ",
        ["DZ"] = "རྫ",
        ["IU"] = "ᐃ",
        ["CHR"] = "Ꮳ",
    };

    public static string FromLangId(ushort langId)
    {
        if (langId == 0) return "?";
        try
        {
            var culture = CultureInfo.GetCultureInfo(langId);
            var iso = culture.TwoLetterISOLanguageName;
            if (string.IsNullOrEmpty(iso) || iso.Equals("iv", StringComparison.OrdinalIgnoreCase))
                iso = culture.ThreeLetterISOLanguageName;
            if (string.IsNullOrEmpty(iso))
                return langId.ToString("X");

            iso = iso.ToUpperInvariant();
            return Native.TryGetValue(iso, out var mark) ? mark : iso;
        }
        catch
        {
            return langId.ToString("X");
        }
    }
}
