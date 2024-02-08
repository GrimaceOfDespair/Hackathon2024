namespace Hackathon2024
{
    using HtmlAgilityPack;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;

    public struct KnownExpressions
    {
        public const string ItemValue = "itemValue";
        public const string Resource = "resource";
    }

    /// <summary>
    /// ExpressionTransformer for Parsing Selligent specific expressions '[% %]'
    /// </summary>
    public class ExpressionTransformer
    {
        private static readonly Regex ExpressionDetector =
            new Regex(@"\[%(?<expression>.*?)%\]",
            RegexOptions.IgnorePatternWhitespace | RegexOptions.Singleline | RegexOptions.Compiled);

        private static readonly Regex ItemValueFieldDetector =
            new Regex(@"itemValue\('(?<field>.*?)'\)",
            RegexOptions.IgnorePatternWhitespace | RegexOptions.Singleline | RegexOptions.Compiled);

        private static readonly Regex ResourceFieldDetector =
            new Regex(@"resource\('(?<resource>.*?)'\)",
            RegexOptions.IgnorePatternWhitespace | RegexOptions.Singleline | RegexOptions.Compiled);

        public static string RenderExpressions(string content, string baseUrl, Dictionary<string, object> data = null)
        {
            return ExpressionDetector.Replace(content, expressionMatch =>
            {
                var expression = expressionMatch.Groups["expression"].Value;

                expression = ItemValueFieldDetector.Replace(expression, expressionMatch =>
                {
                    var field = expressionMatch.Groups["field"].Value;
                    return (data?[field] ?? "").ToString();
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
        public void RenderTemplate(TextReader template, TextWriter output, string baseUrl)
        {
            var document = new HtmlDocument();
            document.Load(template);

            HtmlNode[] repeaterNodes = document.DocumentNode.SelectNodes("//*[name()='sg:repeater']")?.ToArray() ?? Array.Empty<HtmlNode>();
            foreach (var repeaterNode in repeaterNodes)
            {
                HtmlNode[] repeaterItemNodes = repeaterNode.SelectNodes("//*[name()='sg:repeateritem']")?.ToArray() ?? Array.Empty<HtmlNode>();

                foreach (var repeaterItemNode in repeaterItemNodes)
                {
                    var dataSelection = repeaterNode.Attributes["dataselection"]?.Value;
                    var repeaterItemContent = repeaterItemNode.InnerHtml;

                    var repeatedContent = new StringBuilder();
                    // Modify the following logic based on your specific requirements
                    var dataItem = new Dictionary<string, object>(); // Replace this with your data retrieval logic
                    var result = ExpressionTransformer.RenderExpressions(repeaterItemContent, baseUrl, dataItem);

                    repeatedContent.Append(result);

                    ReplaceHtml(repeaterNode, repeatedContent);
                }
            }

            HtmlNode[] imageNodes = document.DocumentNode.SelectNodes("//img")?.ToArray() ?? Array.Empty<HtmlNode>();
            foreach (var imageNode in imageNodes)
            {
                var srcAttributeValue = imageNode.Attributes["src"]?.Value;
                if (srcAttributeValue != null)
                {
                    var result = ExpressionTransformer.RenderExpressions(srcAttributeValue, baseUrl);
                    imageNode.Attributes["src"].Value = result;
                }
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
                parent?.AppendChild(child);
            }
        }
    }
}
