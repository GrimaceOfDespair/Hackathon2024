using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Hackathon2024
{
    public struct KnownExpressions
    {
        public const string ItemValue = "itemValue";
        public const string Resource = "resource";
    }

    public static class ExpressionTransformer
    {
        public static string RenderExpressions(string content, string baseUrl, Dictionary<string, object> data = null)
        {
            var resultBuilder = new StringBuilder(content.Length);
            int prevIndex = 0;
            int startIndex = content.IndexOf("[%", StringComparison.Ordinal);

            while (startIndex != -1)
            {
                int endIndex = content.IndexOf("%]", startIndex, StringComparison.Ordinal);
                if (endIndex == -1)
                {
                    // If no closing tag found, break the loop
                    break;
                }

                // Append content before the matched expression
                resultBuilder.Append(content, prevIndex, startIndex - prevIndex);

                // Extract expression and perform replacements
                string expression = content.Substring(startIndex + 2, endIndex - startIndex - 2);
                expression = ReplaceItemValueFields(expression, data);
                expression = ReplaceResourceFields(expression, baseUrl);
                resultBuilder.Append(expression);

                // Update previous index
                prevIndex = endIndex + 2;

                // Find next start index
                startIndex = content.IndexOf("[%", prevIndex, StringComparison.Ordinal);
            }

            // Append remaining content after the last matched expression
            resultBuilder.Append(content, prevIndex, content.Length - prevIndex);

            // Return the resulting string
            return resultBuilder.ToString();
        }

        private static string ReplaceItemValueFields(string expression, Dictionary<string, object> data)
        {
            int startIndex = 0;
            int expressionLength = expression.Length;
            var resultBuilder = new StringBuilder(expressionLength);

            while (true)
            {
                int matchIndex = expression.IndexOf("itemValue('", startIndex, StringComparison.Ordinal);
                if (matchIndex == -1)
                    break;

                int fieldStartIndex = matchIndex + 11;
                int fieldEndIndex = expression.IndexOf("')", fieldStartIndex, StringComparison.Ordinal);
                if (fieldEndIndex == -1)
                    break;

                string field = expression.Substring(fieldStartIndex, fieldEndIndex - fieldStartIndex);

                if (data != null && data.TryGetValue(field, out object value))
                {
                    if (value != null)
                    {
                        resultBuilder.Append(expression, startIndex, matchIndex - startIndex);
                        resultBuilder.Append(value);
                    }
                }
                else
                {
                    resultBuilder.Append(expression, startIndex, fieldEndIndex - startIndex + 2);
                }

                // Update the startIndex for the next search
                startIndex = fieldEndIndex + 2;

                // Ensure startIndex stays within bounds
                if (startIndex >= expressionLength)
                    break;
            }

            // Append the remaining content if any
            if (startIndex < expressionLength)
                resultBuilder.Append(expression, startIndex, expressionLength - startIndex);

            return resultBuilder.ToString();
        }

        private static string ReplaceResourceFields(string expression, string baseUrl)
        {
            int startIndex = 0;
            int expressionLength = expression.Length;
            int baseUrlLength = baseUrl.Length;
            var resultBuilder = new StringBuilder(expressionLength + baseUrlLength);

            while (true)
            {
                int matchIndex = expression.IndexOf("resource('", startIndex, StringComparison.Ordinal);
                if (matchIndex == -1)
                    break;

                int resourceStartIndex = matchIndex + 10;
                int resourceEndIndex = expression.IndexOf("')", resourceStartIndex, StringComparison.Ordinal);
                if (resourceEndIndex == -1)
                    break;

                resultBuilder.Append(expression, startIndex, matchIndex - startIndex);
                resultBuilder.Append(baseUrl);
                resultBuilder.Append(expression, resourceStartIndex, resourceEndIndex - resourceStartIndex);

                // Update the startIndex for the next search
                startIndex = resourceEndIndex + 2;

                // Ensure startIndex stays within bounds
                if (startIndex >= expressionLength)
                    break;
            }

            // Append the remaining content if any
            if (startIndex < expressionLength)
                resultBuilder.Append(expression, startIndex, expressionLength - startIndex);

            return resultBuilder.ToString();
        }
    }

    public class TemplateRenderer
    {
        private string GetBaseUrl(Dictionary<string, Dictionary<string, object>[]> allData)
        {
            foreach (var entry in allData["variables"])
            {
                if (entry.TryGetValue("name", out var name) && "baseurl".Equals(name?.ToString()))
                {
                    return entry.TryGetValue("value", out var value) ? value.ToString() : "";
                }
            }
            return "";
        }

        public void RenderTemplate(TextReader template, TextWriter output, Dictionary<string, Dictionary<string, object>[]> allData)
        {
            using var document = new HtmlDocument();
            document.Load(template);

            string baseUrl = GetBaseUrl(allData);

            var repeaterNodes = document.DocumentNode.SelectNodes("//*[name()='sg:repeater']");
            if (repeaterNodes != null)
            {
                foreach (var repeaterNode in repeaterNodes)
                {
                    var repeaterItemNodes = repeaterNode.SelectNodes("//*[name()='sg:repeateritem']");
                    if (repeaterItemNodes != null)
                    {
                        foreach (var repeaterItemNode in repeaterItemNodes)
                        {
                            string dataSelection = repeaterNode.GetAttributeValue("dataselection", "");
                            string repeaterItemContent = repeaterItemNode.InnerHtml;
                            var repeatedContent = new StringBuilder();
                            foreach (var dataItem in allData[dataSelection])
                            {
                                string result = ExpressionTransformer.RenderExpressions(repeaterItemContent, baseUrl, dataItem);
                                repeatedContent.Append(result);
                            }
                            ReplaceHtml(repeaterNode, repeatedContent);
                        }
                    }
                }
            }

            var imageNodes = document.DocumentNode.SelectNodes("//img");
            if (imageNodes != null)
            {
                foreach (var imageNode in imageNodes)
                {
                    string srcAttributeValue = imageNode.GetAttributeValue("src", "");
                    string result = ExpressionTransformer.RenderExpressions(srcAttributeValue, baseUrl);
                    imageNode.SetAttributeValue("src", result);
                }
            }

            document.Save(output);
        }

        private static void ReplaceHtml(HtmlNode repeaterNode, StringBuilder repeatedContent)
        {
            repeaterNode.InnerHtml = repeatedContent.ToString();

            var repeatedNodes = repeaterNode.ChildNodes;
            var parent = repeaterNode.ParentNode;

            repeaterNode.Remove();

            foreach (var node in repeatedNodes)
            {
                parent.AppendChild(node);
            }
        }
    }
}
