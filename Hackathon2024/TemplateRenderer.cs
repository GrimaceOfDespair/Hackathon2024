using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Hackathon2024
{
    public struct KnownExpressions
    {
        public const string ItemValue = "itemValue";
        public const string Resource = "resource";
    }

    public class ExpressionTransformer
    {
        private static readonly Regex ExpressionDetector =
            new Regex(@"\[%(?<expression>.*?)%\]",
            RegexOptions.IgnorePatternWhitespace | RegexOptions.Singleline | RegexOptions.Compiled, TimeSpan.FromSeconds(15));

        private static readonly Regex ItemValueFieldDetector =
            new Regex(@"itemValue\('(?<field>.*?)'\)",
            RegexOptions.IgnorePatternWhitespace | RegexOptions.Singleline | RegexOptions.Compiled, TimeSpan.FromSeconds(15));

        private static readonly Regex ResourceFieldDetector =
            new Regex(@"resource\('(?<resource>.*?)'\)",
            RegexOptions.IgnorePatternWhitespace | RegexOptions.Singleline | RegexOptions.Compiled, TimeSpan.FromSeconds(15));

        public static string RenderExpressions(string content, string baseUrl, Dictionary<string, object> data = null)
        {
            StringBuilder resultBuilder = new StringBuilder(content.Length);

            MatchCollection matches = ExpressionDetector.Matches(content);
            int prevIndex = 0;
            for (int i = 0; i < matches.Count; i++)
            {
                Match expressionMatch = matches[i];
                resultBuilder.Append(content, prevIndex, expressionMatch.Index - prevIndex);
                prevIndex = expressionMatch.Index + expressionMatch.Length;
                string expression = expressionMatch.Groups["expression"].Value;
                expression = ReplaceItemValueFields(expression, data);
                expression = ReplaceResourceFields(expression, baseUrl);
                resultBuilder.Append(expression);
            }
            resultBuilder.Append(content, prevIndex, content.Length - prevIndex);

            return resultBuilder.ToString();
        }

        private static string ReplaceItemValueFields(string expression, Dictionary<string, object> data)
        {
            int startIndex = 0;
            while (true)
            {
                Match match = ItemValueFieldDetector.Match(expression, startIndex);
                if (!match.Success)
                    break;
                string field = match.Groups["field"].Value;
                if (data != null && data.TryGetValue(field, out object value))
                {
                    expression = expression.Remove(match.Index, match.Length).Insert(match.Index, value.ToString());
                    startIndex = match.Index + value.ToString().Length;
                }
                else
                {
                    startIndex = match.Index + match.Length;
                }
            }
            return expression;
        }

        private static string ReplaceResourceFields(string expression, string baseUrl)
        {
            int startIndex = 0;
            while (true)
            {
                Match match = ResourceFieldDetector.Match(expression, startIndex);
                if (!match.Success)
                    break;
                string resource = match.Groups["resource"].Value;
                expression = expression.Remove(match.Index, match.Length).Insert(match.Index, $"{baseUrl}{resource}");
                startIndex = match.Index + baseUrl.Length + resource.Length;
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
            for (int i = 0; i < repeaterNodes.Length; i++)
            {
                var repeaterNode = repeaterNodes[i];
                HtmlNode[] repeaterItemNodes = repeaterNode.SelectNodes(".//*[name()='sg:repeateritem']")?.ToArray() ?? Array.Empty<HtmlNode>();
                for (int j = 0; j < repeaterItemNodes.Length; j++)
                {
                    var repeaterItemNode = repeaterItemNodes[j];
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
            for (int k = 0; k < imageNodes.Length; k++)
            {
                var imageNode = imageNodes[k];
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

            for (int i = 0; i < repeatedNodes.Length; i++)
            {
                var child = repeatedNodes[i];
                parent.AppendChild(child);
            }
        }
    }
}
