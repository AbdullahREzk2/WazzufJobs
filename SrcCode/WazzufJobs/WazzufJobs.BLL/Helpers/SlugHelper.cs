using System.Text.RegularExpressions;

namespace WazzufJobs.BLL.Helpers;
public static class SlugHelper
{
    public static string Generate(string name)
    {
        var slug = name.ToLowerInvariant();
        slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");
        slug = Regex.Replace(slug, @"\s+", "-");
        slug = slug.Trim('-');
        return slug;
    }
}
