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
        public static string RenderExpressions(string content, string baseUrl, Dictionary<string, object> data = null)
        {
            const string expressionStartMarker = "[%";
            const string expressionEndMarker = "%]";

            if (string.IsNullOrEmpty(content))
                return content;

            StringBuilder result = new StringBuilder(content.Length);
            int currentIndex = 0;

            while (currentIndex < content.Length)
            {
                int expressionStartIndex = content.IndexOf(expressionStartMarker, currentIndex);
                if (expressionStartIndex == -1)
                {
                    result.Append(content.Substring(currentIndex));
                    break;
                }

                int expressionEndIndex = content.IndexOf(expressionEndMarker, expressionStartIndex);
                if (expressionEndIndex == -1)
                {
                    result.Append(content.Substring(currentIndex));
                    break;
                }

                result.Append(content.Substring(currentIndex, expressionStartIndex - currentIndex));

                string expression = content.Substring(expressionStartIndex + expressionStartMarker.Length, expressionEndIndex - expressionStartIndex - expressionEndMarker.Length);
                result.Append(ProcessExpression(expression, baseUrl, data));

                currentIndex = expressionEndIndex + expressionEndMarker.Length;
            }

            return result.ToString();
        }


        private static string ProcessExpression(string expression, string baseUrl, Dictionary<string, object> data)
        {
            const string itemValuePrefix = "itemValue('";
            const string itemValueSuffix = "')";
            const string resourcePrefix = "resource('";
            const string resourceSuffix = "')";

            if (expression.StartsWith(itemValuePrefix) && expression.EndsWith(itemValueSuffix))
            {
                string field = expression.Substring(itemValuePrefix.Length, expression.Length - itemValuePrefix.Length - itemValueSuffix.Length);
                if (data != null && data.TryGetValue(field, out object value))
                {
                    return value?.ToString() ?? "";
                }
                return "";
            }
            else if (expression.StartsWith(resourcePrefix) && expression.EndsWith(resourceSuffix))
            {
                string resource = expression.Substring(resourcePrefix.Length, expression.Length - resourcePrefix.Length - resourceSuffix.Length);
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