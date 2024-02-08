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

    public class ExpressionTransformer
    {
        public static string RenderExpressions(string content, string baseUrl, Dictionary<string, object> data = null)
        {
            StringBuilder resultBuilder = new StringBuilder(content.Length);

            int startIndex = 0;
            while (true)
            {
                int expressionStartIndex = content.IndexOf("[%", startIndex);
                if (expressionStartIndex == -1)
                    break;

                int expressionEndIndex = content.IndexOf("%]", expressionStartIndex);
                if (expressionEndIndex == -1)
                    break;

                resultBuilder.Append(content, startIndex, expressionStartIndex - startIndex);

                string expression = content.Substring(expressionStartIndex + 2, expressionEndIndex - expressionStartIndex - 2);
                expression = ReplaceItemValueFields(expression, data);
                expression = ReplaceResourceFields(expression, baseUrl);
                resultBuilder.Append(expression);

                startIndex = expressionEndIndex + 2;
            }
            resultBuilder.Append(content, startIndex, content.Length - startIndex);

            return resultBuilder.ToString();
        }

        private static string ReplaceItemValueFields(string expression, Dictionary<string, object> data)
        {
            int startIndex = 0;
            while (true)
            {
                int fieldStartIndex = expression.IndexOf("itemValue('", startIndex);
                if (fieldStartIndex == -1)
                    break;

                int fieldEndIndex = expression.IndexOf("')", fieldStartIndex);
                if (fieldEndIndex == -1)
                    break;

                string field = expression.Substring(fieldStartIndex + 11, fieldEndIndex - fieldStartIndex - 11);
                if (data != null && data.TryGetValue(field, out object value))
                {
                    expression = expression.Remove(fieldStartIndex, fieldEndIndex - fieldStartIndex + 2).Insert(fieldStartIndex, value.ToString());
                    startIndex = fieldStartIndex + value.ToString().Length;
                }
                else
                {
                    startIndex = fieldEndIndex + 2;
                }
            }
            return expression;
        }

        private static string ReplaceResourceFields(string expression, string baseUrl)
        {
            int startIndex = 0;
            while (true)
            {
                int resourceStartIndex = expression.IndexOf("resource('", startIndex);
                if (resourceStartIndex == -1)
                    break;

                int resourceEndIndex = expression.IndexOf("')", resourceStartIndex);
                if (resourceEndIndex == -1)
                    break;

                string resource = expression.Substring(resourceStartIndex + 10, resourceEndIndex - resourceStartIndex - 10);
                expression = expression.Remove(resourceStartIndex, resourceEndIndex - resourceStartIndex + 2).Insert(resourceStartIndex, $"{baseUrl}{resource}");
                startIndex = resourceStartIndex + baseUrl.Length + resource.Length;
            }
            return expression;
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
                HtmlNode[] repeaterItemNodes = repeaterNode.SelectNodes(".//*[name()='sg:repeateritem']")?.ToArray() ?? Array.Empty<HtmlNode>();
                foreach (var repeaterItemNode in repeaterItemNodes)
                {
                    string dataSelection = repeaterNode.GetAttributeValue("dataselection", "");
                    string repeaterItemContent = repeaterItemNode.InnerHtml;
                    StringBuilder repeatedContent = new StringBuilder();
                    foreach (var dataItem in allData[dataSelection])
                    {
                        string result = ExpressionTransformer.RenderExpressions(repeaterItemContent, baseUrl, dataItem);
                        repeatedContent.Append(result);
                    }
                    ReplaceHtml(repeaterNode, repeatedContent);
                }
            }

            HtmlNode[] imageNodes = document.DocumentNode.SelectNodes("//img")?.ToArray() ?? Array.Empty<HtmlNode>();
            foreach (var imageNode in imageNodes)
            {
                string srcAttributeValue = imageNode.GetAttributeValue("src", "");
                string result = ExpressionTransformer.RenderExpressions(srcAttributeValue, baseUrl);
                imageNode.SetAttributeValue("src", result);
            }

            document.Save(output);
        }

        private static string GetBaseUrl(Dictionary<string, Dictionary<string, object>[]> allData)
        {
            foreach (var dataItem in allData["variables"])
            {
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

            foreach (var child in repeatedNodes)
            {
                parent.AppendChild(child);
            }
        }
    }
}
