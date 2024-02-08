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
    public class ExpressionTransformer
    {
        private static readonly Regex ExpressionRegex = new Regex(@"\[% (?<expression>.*?) %\]", RegexOptions.Compiled);
        private static readonly Regex ItemValueRegex = new Regex(@"itemValue\('(?<field>.*?)'\)", RegexOptions.Compiled);
        private static readonly Regex ResourceRegex = new Regex(@"resource\('(?<resource>.*?)'\)", RegexOptions.Compiled);

        public static string RenderExpressions(string content, string baseUrl, Dictionary<string, object> data = null)
        {
            StringBuilder resultBuilder = new StringBuilder(content.Length + 100); // Set initial capacity

            int prevIndex = 0;
            foreach (Match expressionMatch in ExpressionRegex.Matches(content))
            {
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
            return ItemValueRegex.Replace(expression, match =>
            {
                string field = match.Groups["field"].Value;
                if (data != null && data.TryGetValue(field, out object value))
                {
                    // Ensure that the value is not null before converting to string
                    return value?.ToString() ?? "";
                }
                return match.Value;
            });
        }


        private static string ReplaceResourceFields(string expression, string baseUrl)
        {
            return ResourceRegex.Replace(expression, match =>
            {
                string resource = match.Groups["resource"].Value;
                return $"{baseUrl}/{resource}"; // Properly concatenate baseUrl and resource
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
                .OfType<Dictionary<string, object>>()
                .Where(x => x.TryGetValue("name", out object name) && "baseurl".Equals(name.ToString()))
                .Select(x => x["value"]?.ToString())
                .FirstOrDefault();

            var repeaterNodes = document.DocumentNode.Descendants("sg:repeater").ToArray();
            foreach (var repeaterNode in repeaterNodes)
            {
                var repeaterItemNodes = repeaterNode.Descendants("sg:repeateritem").ToArray();
                foreach (var repeaterItemNode in repeaterItemNodes)
                {
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

            var imageNodes = document.DocumentNode.Descendants("img").ToArray();
            foreach (var imageNode in imageNodes)
            {
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
            foreach (var child in repeatedNodes)
            {
                parent.AppendChild(child);
            }
        }
    }

}