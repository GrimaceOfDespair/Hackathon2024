using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.Linq;
using System.Xml;
using System.Xml.XPath;

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
    private const string repeaterXPath = "//*[name()='sg:repeater']";
    private const string repeaterItemXPath = ".//*[name()='sg:repeateritem']";
    private const string imgXPath = "//img";

    public void RenderTemplate(TextReader template, TextWriter output, Dictionary<string, Dictionary<string, object>[]> allData)
    {
        var baseUrl = GetBaseUrl(allData);

        using (var reader = new HtmlReader(template))
        {
            var writer = new HtmlWriter(output);

            while (reader.Read())
            {
                if (reader.NodeType == HtmlNodeType.Element && reader.LocalName == "sg:repeater")
                {
                    ProcessRepeater(reader, writer, baseUrl, allData);
                }
                else if (reader.NodeType == HtmlNodeType.Element && reader.LocalName == "img")
                {
                    ProcessImage(reader, writer, baseUrl);
                }
                else
                {
                    writer.WriteNode(reader, true);
                }
            }
        }
    }

    private void ProcessRepeater(HtmlReader reader, HtmlWriter writer, string baseUrl, Dictionary<string, Dictionary<string, object>[]> allData)
    {
        using (var subReader = reader.ReadSubtree())
        {
            var repeaterNode = XElement.Load(subReader);

            var dataSelection = repeaterNode.GetAttributeValue("dataselection", "");
            var repeaterItemNodes = repeaterNode.XPathSelectElements(repeaterItemXPath);

            if (allData.TryGetValue(dataSelection, out var data))
            {
                foreach (var dataItem in data)
                {
                    foreach (var repeaterItemNode in repeaterItemNodes)
                    {
                        var repeaterItemContent = repeaterItemNode.InnerHtml;
                        var result = ExpressionTransformer.RenderExpressions(repeaterItemContent, baseUrl, dataItem);
                        writer.WriteRaw(result);
                    }
                }
            }
        }
    }

    private void ProcessImage(HtmlReader reader, HtmlWriter writer, string baseUrl)
    {
        var srcAttributeValue = reader.GetAttribute("src");
        var result = ExpressionTransformer.RenderExpressions(srcAttributeValue, baseUrl);
        reader.SetAttribute("src", result);
        writer.WriteNode(reader, true);
    }

    private string GetBaseUrl(Dictionary<string, Dictionary<string, object>[]> allData)
    {
        if (allData.TryGetValue("variables", out var variables))
        {
            foreach (var variable in variables)
            {
                if (variable.TryGetValue("name", out var name) && "baseurl".Equals(name?.ToString(), StringComparison.OrdinalIgnoreCase) && variable.TryGetValue("value", out var value))
                {
                    return value?.ToString() ?? "";
                }
            }
        }
        return "";
    }

    private class HtmlReader : HtmlNodeReader
    {
        public HtmlReader(TextReader reader) : base(reader)
        {
        }

        protected override bool Read()
        {
            bool result = base.Read();

            if (NodeType == HtmlNodeType.Text && string.IsNullOrWhiteSpace(Value))
            {
                // Ignore whitespace-only text nodes
                result = Read();
            }

            return result;
        }
    }

    private class HtmlWriter : HtmlNodeWriter
    {
        public HtmlWriter(TextWriter writer) : base(writer)
        {
        }
    }
}