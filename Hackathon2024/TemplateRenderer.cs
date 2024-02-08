namespace Hackathon2024
{
    using HtmlAgilityPack;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using Data = System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, object>[]>;


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
        private static readonly Regex ExpressionDetector = new Regex(
            @"\[%(?<expression>.*?)%\]",
            RegexOptions.IgnorePatternWhitespace | RegexOptions.Singleline | RegexOptions.Compiled, TimeSpan.FromSeconds(15));

        private static readonly Regex ItemValueFieldDetector = new Regex(
            @"itemValue\('(?<field>.*?)'\)",
            RegexOptions.IgnorePatternWhitespace | RegexOptions.Singleline | RegexOptions.Compiled, TimeSpan.FromSeconds(15));

        private static readonly Regex ResourceFieldDetector = new Regex(
            @"resource\('(?<resource>.*?)'\)",
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
        public void RenderTemplate(TextReader template, TextWriter output, Data allData)
        {
            var document = new HtmlDocument();
            document.Load(template);

            var baseUrl = allData["variables"]
                .Where(x =>
                    x.TryGetValue("name", out object name) &&
                    "baseurl".Equals(name.ToString()))
                .Select(x =>
                    x.TryGetValue("value", out var value) ? value.ToString() : "")
                .FirstOrDefault();

            var repeaterNodes = document.DocumentNode.SelectNodes("//*[name()='sg:repeater']");
            if (repeaterNodes != null)
            {
                Parallel.ForEach(repeaterNodes, repeaterNode =>
                {
                    var repeaterItemNodes = repeaterNode.SelectNodes("//*[name()='sg:repeateritem']");
                    if (repeaterItemNodes != null)
                    {
                        Parallel.ForEach(repeaterItemNodes, repeaterItemNode =>
                        {
                            var dataSelection = repeaterNode.Attributes["dataselection"].Value;
                            var repeaterItemContent = repeaterItemNode.InnerHtml;

                            var repeatedContent = new List<string>();
                            foreach (var dataItem in allData[dataSelection])
                            {
                                var result = ExpressionTransformer.RenderExpressions(repeaterItemContent, baseUrl, dataItem);
                                repeatedContent.Add(result);
                            }

                            ReplaceHtml(repeaterNode, repeatedContent);
                        });
                    }
                });
            }

            var imageNodes = document.DocumentNode.SelectNodes("//img");
            if (imageNodes != null)
            {
                foreach (var imageNode in imageNodes)
                {
                    var srcAttributeValue = imageNode.Attributes["src"].Value;
                    var result = ExpressionTransformer.RenderExpressions(srcAttributeValue, baseUrl);
                    imageNode.Attributes["src"].Value = result;
                }
            }

            document.DocumentNode.WriteTo(output);
        }

        private static void ReplaceHtml(HtmlNode repeaterNode, List<string> repeatedContent)
        {
            var capacity = repeatedContent.Sum(s => s.Length);
            var stringBuilder = new StringBuilder(capacity);

            foreach (var content in repeatedContent)
            {
                stringBuilder.Append(content);
            }

            repeaterNode.InnerHtml = stringBuilder.ToString();

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

