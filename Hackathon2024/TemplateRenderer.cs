namespace Hackathon2024
{
    using HtmlAgilityPack;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
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
        public static string RenderExpressions(string content, string baseUrl, Dictionary<string, object> data = null)
        {
            if (string.IsNullOrEmpty(content))
                return content;

            var result = new StringBuilder(content.Length);
            int lastIndex = 0;

            while (true)
            {
                int startExpressionIndex = content.IndexOf("[%", lastIndex);
                if (startExpressionIndex == -1)
                    break;

                int endExpressionIndex = content.IndexOf("%]", startExpressionIndex + 2);
                if (endExpressionIndex == -1)
                    break;

                result.Append(content, lastIndex, startExpressionIndex - lastIndex);

                string expression = content.Substring(startExpressionIndex + 2, endExpressionIndex - startExpressionIndex - 2);
                expression = ReplaceItemValueField(expression, data);
                expression = ReplaceResourceField(expression, baseUrl);

                result.Append(expression);

                lastIndex = endExpressionIndex + 2;
            }

            result.Append(content, lastIndex, content.Length - lastIndex);
            return result.ToString();
        }

        private static string ReplaceItemValueField(string expression, Dictionary<string, object> data)
        {
            int startIndex = expression.IndexOf("itemValue('");
            if (startIndex == -1)
                return expression;

            int endIndex = expression.IndexOf("'", startIndex + 10);
            if (endIndex == -1)
                return expression;

            string field = expression.Substring(startIndex + 10, endIndex - startIndex - 10);
            object value = (data != null && data.TryGetValue(field, out var val)) ? val : "";
            return expression.Substring(0, startIndex) + value.ToString() + expression.Substring(endIndex + 1);
        }

        private static string ReplaceResourceField(string expression, string baseUrl)
        {
            int startIndex = expression.IndexOf("resource('");
            if (startIndex == -1)
                return expression;

            int endIndex = expression.IndexOf("'", startIndex + 10);
            if (endIndex == -1)
                return expression;

            string resource = expression.Substring(startIndex + 10, endIndex - startIndex - 10);
            return baseUrl + resource + expression.Substring(endIndex + 1);
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
                    x["value"]?.ToString() ?? "")
                .FirstOrDefault();

            HtmlNode[] repeaterNodes = document.DocumentNode.SelectNodes("//*[name()='sg:repeater']")?.ToArray() ?? Array.Empty<HtmlNode>();
            foreach (var repeaterNode in repeaterNodes)
            {
                HtmlNode[] repeaterItemNodes = repeaterNode.SelectNodes("//*[name()='sg:repeateritem']")?.ToArray() ?? Array.Empty<HtmlNode>();

                foreach (var repeaterItemNode in repeaterItemNodes)
                {
                    var dataSelection = repeaterNode.Attributes["dataselection"].Value;
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

