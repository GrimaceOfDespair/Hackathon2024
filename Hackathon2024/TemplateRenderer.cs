namespace Hackathon2024
{
    using HtmlAgilityPack;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;

    public class ExpressionTransformer
    {
        private const string ExpressionStartMarker = "[%";
        private const string ExpressionEndMarker = "%]";
        private const string ItemValuePrefix = "itemValue('";
        private const string ItemValueSuffix = "')";
        private const string ResourcePrefix = "resource('";
        private const string ResourceSuffix = "')";

        public static string RenderExpressions(string content, string baseUrl, Dictionary<string, object> data = null)
        {
            if (string.IsNullOrEmpty(content))
                return content;

            StringBuilder result = new StringBuilder(content.Length);
            int currentIndex = 0;

            while (currentIndex < content.Length)
            {
                int expressionStartIndex = content.IndexOf(ExpressionStartMarker, currentIndex);
                if (expressionStartIndex == -1)
                {
                    result.Append(content.Substring(currentIndex));
                    break;
                }

                int expressionEndIndex = content.IndexOf(ExpressionEndMarker, expressionStartIndex);
                if (expressionEndIndex == -1)
                {
                    result.Append(content.Substring(currentIndex));
                    break;
                }

                result.Append(content, currentIndex, expressionStartIndex - currentIndex);
                result.Append(ProcessExpression(content, expressionStartIndex + ExpressionStartMarker.Length, expressionEndIndex, baseUrl, data));

                currentIndex = expressionEndIndex + ExpressionEndMarker.Length;
            }

            return result.ToString();
        }

        private static string ProcessExpression(string content, int startIndex, int endIndex, string baseUrl, Dictionary<string, object> data)
        {
            string expression = content.Substring(startIndex, endIndex - startIndex);

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

            HtmlNode[] repeaterNodes = document.DocumentNode.SelectNodes("//*[name()='sg:repeater']")?.ToArray() ?? Array.Empty<HtmlNode>();
            foreach (var repeaterNode in repeaterNodes)
            {
                var dataSelection = repeaterNode.Attributes["dataselection"]?.Value;
                var repeaterItemNodes = repeaterNode.SelectNodes(".//*[name()='sg:repeateritem']")?.ToArray() ?? Array.Empty<HtmlNode>();

                foreach (var repeaterItemNode in repeaterItemNodes)
                {
                    var repeaterItemContent = repeaterItemNode.InnerHtml;
                    var repeatedContent = new StringBuilder();

                    if (dataSelection != null && allData.TryGetValue(dataSelection, out var data))
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

            HtmlNode[] imageNodes = document.DocumentNode.SelectNodes("//img")?.ToArray() ?? Array.Empty<HtmlNode>();
            foreach (var imageNode in imageNodes)
            {
                var srcAttributeValue = imageNode.Attributes["src"]?.Value;
                var result = ExpressionTransformer.RenderExpressions(srcAttributeValue, baseUrl);
                imageNode.Attributes["src"].Value = result;
            }

            document.DocumentNode.WriteTo(output);
        }

        private static string GetBaseUrl(Dictionary<string, Dictionary<string, object>[]> allData)
        {
            return allData.TryGetValue("variables", out var variables) &&
                   variables.Any(x => x.TryGetValue("name", out var name) && "baseurl".Equals(name.ToString()))
                ? variables.First(x => x["name"].ToString() == "baseurl")["value"]?.ToString() ?? ""
                : "";
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
