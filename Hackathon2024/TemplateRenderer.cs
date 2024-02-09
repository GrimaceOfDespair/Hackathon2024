using AngleSharp.Html.Parser;
using AngleSharp;
using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AngleSharp.Dom;
using AngleSharp.Html;

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
    public async Task RenderTemplate(TextReader template, TextWriter output, Dictionary<string, Dictionary<string, object>[]> allData)
    {
        var config = Configuration.Default;
        var context = BrowsingContext.New(config);
        var parser = context.GetService<IHtmlParser>();
        var document = await parser.ParseDocumentAsync(template.ReadToEnd());

        var baseUrl = GetBaseUrl(allData);

        var repeaterNodes = document.QuerySelectorAll("sg\\:repeater");
        if (repeaterNodes.Any())
        {
            foreach (var repeaterNode in repeaterNodes)
            {
                var repeaterItemNodes = repeaterNode.QuerySelectorAll("sg\\:repeateritem");
                if (repeaterItemNodes.Any())
                {
                    foreach (var repeaterItemNode in repeaterItemNodes)
                    {
                        var dataSelection = repeaterNode.GetAttribute("dataselection") ?? "";
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
        }

        var imageNodes = document.QuerySelectorAll("img");
        if (imageNodes.Any())
        {
            foreach (var imageNode in imageNodes)
            {
                var srcAttributeValue = imageNode.GetAttribute("src") ?? "";
                var result = ExpressionTransformer.RenderExpressions(srcAttributeValue, baseUrl);
                imageNode.SetAttribute("src", result);
            }
        }

        await output.WriteAsync(document.ToHtml());
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

    private void ReplaceHtml(IElement repeaterNode, StringBuilder repeatedContent)
    {
        repeaterNode.InnerHtml = repeatedContent.ToString();
        var repeatedNodes = repeaterNode.Children;
        var parent = repeaterNode.ParentElement;
        repeaterNode.Remove();

        foreach (var child in repeatedNodes)
        {
            parent.AppendChild(child);
        }
    }
}