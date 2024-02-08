namespace Hackathon2024
{
    using HtmlAgilityPack;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using Data = System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, object>[]>;


    public struct KnownExpressions
    {
        public const string ItemValue = "itemValue";
        public const string Resource = "resource";
    }

    /// <summary>
    /// ExpressionTransformer for Parsing Selligent specific expressions '[% %]'
    /// </summary>
    public class ExpressionTransformer
{
    private const string ExpressionPattern = @"\[%(?<expression>.*?)%\]";
    private const string ItemValueFieldPattern = @"itemValue\('(?<field>.*?)'\)";
    private const string ResourceFieldPattern = @"resource\('(?<resource>.*?)'\)";

    public static string RenderExpressions(string content, string baseUrl, Dictionary<string, object> data = null)
    {
        // Optimize regular expression compilation and string manipulation
        StringBuilder resultBuilder = new StringBuilder(content.Length);
        int prevIndex = 0;
        foreach (Match expressionMatch in Regex.Matches(content, ExpressionPattern))
        {
            resultBuilder.Append(content, prevIndex, expressionMatch.Index - prevIndex);
            prevIndex = expressionMatch.Index + expressionMatch.Length;

            string expression = expressionMatch.Groups["expression"].Value;
            expression = ReplaceItemValueFields(expression, data);
            expression = ReplaceResourceFields(expression, baseUrl);
            resultBuilder.Append(expression);
        }
        resultBuilder.Append(content, prevIndex, content.Length - prevIndex);

        return resultBuilder.ToString();
    }

    private static string ReplaceItemValueFields(string expression, Dictionary<string, object> data)
    {
        // Optimize item value field replacement
        int startIndex = 0;
        while (true)
        {
            int matchIndex = expression.IndexOf("itemValue('", startIndex);
            if (matchIndex == -1)
                break;

            int endIndex = expression.IndexOf("')", matchIndex + 11);
            if (endIndex == -1)
                break;

            string field = expression.Substring(matchIndex + 11, endIndex - matchIndex - 11);
            if (data != null && data.TryGetValue(field, out object value))
                expression = expression.Remove(matchIndex, endIndex - matchIndex + 2).Insert(matchIndex, value.ToString());

            startIndex = matchIndex + 1;
        }

        return expression;
    }

    private static string ReplaceResourceFields(string expression, string baseUrl)
    {
        // Optimize resource field replacement
        int startIndex = 0;
        while (true)
        {
            int matchIndex = expression.IndexOf("resource('", startIndex);
            if (matchIndex == -1)
                break;

            int endIndex = expression.IndexOf("')", matchIndex + 10);
            if (endIndex == -1)
                break;

            string resource = expression.Substring(matchIndex + 10, endIndex - matchIndex - 10);
            expression = expression.Remove(matchIndex, endIndex - matchIndex + 2).Insert(matchIndex, $"{baseUrl}{resource}");

            startIndex = matchIndex + 1;
        }

        return expression;
    }
}

    public class TemplateRenderer
    {
        public void RenderTemplate(TextReader template, TextWriter output, Data allData)
        {
            var document = new HtmlDocument();
            document.Load(template);

            var baseUrl = allData["variables"]
                .Where(x =>
                    x.TryGetValue("name", out object name) &&
                    "baseurl".Equals(name.ToString()))
                .Select(x =>
                    x["value"]?.ToString() ?? "")
                .FirstOrDefault();

            HtmlNode[] repeaterNodes = document.DocumentNode.SelectNodes("//*[name()='sg:repeater']")?.ToArray() ?? Array.Empty<HtmlNode>();
            foreach (var repeaterNode in repeaterNodes)
            {
                HtmlNode[] repeaterItemNodes = repeaterNode.SelectNodes("//*[name()='sg:repeateritem']")?.ToArray() ?? Array.Empty<HtmlNode>();

                foreach (var repeaterItemNode in repeaterItemNodes)
                {
                    var dataSelection = repeaterNode.Attributes["dataselection"].Value;
                    var repeaterItemContent = repeaterItemNode.InnerHtml;

                    var repeatedContent = new StringBuilder();
                    foreach (var dataItem in allData[dataSelection])
                    {
                        var result = ExpressionTransformer.RenderExpressions(repeaterItemContent, baseUrl, dataItem);

                        repeatedContent.Append(result);
                    }

                    ReplaceHtml(repeaterNode, repeatedContent);
                }
            }

            HtmlNode[] imageNodes = document.DocumentNode.SelectNodes("//img")?.ToArray() ?? Array.Empty<HtmlNode>();
            foreach (var imageNode in imageNodes)
            {
                var srcAttributeValue = imageNode.Attributes["src"].Value;

                var result = ExpressionTransformer.RenderExpressions(srcAttributeValue, baseUrl);

                imageNode.Attributes["src"].Value = result;
            }

            document.DocumentNode.WriteTo(output);
        }

        private static void ReplaceHtml(HtmlNode repeaterNode, StringBuilder repeatedContent)
        {
            repeaterNode.InnerHtml = repeatedContent.ToString();

            var repeatedNodes = repeaterNode.ChildNodes;

            var parent = repeaterNode.ParentNode;

            repeaterNode.Remove();

            foreach (var child in repeatedNodes)
            {
                parent.AppendChild(child);
            }
        }
    }
}

