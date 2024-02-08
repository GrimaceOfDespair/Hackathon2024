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
            return ExpressionDetector.Replace(content, expressionMatch =>
            {
                var expression = expressionMatch.Groups["expression"].Value;

                expression = ItemValueFieldDetector.Replace(expression, expressionMatch =>
                {
                    var field = expressionMatch.Groups["field"].Value;
                    return (data.TryGetValue(field, out var value) ? value : "").ToString();
                });

                expression = ResourceFieldDetector.Replace(expression, expressionMatch =>
                {
                    var resource = expressionMatch.Groups["resource"].Value;
                    return $"{baseUrl}{resource}";
                });

                return expression;
            });
        }
    }

    public class TemplateRenderer
    {
        public void RenderTemplate(TextReader template, TextWriter output, string baseUrl, Dictionary<string, object>[] data, Dictionary<string, string> variables)
        {
            var document = new HtmlDocument();
            document.Load(template);

            foreach (var variable in variables)
            {
                if (variable.Key.Equals("baseurl", StringComparison.OrdinalIgnoreCase))
                {
                    baseUrl = variable.Value;
                    break;
                }
            }

            var repeaterNodes = document.DocumentNode.SelectNodes("//*[name()='sg:repeater']")?.ToArray() ?? Array.Empty<HtmlNode>();

            foreach (var repeaterNode in repeaterNodes)
            {
                var repeaterItemNodes = repeaterNode.SelectNodes("//*[name()='sg:repeateritem']")?.ToArray() ?? Array.Empty<HtmlNode>();

                var dataSelection = repeaterNode.Attributes["dataselection"].Value;
                var repeaterItemContent = repeaterItemNodes.Select(node => node.InnerHtml).ToArray();

                var repeatedContent = new StringBuilder(repeaterItemContent.Length * 100); // Adjust the initial capacity based on your data

                repeaterItemContent.AsParallel().ForAll(repeaterItem =>
                {
                    foreach (var dataItem in data)
                    {
                        var result = ExpressionTransformer.RenderExpressions(repeaterItem, baseUrl, dataItem);
                        repeatedContent.Append(result);
                    }
                });

                ReplaceHtml(repeaterNode, repeatedContent);
            }

            var imageNodes = document.DocumentNode.SelectNodes("//img")?.ToArray() ?? Array.Empty<HtmlNode>();
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
