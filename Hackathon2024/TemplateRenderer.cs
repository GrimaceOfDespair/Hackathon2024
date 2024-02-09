using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Hackathon2024
{
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

        public static string ReplaceItemValueFields(string expression, Dictionary<string, object> data)
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

        public static string ReplaceResourceFields(string expression, string baseUrl)
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
            if (allData != null && allData.TryGetValue("variables", out var variables) && variables != null)
            {
                foreach (var entry in variables)
                {
                    if (entry != null && entry.TryGetValue("name", out var name) && "baseurl".Equals(name?.ToString()))
                    {
                        return entry.TryGetValue("value", out var value) && value != null ? value.ToString() : "";
                    }
                }
            }

            return "";
        }

       public void RenderTemplate(TextReader template, TextWriter output, Dictionary<string, Dictionary<string, object>[]> allData)
{
    if (template == null || output == null || allData == null)
        throw new ArgumentNullException();

    var document = new HtmlDocument();
    document.Load(template);

    string baseUrl = GetBaseUrl(allData);

    if (document.DocumentNode != null)
    {
        var repeaterNodes = document.DocumentNode.SelectNodes("//*[name()='sg:repeater']");
        if (repeaterNodes != null)
        {
            Parallel.ForEach(repeaterNodes, repeaterNode =>
            {
                var repeaterItemNodes = repeaterNode.SelectNodes("//*[name()='sg:repeateritem']");
                if (repeaterItemNodes != null)
                {
                    string dataSelection = repeaterNode.GetAttributeValue("dataselection", "");
                    var repeatedContent = new StringBuilder();
                    foreach (var dataItem in allData[dataSelection])
                    {
                        if (dataItem != null)
                        {
                            string result = RenderRepeaterItem(repeaterItemNodes[0].InnerHtml, baseUrl, dataItem);
                            repeatedContent.Append(result);
                        }
                    }
                    ReplaceHtml(repeaterNode, repeatedContent);
                }
            });
        }

        var imageNodes = document.DocumentNode.SelectNodes("//img");
        if (imageNodes != null)
        {
            Parallel.ForEach(imageNodes, imageNode =>
            {
                if (imageNode != null)
                {
                    string srcAttributeValue = imageNode.GetAttributeValue("src", "");
                    string result = ExpressionTransformer.RenderExpressions(srcAttributeValue, baseUrl);
                    imageNode.SetAttributeValue("src", result);
                }
            });
        }
    }

    document.Save(output);
}


        private string RenderRepeaterItem(string repeaterItemContent, string baseUrl, Dictionary<string, object> data)
        {
            if (string.IsNullOrEmpty(repeaterItemContent) || string.IsNullOrEmpty(baseUrl) || data == null)
                return repeaterItemContent;

            var resultBuilder = new StringBuilder(repeaterItemContent.Length);
            int prevIndex = 0;
            int startIndex = repeaterItemContent.IndexOf("[%", StringComparison.Ordinal);

            while (startIndex != -1)
            {
                int endIndex = repeaterItemContent.IndexOf("%]", startIndex, StringComparison.Ordinal);
                if (endIndex == -1)
                {
                    // If no closing tag found, break the loop
                    break;
                }

                // Append content before the matched expression
                resultBuilder.Append(repeaterItemContent, prevIndex, startIndex - prevIndex);

                // Extract expression and perform replacements
                string expression = repeaterItemContent.Substring(startIndex + 2, endIndex - startIndex - 2);
                expression = ExpressionTransformer.ReplaceItemValueFields(expression, data);
                expression = ExpressionTransformer.ReplaceResourceFields(expression, baseUrl);
                resultBuilder.Append(expression);

                // Update previous index
                prevIndex = endIndex + 2;

                // Find next start index
                startIndex = repeaterItemContent.IndexOf("[%", prevIndex, StringComparison.Ordinal);
            }

            // Append remaining content after the last matched expression
            resultBuilder.Append(repeaterItemContent, prevIndex, repeaterItemContent.Length - prevIndex);

            // Return the resulting string
            return resultBuilder.ToString();
        }

        private static void ReplaceHtml(HtmlNode repeaterNode, StringBuilder repeatedContent)
        {
            if (repeaterNode == null || repeatedContent == null)
                return;

            repeaterNode.InnerHtml = repeatedContent.ToString();

            var repeatedNodes = repeaterNode.ChildNodes;
            var parent = repeaterNode.ParentNode;

            repeaterNode.Remove();

            foreach (var node in repeatedNodes)
            {
                parent?.AppendChild(node);
            }
        }
    }
}