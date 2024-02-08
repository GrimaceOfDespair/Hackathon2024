using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Hackathon2024
{
    public static class ExpressionTransformer
    {
        public static string RenderExpressions(string content, string baseUrl, Dictionary<string, object> data = null)
        {
            var resultBuilder = new StringBuilder(content.Length);
            int prevIndex = 0;

            while (true)
            {
                int startIndex = content.IndexOf("[%", prevIndex, StringComparison.Ordinal);
                if (startIndex == -1)
                    break;

                int endIndex = content.IndexOf("%]", startIndex, StringComparison.Ordinal);
                if (endIndex == -1)
                    break;

                resultBuilder.Append(content, prevIndex, startIndex - prevIndex);

                string expression = content.Substring(startIndex + 2, endIndex - startIndex - 2);
                expression = ReplaceFields(expression, data);
                expression = ReplaceResource(expression, baseUrl);
                resultBuilder.Append(expression);

                prevIndex = endIndex + 2;
            }

            resultBuilder.Append(content, prevIndex, content.Length - prevIndex);

            return resultBuilder.ToString();
        }

        private static string ReplaceFields(string expression, Dictionary<string, object> data)
        {
            var resultBuilder = new StringBuilder(expression.Length);
            int startIndex = 0;

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
                if (data != null && data.TryGetValue(field, out object value) && value != null)
                {
                    resultBuilder.Append(expression, startIndex, matchIndex - startIndex);
                    resultBuilder.Append(value);
                }
                else
                {
                    resultBuilder.Append(expression, startIndex, fieldEndIndex - startIndex + 2);
                }

                startIndex = fieldEndIndex + 2;
            }

            resultBuilder.Append(expression, startIndex, expression.Length - startIndex);
            return resultBuilder.ToString();
        }

        private static string ReplaceResource(string expression, string baseUrl)
        {
            var resultBuilder = new StringBuilder(expression.Length + baseUrl.Length);
            int startIndex = 0;

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

                startIndex = resourceEndIndex + 2;
            }

            resultBuilder.Append(expression, startIndex, expression.Length - startIndex);
            return resultBuilder.ToString();
        }
    }

    public class TemplateRenderer
    {
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

        private static string GetBaseUrl(Dictionary<string, Dictionary<string, object>[]> allData)
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

        private static void ReplaceHtml(HtmlNode repeaterNode, StringBuilder repeatedContent)
        {
            repeaterNode.InnerHtml = repeatedContent.ToString();

            var parent = repeaterNode.ParentNode;
            repeaterNode.Remove();

            foreach (var node in repeaterNode.ChildNodes)
            {
                parent.AppendChild(node);
            }
        }
    }
}
