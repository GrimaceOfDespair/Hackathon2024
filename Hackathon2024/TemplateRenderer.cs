namespace Hackathon2024
{
    using HtmlAgilityPack;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using Data = System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, object>[]>;


    public struct KnownExpressions
    {
        public const string ItemValue = "itemValue";
        public const string Resource = "resource";
    }

    /// <summary>
    /// ExpressionTransformer for Parsing Selligent specific expressions '[% %]'
    /// </summary>
    public static class ExpressionTransformer
    {
        //private const string ExpressionPattern = @"\[%(?<expression>.*?)%\]";
        //private const string ItemValueFieldPattern = @"itemValue\('(?<field>.*?)'\)";
       //private const string ResourceFieldPattern = @"resource\('(?<resource>.*?)'\)";

        public static string RenderExpressions(string content, string baseUrl, Dictionary<string, object> data = null)
        {
            // Optimize string manipulation
            StringBuilder resultBuilder = new(content.Length);
            int prevIndex = 0;
            int startIndex = content.IndexOf("[%", prevIndex, StringComparison.Ordinal);
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

            while (true)
            {
                int matchIndex = expression.IndexOf("itemValue('", startIndex, StringComparison.Ordinal);
                if (matchIndex == -1)
                    break;

                int fieldStartIndex = matchIndex + 11;
                int fieldEndIndex = expression.IndexOf("')", fieldStartIndex, StringComparison.Ordinal);
                if (fieldEndIndex == -1)
                    break;

                string field = expression[fieldStartIndex..fieldEndIndex];
                if (data != null && data.TryGetValue(field, out object value))
                {
                    // Calculate the length of the resulting string after replacement
                    if (value != null)
                    {
                        int valueLength = value.ToString()!.Length;
                        int replaceLength = fieldEndIndex - matchIndex + 2;

                        // Replace the matched substring directly within the existing string
                        expression = expression.Remove(matchIndex, replaceLength)
                            .Insert(matchIndex, value.ToString() ?? throw new InvalidOperationException());

                        // Update the startIndex for the next search
                        startIndex = matchIndex + valueLength;
                    }
                }
                else
                {
                    // If the field is not found in the dictionary, move to the next character
                    startIndex = fieldEndIndex + 2;
                }

                // Ensure startIndex stays within bounds
                if (startIndex >= expressionLength)
                    break;
            }

            return expression;
        }


        private static string ReplaceResourceFields(string expression, string baseUrl)
        {
            int startIndex = 0;
            int expressionLength = expression.Length;

            while (true)
            {
                int matchIndex = expression.IndexOf("resource('", startIndex, StringComparison.Ordinal);
                if (matchIndex == -1)
                    break;

                int resourceStartIndex = matchIndex + 10;
                int resourceEndIndex = expression.IndexOf("')", resourceStartIndex, StringComparison.Ordinal);
                if (resourceEndIndex == -1)
                    break;

                string resource = expression[resourceStartIndex..resourceEndIndex];

                // Calculate the length of the resulting string after replacement
                int replaceLength = resourceEndIndex - matchIndex + 2;

                // Replace the matched substring directly within the existing string
                expression = expression.Remove(matchIndex, replaceLength)
                    .Insert(matchIndex, $"{baseUrl}{resource}");

                // Update the startIndex for the next search
                startIndex = matchIndex + baseUrl.Length + resource.Length;

                // Ensure startIndex stays within bounds
                if (startIndex >= expressionLength)
                    break;
            }

            return expression;
        }

    }

    public class TemplateRenderer
    {
        public void RenderTemplate(TextReader template, TextWriter output, Data allData)
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

            HtmlNode[] repeaterNodes = document.DocumentNode.SelectNodes("//*[name()='sg:repeater']")?.ToArray() ??
                                       Array.Empty<HtmlNode>();
            foreach (var repeaterNode in repeaterNodes)
            {
                HtmlNode[] repeaterItemNodes = repeaterNode.SelectNodes("//*[name()='sg:repeateritem']")?.ToArray() ??
                                               Array.Empty<HtmlNode>();

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