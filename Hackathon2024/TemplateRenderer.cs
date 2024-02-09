using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

public static class ExpressionTransformer
{
    public static string RenderExpressions(string content, string baseUrl, Dictionary<string, object> data = null)
    {
        if (string.IsNullOrEmpty(content))
            return content;

        StringBuilder result = new StringBuilder(content.Length);
        int currentIndex = 0;

        while (currentIndex < content.Length)
        {
            int expressionStartIndex = content.IndexOf("[%", currentIndex);
            if (expressionStartIndex == -1)
            {
                result.Append(content, currentIndex, content.Length - currentIndex);
                break;
            }

            result.Append(content, currentIndex, expressionStartIndex - currentIndex);

            int expressionEndIndex = content.IndexOf("%]", expressionStartIndex + 2);
            if (expressionEndIndex == -1)
            {
                result.Append(content, expressionStartIndex, content.Length - expressionStartIndex);
                break;
            }

            string expression = content.Substring(expressionStartIndex + 2, expressionEndIndex - expressionStartIndex - 2);
            string processedExpression = ProcessExpression(expression, baseUrl, data);

            result.Append(processedExpression);

            currentIndex = expressionEndIndex + 2;
        }

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
            return data != null && data.TryGetValue(field, out object value) ? value?.ToString() ?? "" : "";
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
        var parser = new HtmlParser();
        var document = parser.ParseDocument(template.ReadToEnd());

        var baseUrl = GetBaseUrl(allData);

        ProcessRepeaterNodes(document, baseUrl, allData);
        ProcessImageNodes(document, baseUrl);

        output.Write(document.ToHtml());
    }

    private void ProcessRepeaterNodes(IHtmlDocument document, string baseUrl, Dictionary<string, Dictionary<string, object>[]> allData)
    {
        var repeaterNodes = document.QuerySelectorAll("sg|repeater");
        foreach (var repeaterNode in repeaterNodes)
        {
            var repeaterItemNodes = repeaterNode.QuerySelectorAll("sg|repeateritem");
            foreach (var repeaterItemNode in repeaterItemNodes)
            {
                var dataSelection = repeaterNode.GetAttribute("dataselection");
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

                repeaterItemNode.InnerHtml = repeatedContent.ToString();
            }
        }
    }

    private void ProcessImageNodes(IHtmlDocument document, string baseUrl)
    {
        var imageNodes = document.QuerySelectorAll("img");
        foreach (var imageNode in imageNodes)
        {
            var srcAttributeValue = imageNode.GetAttribute("src");
            var result = ExpressionTransformer.RenderExpressions(srcAttributeValue, baseUrl);
            imageNode.SetAttribute("src", result);
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
}
