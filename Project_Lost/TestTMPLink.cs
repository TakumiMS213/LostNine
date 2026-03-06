using System;
using System.Text.RegularExpressions;

public class TestTMPLink {
    public static void Main() {
        string text = "<link=\"dummy_despair\">絶望</link>";
        string id = "dummy_despair";
        string pattern = $@"<(?:a\s+href|link)\s*=\s*""{Regex.Escape(id)}""\s*>(.*?)</(?:a|link)>";
        Console.WriteLine($"Regex Match: {Regex.IsMatch(text, pattern)}");
        Console.WriteLine($"Extracted Text: {Regex.Match(text, pattern).Groups[1].Value}");

        string pattern2 = $@"<(?:a\s+href|link)\s*=\s*""(.*?)""\s*>";
        Console.WriteLine($"Extracted ID: {Regex.Match(text, pattern2).Groups[1].Value}");
    }
}
