using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

public static class ExpressionTransformer
{
    private const string ItemValuePrefix = "itemValue('";
    private const string ResourcePrefix = "resource('";

    public static string RenderExpressions(string content, string baseUrl, Dictionary<string, object> data = null)
    {
        if (string.IsNullOrEmpty(content))
            return content;

        StringBuilder result = new StringBuilder(content.Length);
        int currentIndex = 0, contentLength = content.Length;

        while (currentIndex < contentLength)
        {
            int start = content.IndexOf("[%", currentIndex, StringComparison.Ordinal);
            int end = content.IndexOf("%]", start + 2, StringComparison.Ordinal);

            if (start == -1 || end == -1)
            {
                result.Append(content, currentIndex, contentLength - currentIndex);
                break;
            }

            result.Append(content, currentIndex, start - currentIndex);

            string expression = content.Substring(start + 2, end - start - 2);
            result.Append(ProcessExpression(expression, baseUrl, data));

            currentIndex = end + 2;
        }

        return result.ToString();
    }

    private static string ProcessExpression(string expression, string baseUrl, Dictionary<string, object> data)
    {
        if (IsValidExpression(expression, ItemValuePrefix.Length) && expression.EndsWith("')"))
            return GetValueOrDefault(data, expression.Substring(ItemValuePrefix.Length, expression.Length - ItemValuePrefix.Length - 2));

        if (IsValidExpression(expression, ResourcePrefix.Length) && expression.EndsWith("')"))
            return baseUrl + expression.Substring(ResourcePrefix.Length, expression.Length - ResourcePrefix.Length - 2);

        return string.Empty;
    }

    private static bool IsValidExpression(string expression, int prefixLength) =>
        expression.Length > (prefixLength + 2) && expression[0] == '\'' && expression[expression.Length - 1] == '%';

    private static string GetValueOrDefault(Dictionary<string, object> data, string field) =>
        data?.GetValueOrDefault(field)?.ToString() ?? string.Empty;
}

public class TemplateRenderer
{
    public void RenderTemplate(TextReader template, TextWriter output, Dictionary<string, Dictionary<string, object>[]> allData)
    {
        var document = new HtmlDocument();
        document.Load(template);

        var baseUrl = GetBaseUrl(allData);

        ProcessRepeaterNodes(document, baseUrl, allData);
        ProcessImageNodes(document, baseUrl);

        document.Save(output);
    }

    private void ProcessRepeaterNodes(HtmlDocument document, string baseUrl, Dictionary<string, Dictionary<string, object>[]> allData)
    {
        var repeaterNodes = document.DocumentNode.Descendants("sg:repeater").ToList();
        foreach (var repeaterNode in repeaterNodes)
        {
            var repeaterItemNodes = repeaterNode.Descendants("sg:repeateritem").ToList();
            foreach (var repeaterItemNode in repeaterItemNodes)
            {
                var dataSelection = repeaterNode.GetAttributeValue("dataselection", "");
                var repeaterItemContent = repeaterItemNode.InnerHtml;

                var repeatedContent = new StringBuilder();
                if (allData.TryGetValue(dataSelection, out var data))
                {
                    foreach (var dataItem in data)
                    {
                        var result = ExpressionTransformer.RenderExpressions(repeaterItemContent, baseUrl, dataItem);
                        repeatedContent.Append(result);
                    }
                }
                ReplaceHtml(repeaterNode, repeatedContent);
            }
        }
    }

    private void ProcessImageNodes(HtmlDocument document, string baseUrl)
    {
        var imageNodes = document.DocumentNode.Descendants("img").ToList();
        foreach (var imageNode in imageNodes)
        {
            var srcAttributeValue = imageNode.GetAttributeValue("src", "");
            var result = ExpressionTransformer.RenderExpressions(srcAttributeValue, baseUrl);
            imageNode.SetAttributeValue("src", result);
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
        var repeatedNodes = repeaterNode.ChildNodes.ToList();
        var parent = repeaterNode.ParentNode;
        
        foreach (var child in repeatedNodes)
        {
            parent.AppendChild(child);
        }
    }
}