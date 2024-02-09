using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            int expressionStartIndex = content.IndexOf("[%", currentIndex, StringComparison.Ordinal);
            if (expressionStartIndex == -1)
            {
                result.Append(content, currentIndex, content.Length - currentIndex);
                break;
            }

            int expressionEndIndex = content.IndexOf("%]", expressionStartIndex + 2, StringComparison.Ordinal);
            if (expressionEndIndex == -1)
            {
                result.Append(content, expressionStartIndex, content.Length - expressionStartIndex);
                break;
            }

            int expressionLength = expressionEndIndex - expressionStartIndex - 2;

            result.Append(content, currentIndex, expressionStartIndex - currentIndex)
                  .Append(ProcessExpression(content, expressionStartIndex + 2, expressionLength, baseUrl, data));

            currentIndex = expressionEndIndex + 2;
        }

        return result.ToString();
    }

    private static string ProcessExpression(string content, int startIndex, int length, string baseUrl, Dictionary<string, object> data)
    {
        const string ItemValuePrefix = "itemValue('";
        const string ResourcePrefix = "resource('";

        if (content[startIndex] == '%' && content[startIndex + length - 1] == '%' && length > 2)
        {
            string expression = content.Substring(startIndex, length);
            if (expression.StartsWith(ItemValuePrefix, StringComparison.Ordinal) && expression.EndsWith("')"))
            {
                string field = expression.Substring(ItemValuePrefix.Length, length - ItemValuePrefix.Length - 2);
                return data?.GetValueOrDefault(field)?.ToString() ?? "";
            }
            else if (expression.StartsWith(ResourcePrefix, StringComparison.Ordinal) && expression.EndsWith("')"))
            {
                string resource = expression.Substring(ResourcePrefix.Length, length - ResourcePrefix.Length - 2);
                return baseUrl + resource;
            }
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