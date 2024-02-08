using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

public class ExpressionTransformer
{
    public static string RenderExpressions(string content, string baseUrl, Dictionary<string, object> data = null)
    {
        if (string.IsNullOrEmpty(content))
            return content;

        var result = new StringBuilder(content.Length);
        int currentIndex = 0;
        int contentLength = content.Length;

        while (currentIndex < contentLength)
        {
            int expressionStartIndex = content.IndexOf("[%", currentIndex);
            if (expressionStartIndex == -1)
            {
                result.Append(content, currentIndex, contentLength - currentIndex);
                break;
            }

            result.Append(content, currentIndex, expressionStartIndex - currentIndex);

            int expressionEndIndex = content.IndexOf("%]", expressionStartIndex + 2);
            if (expressionEndIndex == -1)
            {
                result.Append(content, expressionStartIndex, contentLength - expressionStartIndex);
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
        if (expression.StartsWith("itemValue('") && expression.EndsWith("')"))
        {
            string field = expression.Substring(11, expression.Length - 13);
            if (data != null && data.TryGetValue(field, out var value))
            {
                return value?.ToString() ?? "";
            }
            return "";
        }
        else if (expression.StartsWith("resource('") && expression.EndsWith("')"))
        {
            string resource = expression.Substring(10, expression.Length - 12);
            return baseUrl + resource;
        }

        return "";
    }
}

public class TemplateRenderer
{
    public void RenderTemplate(TextReader template, TextWriter output, Dictionary<string, Dictionary<string, object>[]> allData)
    {
        var document = new HtmlAgilityPack.HtmlDocument();
        document.Load(template);

        var baseUrl = allData["variables"]
            .Where(x => x.TryGetValue("name", out object name) && "baseurl".Equals(name.ToString()))
            .Select(x => x["value"]?.ToString() ?? "")
            .FirstOrDefault();

        HtmlAgilityPack.HtmlNode[] repeaterNodes = document.DocumentNode.SelectNodes("//*[name()='sg:repeater']")?.ToArray() ?? Array.Empty<HtmlAgilityPack.HtmlNode>();
        foreach (var repeaterNode in repeaterNodes)
        {
            HtmlAgilityPack.HtmlNode[] repeaterItemNodes = repeaterNode.SelectNodes("//*[name()='sg:repeateritem']")?.ToArray() ?? Array.Empty<HtmlAgilityPack.HtmlNode>();

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

        HtmlAgilityPack.HtmlNode[] imageNodes = document.DocumentNode.SelectNodes("//img")?.ToArray() ?? Array.Empty<HtmlAgilityPack.HtmlNode>();
        foreach (var imageNode in imageNodes)
        {
            var srcAttributeValue = imageNode.Attributes["src"].Value;
            var result = ExpressionTransformer.RenderExpressions(srcAttributeValue, baseUrl);
            imageNode.Attributes["src"].Value = result;
        }

        document.DocumentNode.WriteTo(output);
    }

    private static void ReplaceHtml(HtmlAgilityPack.HtmlNode repeaterNode, StringBuilder repeatedContent)
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