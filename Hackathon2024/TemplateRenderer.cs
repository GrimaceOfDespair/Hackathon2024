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
            new Regex(@"\[%(?<expression>[^%]+)%\]",
            RegexOptions.Singleline | RegexOptions.Compiled, TimeSpan.FromSeconds(15));

        private static readonly Regex ItemValueFieldDetector =
            new Regex(@"itemValue\('(?<field>[^']+?)'\)",
            RegexOptions.Singleline | RegexOptions.Compiled, TimeSpan.FromSeconds(15));

        private static readonly Regex ResourceFieldDetector =
            new Regex(@"resource\('(?<resource>[^']+?)'\)",
            RegexOptions.Singleline | RegexOptions.Compiled, TimeSpan.FromSeconds(15));

        public static string RenderExpressions(string content, string baseUrl, Dictionary<string, object> data = null)
        {
            return ExpressionDetector.Replace(content, expressionMatch =>
            {
                var expression = expressionMatch.Groups["expression"].Value;

                expression = ItemValueFieldDetector.Replace(expression, fieldMatch =>
                {
                    var field = fieldMatch.Groups["field"].Value;

                    return (data != null && data.TryGetValue(field, out var value)) ? value.ToString() : string.Empty;
                });

                expression = ResourceFieldDetector.Replace(expression, resourceMatch =>
                {
                    var resource = resourceMatch.Groups["resource"].Value;

                    return $"{baseUrl}{resource}";
                });

                return expression;
            });
        }
    }

    public class TemplateRenderer
    {
        public void RenderTemplate(TextReader template, TextWriter output, Dictionary<string, Dictionary<string, object>[]> allData)
        {
            var document = new HtmlDocument();
            document.Load(template);

            var baseUrl = allData["variables"]
                .Where(x => x.TryGetValue("name", out var name) && "baseurl".Equals(name.ToString()))
                .Select(x => x["value"]?.ToString() ?? "")
                .FirstOrDefault();

            HtmlNode[] repeaterNodes = document.DocumentNode.SelectNodes("//*[name()='sg:repeater']")?.ToArray() ?? Array.Empty<HtmlNode>();
            for (int i = 0; i < repeaterNodes.Length; i++)
            {
                var repeaterNode = repeaterNodes[i];
                HtmlNode[] repeaterItemNodes = repeaterNode.SelectNodes(".//*[name()='sg:repeateritem']")?.ToArray() ?? Array.Empty<HtmlNode>();

                for (int j = 0; j < repeaterItemNodes.Length; j++)
                {
                    var repeaterItemNode = repeaterItemNodes[j];
                    var dataSelection = repeaterNode.GetAttributeValue("dataselection", "");
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
            for (int k = 0; k < imageNodes.Length; k++)
            {
                var imageNode = imageNodes[k];
                var srcAttributeValue = imageNode.GetAttributeValue("src", "");

                var result = ExpressionTransformer.RenderExpressions(srcAttributeValue, baseUrl);
                imageNode.SetAttributeValue("src", result);
            }

            document.Save(output);
        }

        private static void ReplaceHtml(HtmlNode repeaterNode, StringBuilder repeatedContent)
        {
            repeaterNode.InnerHtml = repeatedContent.ToString();

            var repeatedNodes = repeaterNode.ChildNodes.ToArray();
            var parent = repeaterNode.ParentNode;
            repeaterNode.Remove();

            for (int i = 0; i < repeatedNodes.Length; i++)
            {
                var child = repeatedNodes[i];
                parent.AppendChild(child);
            }
        }
    }
}
