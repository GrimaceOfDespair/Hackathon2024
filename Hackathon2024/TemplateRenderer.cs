using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Hackathon2024
{
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
            StringBuilder resultBuilder = new StringBuilder(content.Length);
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
                        // Calculate the length of the resulting string after replacement
                        int valueLength = value.ToString().Length;
                        int replaceLength = fieldEndIndex - matchIndex + 2;

                        // Replace the matched substring directly within the existing string
                        expression = expression.Remove(matchIndex, replaceLength)
                            .Insert(matchIndex, value.ToString());

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
            int baseUrlLength = baseUrl.Length;

            while (true)
            {
                int matchIndex = expression.IndexOf("resource('", startIndex, StringComparison.Ordinal);
                if (matchIndex == -1)
                    break;

                int resourceStartIndex = matchIndex + 10;
                int resourceEndIndex = expression.IndexOf("')", resourceStartIndex, StringComparison.Ordinal);
                if (resourceEndIndex == -1)
                    break;

                // Calculate the length of the resulting string after replacement
                int replaceLength = resourceEndIndex - matchIndex + 2;

                // Ensure the capacity of the resulting string
                int newLength = expression.Length - replaceLength + baseUrlLength;
                StringBuilder resultBuilder = new StringBuilder(newLength);

                // Append the content before the matched substring
                resultBuilder.Append(expression, 0, matchIndex);

                // Append the base URL and resource
                resultBuilder.Append(baseUrl);
                resultBuilder.Append(expression, resourceStartIndex, resourceEndIndex - resourceStartIndex);

                // Append the content after the matched substring
                resultBuilder.Append(expression, resourceEndIndex + 2, expression.Length - resourceEndIndex - 2);

                // Update the expression with the modified string
                expression = resultBuilder.ToString();

                // Update the startIndex for the next search
                startIndex = matchIndex + baseUrlLength + resourceEndIndex - resourceStartIndex;

                // Ensure startIndex stays within bounds
                if (startIndex >= expressionLength)
                    break;
            }

            return expression;
        }
    }


    public class TemplateRenderer
    {
        private string GetBaseUrl(Data allData)
        {
            foreach (var entry in allData["variables"])
            {
                if (entry.TryGetValue("name", out var name) && "baseurl".Equals(name?.ToString()))
                {
                    if (entry.TryGetValue("value", out var value))
                    {
                        return value?.ToString() ?? "";
                    }
                }
            }

            return "";
        }

        // Other methods...

        public void RenderTemplate(TextReader template, TextWriter output, Data allData)
        {
            var document = new HtmlDocument();
            document.Load(template);

            // Other members...


            string baseUrl = GetBaseUrl(allData);


            HtmlNode[] repeaterNodes = document.DocumentNode.SelectNodes("//*[name()='sg:repeater']")?.ToArray() ??
                                       Array.Empty<HtmlNode>();
            foreach (var repeaterNode in repeaterNodes)
            {
                HtmlNode[] repeaterItemNodes = repeaterNode.SelectNodes("//*[name()='sg:repeateritem']")?.ToArray() ??
                                               Array.Empty<HtmlNode>();

                foreach (var repeaterItemNode in repeaterItemNodes)
                {
                    var repeaterItemNode = repeaterItemNodes[j];
                    string dataSelection = repeaterNode.GetAttributeValue("dataselection", "");
                    string repeaterItemContent = repeaterItemNode.InnerHtml;
                    StringBuilder repeatedContent = new StringBuilder();
                    for (int k = 0; k < allData[dataSelection].Length; k++)
                    {
                        var dataItem = allData[dataSelection][k];
                        string result = ExpressionTransformer.RenderExpressions(repeaterItemContent, baseUrl, dataItem);
                        repeatedContent.Append(result);
                    }
                    ReplaceHtml(repeaterNode, repeatedContent);
                }
            }

            HtmlNode[] imageNodes = document.DocumentNode.SelectNodes("//img")?.ToArray() ?? Array.Empty<HtmlNode>();
            for (int i = 0; i < imageNodes.Length; i++)
            {
                var imageNode = imageNodes[i];
                string srcAttributeValue = imageNode.GetAttributeValue("src", "");
                string result = ExpressionTransformer.RenderExpressions(srcAttributeValue, baseUrl);
                imageNode.SetAttributeValue("src", result);
            }

            document.Save(output);
        }

        private static string GetBaseUrl(Dictionary<string, Dictionary<string, object>[]> allData)
        {
            for (int i = 0; i < allData["variables"].Length; i++)
            {
                var dataItem = allData["variables"][i];
                if (dataItem.TryGetValue("name", out object name) && "baseurl".Equals(name.ToString()))
                {
                    return dataItem["value"]?.ToString() ?? "";
                }
            }
            return "";
        }

        private static void ReplaceHtml(HtmlNode repeaterNode, StringBuilder repeatedContent)
        {
            repeaterNode.InnerHtml = repeatedContent.ToString();

            HtmlNode[] repeatedNodes = repeaterNode.ChildNodes.ToArray();
            HtmlNode parent = repeaterNode.ParentNode;
            repeaterNode.Remove();

            for (int i = 0; i < repeatedNodes.Count; i++)
            {
                parent.AppendChild(repeatedNodes[i]);
            }
        }
    }
}