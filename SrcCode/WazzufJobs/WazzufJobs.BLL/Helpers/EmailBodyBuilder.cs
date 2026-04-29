namespace WazzufJobs.BLL.Helpers;
public static class EmailBodyBuilder
{
    public static string GenerateEmailBody(string templete, Dictionary<string, string> templeteModel)
    {
        var templetePath = $"{Directory.GetCurrentDirectory()}/Templete/{templete}.html";

        var streamReader = new StreamReader(templetePath);
        var body = streamReader.ReadToEnd();
        streamReader.Close();

        foreach (var item in templeteModel)
            body = body.Replace(item.Key, item.Value);

        return body;
    }
}
