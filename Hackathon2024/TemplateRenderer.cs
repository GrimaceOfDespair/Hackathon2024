using HtmlAgilityPack;
using System;
using System.Collections.Concurrent;
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
        private string GetBaseUrl(Dictionary<string, Dictionary<string, object>[]> allData)
        {
            foreach (var entry in allData["variables"])
            {
                if (entry.TryGetValue("name", out var name) && "baseurl".Equals(name?.ToString()))
                    return entry.TryGetValue("value", out var value) ? value.ToString() : "";
            }
            return "";
        }

        public void RenderTemplate(TextReader template, TextWriter output, Dictionary<string, Dictionary<string, object>[]> allData)
        {
            var document = new HtmlDocument();
            document.Load(template);

            string baseUrl = GetBaseUrl(allData);

            ProcessRepeaterNodes(document, baseUrl, allData);
            ProcessImageNodes(document.DocumentNode.SelectNodes("//img"), baseUrl);

            document.Save(output);
        }

        private void ProcessRepeaterNodes(HtmlDocument document, string baseUrl, Dictionary<string, Dictionary<string, object>[]> allData)
        {
            var repeaterNodes = document.DocumentNode.SelectNodes("//*[name()='sg:repeater']");
            if (repeaterNodes != null)
            {
                var bag = new ConcurrentBag<Tuple<HtmlNode, StringBuilder>>();

                Parallel.ForEach(repeaterNodes, repeaterNode =>
                {
                    var repeaterItemNodes = repeaterNode.SelectNodes("//*[name()='sg:repeateritem']");
                    if (repeaterItemNodes != null)
                    {
                        var repeatedContent = new StringBuilder();

                        foreach (var repeaterItemNode in repeaterItemNodes)
                        {
                            string dataSelection = repeaterNode.GetAttributeValue("dataselection", "");
                            string repeaterItemContent = repeaterItemNode.InnerHtml;
                            var resultBuilder = new StringBuilder(repeaterItemContent.Length);

                            foreach (var dataItem in allData[dataSelection])
                            {
                                string result = ExpressionTransformer.RenderExpressions(repeaterItemContent, baseUrl, dataItem);
                                resultBuilder.Append(result);
                            }
                            repeatedContent.Append(resultBuilder);
                        }

                        bag.Add(Tuple.Create(repeaterNode, repeatedContent));
                    }
                });

                foreach (var item in bag)
                {
                    ReplaceHtml(item.Item1, item.Item2);
                }
            }
        }

        private void ProcessImageNodes(HtmlNodeCollection imageNodes, string baseUrl)
        {
            if (imageNodes != null)
            {
                Parallel.ForEach(imageNodes, imageNode =>
                {
                    string srcAttributeValue = imageNode.GetAttributeValue("src", "");
                    string result = ExpressionTransformer.RenderExpressions(srcAttributeValue, baseUrl);
                    imageNode.SetAttributeValue("src", result);
                });
            }
        }

        private static void ReplaceHtml(HtmlNode repeaterNode, StringBuilder repeatedContent)
        {
            repeaterNode.InnerHtml = repeatedContent.ToString();

            var parent = repeaterNode.ParentNode;

            foreach (var node in repeaterNode.ChildNodes)
                parent.AppendChild(node);

            repeaterNode.Remove();
        }
    }
}
