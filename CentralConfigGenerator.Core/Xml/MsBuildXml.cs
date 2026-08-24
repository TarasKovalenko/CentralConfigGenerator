using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace CentralConfigGenerator.Core.Xml;

/// <summary>
/// Helpers for editing MSBuild XML with as little collateral reformatting as possible.
/// </summary>
public static class MsBuildXml
{
    /// <summary>
    /// Parses MSBuild XML keeping all whitespace so untouched regions round-trip byte-for-byte.
    /// </summary>
    public static XDocument Parse(string content) =>
        XDocument.Parse(content, LoadOptions.PreserveWhitespace);

    public static bool TryParse(string content, out XDocument document, out string? error)
    {
        try
        {
            document = Parse(content);
            error = null;
            return true;
        }
        catch (XmlException ex)
        {
            document = new XDocument();
            error = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Serialises a document, keeping the original XML declaration and indentation style.
    /// </summary>
    public static string ToString(XDocument document, bool preserveWhitespace = true)
    {
        var builder = new StringBuilder();
        var settings = new XmlWriterSettings
        {
            Indent = !preserveWhitespace,
            IndentChars = "  ",
            OmitXmlDeclaration = document.Declaration is null,
            Encoding = new UTF8Encoding(false),
            NewLineHandling = NewLineHandling.None,
        };

        using (var writer = XmlWriter.Create(builder, settings))
        {
            document.Save(writer);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Removes an element along with the insignificant whitespace that preceded it, so the
    /// surrounding file does not gain blank lines.
    /// </summary>
    public static void RemoveWithWhitespace(XElement element)
    {
        if (element.PreviousNode is XText { } text && text.Value.Trim().Length == 0)
        {
            text.Remove();
        }

        element.Remove();
    }

    /// <summary>
    /// Removes an item group once it no longer has any element children.
    /// </summary>
    public static void RemoveIfEmpty(XElement? container)
    {
        if (container is null || container.Elements().Any())
        {
            return;
        }

        RemoveWithWhitespace(container);
    }

    /// <summary>
    /// Returns the indentation string used by the closest sibling, defaulting to two spaces
    /// per nesting level.
    /// </summary>
    public static string GetIndent(XElement element)
    {
        var depth = 0;
        for (var parent = element.Parent; parent is not null; parent = parent.Parent)
        {
            depth++;
        }

        if (element.PreviousNode is XText { } text)
        {
            var value = text.Value;
            var index = value.LastIndexOfAny(['\n', '\r']);
            if (index >= 0)
            {
                return value[(index + 1)..];
            }
        }

        return new string(' ', depth * 2);
    }

    /// <summary>Gets an element's attribute value case-insensitively.</summary>
    public static string? AttributeValue(XElement element, string name)
    {
        var attribute = element
            .Attributes()
            .FirstOrDefault(a =>
                string.Equals(a.Name.LocalName, name, StringComparison.OrdinalIgnoreCase)
            );

        return attribute?.Value;
    }

    public static XAttribute? Attribute(XElement element, string name) =>
        element
            .Attributes()
            .FirstOrDefault(a =>
                string.Equals(a.Name.LocalName, name, StringComparison.OrdinalIgnoreCase)
            );

    /// <summary>Finds descendants by local name, ignoring any MSBuild XML namespace.</summary>
    public static IEnumerable<XElement> Descendants(XContainer container, string localName) =>
        container
            .Descendants()
            .Where(e => string.Equals(e.Name.LocalName, localName, StringComparison.OrdinalIgnoreCase));

    public static IEnumerable<XElement> Elements(XContainer container, string localName) =>
        container
            .Elements()
            .Where(e => string.Equals(e.Name.LocalName, localName, StringComparison.OrdinalIgnoreCase));

    /// <summary>Returns the effective condition of an element, including inherited item-group conditions.</summary>
    public static string? EffectiveCondition(XElement element)
    {
        var conditions = new List<string>();

        for (var current = element; current is not null; current = current.Parent)
        {
            var condition = AttributeValue(current, "Condition");
            if (!string.IsNullOrWhiteSpace(condition))
            {
                conditions.Add(condition.Trim());
            }
        }

        conditions.Reverse();
        return conditions.Count == 0 ? null : string.Join(" AND ", conditions);
    }
}
