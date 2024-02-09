using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

public static class ExpressionTransformer
{
    private static readonly Regex ExpressionPattern = new Regex(@"\[%(.+?)%\]", RegexOptions.Compiled);

    public static string RenderExpressions(string content, string baseUrl, Dictionary<string, object> data = null)
    {
        if (string.IsNullOrEmpty(content))
            return content;

        var matches = ExpressionPattern.Matches(content);
        var result = new StringBuilder(content.Length);
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
        const string ResourcePrefix = "resource('";

        if (expression.StartsWith(ItemValuePrefix) && expression.EndsWith("')"))
        {
            string field = expression.Substring(ItemValuePrefix.Length, expression.Length - ItemValuePrefix.Length - 2);
            return data?.GetValueOrDefault(field)?.ToString() ?? "";
        }
        else if (expression.StartsWith(ResourcePrefix) && expression.EndsWith("')"))
        {
            string resource = expression.Substring(ResourcePrefix.Length, expression.Length - ResourcePrefix.Length - 2);
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
        repeaterNode.Remove();

        foreach (var child in repeatedNodes)
        {
            parent.AppendChild(child);
        }
    }
}