using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

public static class ExpressionTransformer
{
    private static readonly Regex ExpressionPattern = new Regex(@"\[%(.+?)%\]", RegexOptions.Compiled);

    public static string RenderExpressions(string content, string baseUrl, Dictionary<string, object> data = null)
    {
        if (string.IsNullOrEmpty(content))
            return content;

        MatchCollection matches = ExpressionPattern.Matches(content);
        StringBuilder result = new StringBuilder(content.Length);
        int lastIndex = 0;

        foreach (Match match in matches)
        {
            result.Append(content, lastIndex, match.Index - lastIndex);
            string expression = match.Groups[1].Value;
            string processedExpression = ProcessExpression(expression, baseUrl, data);
            result.Append(processedExpression);
            lastIndex = match.Index + match.Length;
        }

        result.Append(content, lastIndex, content.Length - lastIndex);
        return result.ToString();
    }


    private static string ProcessExpression(string expression, string baseUrl, Dictionary<string, object> data)
    {
        const string ItemValuePrefix = "itemValue('";
        const string ItemValueSuffix = "')";
        const string ResourcePrefix = "resource('";
        const string ResourceSuffix = "')";

        if (expression.StartsWith(ItemValuePrefix) && expression.EndsWith(ItemValueSuffix))
        {
            string field = expression.Substring(ItemValuePrefix.Length, expression.Length - ItemValuePrefix.Length - ItemValueSuffix.Length);
            if (data != null && data.TryGetValue(field, out object value))
            {
                return value?.ToString() ?? "";
            }
            return "";
        }
        else if (expression.StartsWith(ResourcePrefix) && expression.EndsWith(ResourceSuffix))
        {
            string resource = expression.Substring(ResourcePrefix.Length, expression.Length - ResourcePrefix.Length - ResourceSuffix.Length);
            return baseUrl + resource;
        }

        return "";
    }
}

public class TemplateRenderer
{
    public void RenderTemplate(TextReader template, TextWriter output, Dictionary<string, Dictionary<string, object>[]> allData)
    {
        var document = new HtmlDocument();
        document.Load(template);

        var baseUrl = GetBaseUrl(allData);

        RenderRepeaterItems(document, baseUrl, allData);

        RenderImageSrcAttributes(document, baseUrl);

        document.Save(output);
    }

    private void RenderRepeaterItems(HtmlDocument document, string baseUrl, Dictionary<string, Dictionary<string, object>[]> allData)
    {
        var repeaterNodes = document.DocumentNode.SelectNodes("//*[name()='sg:repeater']");
        if (repeaterNodes != null)
        {
            foreach (var repeaterNode in repeaterNodes)
            {
                var dataSelection = repeaterNode.GetAttributeValue("dataselection", "");
                var repeaterItemNodes = repeaterNode.SelectNodes(".//*[name()='sg:repeateritem']");
                if (repeaterItemNodes != null && allData.TryGetValue(dataSelection, out var data))
                {
                    foreach (var repeaterItemNode in repeaterItemNodes)
                    {
                        var repeaterItemContent = repeaterItemNode.InnerHtml;
                        var repeatedContent = new StringBuilder();
                        foreach (var dataItem in data)
                        {
                            var result = ExpressionTransformer.RenderExpressions(repeaterItemContent, baseUrl, dataItem);
                            repeatedContent.Append(result);
                        }
                        ReplaceHtml(repeaterNode, repeatedContent);
                    }
                }
            }
        }
    }

    private void RenderImageSrcAttributes(HtmlDocument document, string baseUrl)
    {
        var imageNodes = document.DocumentNode.SelectNodes("//img");
        if (imageNodes != null)
        {
            foreach (var imageNode in imageNodes)
            {
                var srcAttributeValue = imageNode.GetAttributeValue("src", "");
                var result = ExpressionTransformer.RenderExpressions(srcAttributeValue, baseUrl);
                imageNode.SetAttributeValue("src", result);
            }
        }
    }

    private string GetBaseUrl(Dictionary<string, Dictionary<string, object>[]> allData)
    {
        if (allData.TryGetValue("variables", out var variables))
        {
            foreach (var variable in variables)
            {
                if (variable.TryGetValue("name", out var name) && "baseurl".Equals(name?.ToString(), StringComparison.OrdinalIgnoreCase) && variable.TryGetValue("value", out var value))
                {
                    return value?.ToString() ?? "";
                }
            }
        }
        return "";
    }

    private void ReplaceHtml(HtmlNode repeaterNode, StringBuilder repeatedContent)
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
