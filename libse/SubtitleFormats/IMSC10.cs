using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace Nikse.SubtitleEdit.Core.SubtitleFormats
{
    public class IMSC10 : TimedText10
    {
        override public string Extension => ".xml";
        override public string Name => "IMSC 1.0";

        public static string IMSCStylingNamespace = "http://www.w3.org/ns/ttml/profile/imsc1#styling";
        public static string IMSCParameterNamespace = "http://www.w3.org/ns/ttml/profile/imsc1#parameter";
        public static string IMSCMetadataNamespace = "http://www.w3.org/ns/ttml/profile/imsc1#metadata";

        private static XmlNamespaceManager namespaceManager;

        private XmlNamespaceManager NamespaceManager
        {
            get
            {
                if (namespaceManager == null)
                {
                    NameTable nameTable = new NameTable();
                    namespaceManager = new XmlNamespaceManager(nameTable);

                    namespaceManager.AddNamespace("ttml", TTMLNamespace);
                    namespaceManager.AddNamespace("tts", TTMLStylingNamespace);
                    namespaceManager.AddNamespace("ttp", TTMLParameterNamespace);
                    namespaceManager.AddNamespace("ttm", TTMLMetadataNamespace);

                    namespaceManager.AddNamespace("itts", IMSCStylingNamespace);
                    namespaceManager.AddNamespace("ittp", IMSCParameterNamespace);
                    namespaceManager.AddNamespace("ittm", IMSCMetadataNamespace);
                }

                return namespaceManager;
            }
        }

        // Primary interface

        public override void LoadSubtitle(Subtitle subtitle, List<string> lines, string fileName)
        {
            base.LoadSubtitle(subtitle, lines, fileName);

            foreach (Paragraph p in subtitle.Paragraphs)
            {
                bool forcedDisplay = false;
                bool.TryParse(GetEffect(p, "itts:forcedDisplay"), out forcedDisplay);
                p.Forced = forcedDisplay;
            }
        }

        public override string ToText(Subtitle subtitle, string title)
        {
            XmlDocument resultXml = GenerateDefaultHeader(title);
            XmlElement div = resultXml.DocumentElement.SelectSingleNode("ttml:body/ttml:div", NamespaceManager) as XmlElement;

            for (int pNum = 0; pNum < subtitle.Paragraphs.Count; pNum++)
            {
                Paragraph p = subtitle.Paragraphs[pNum];
                string pId = "p" + pNum;
                XmlElement pElement = GenerateParagraph(p, pId, resultXml);

                div.AppendChild(pElement);
            }

            return ToUtf8XmlString(resultXml);
        }

        public static new string SetExtra(Paragraph p)
        {
            string style = string.IsNullOrEmpty(p.Style) ? "-" : p.Style;
            string lang = string.IsNullOrEmpty(p.Language) ? "-" : p.Language;
            string forced = p.Forced ? "y" : "n";

            return string.Format("{0} / {1} / {2}", style, lang, forced);
        }



        private XmlElement GenerateParagraph(Paragraph paragraph, string id, XmlDocument doc)
        {
            XmlElement pNode = doc.CreateElement("p", TTMLNamespace);

            // Text
            string text = paragraph.Text;
            text = RemoverRegionTag(text);

            try     // Try to convert to ttml
            {
                string textWithLineBreaks = string.Join("<br/>", text.SplitToLines());
                XmlDocument paragraphContent = new XmlDocument();
                paragraphContent.LoadXml(string.Format("<root>{0}</root>", textWithLineBreaks));
                ConvertParagraphNodeToTTMLNode(paragraphContent.DocumentElement, doc, pNode);
            }
            catch  // Wrong markup, clear it
            {
                string textWithoutMaukup = Regex.Replace(text, "[<>]", "");
                XmlText textNode = doc.CreateTextNode(textWithoutMaukup);
                pNode.AppendChild(textNode);
            }

            // Common attributes
            pNode.SetAttribute("xml:id", id);
            pNode.SetAttribute("begin", ConvertToTimeString(paragraph.StartTime));
            pNode.SetAttribute("end", ConvertToTimeString(paragraph.EndTime));

            // Forced display
            if (paragraph.Forced)
            {
                pNode.SetAttribute("itts:forcedDisplay", "true");
            }

            // Style
            if (GetStyles(doc).Contains(paragraph.Style))
            {
                pNode.SetAttribute("style", paragraph.Style);
            }
            else    // !!! The style isn't exists. Warning?
            {
                pNode.SetAttribute("style", paragraph.Style);
            }

            // Region
            string region = GetEffect(paragraph, "region");

            if (RegionTagExists(paragraph.Text))
            {
                SetEffect(paragraph, "region", "");
                region = ConvertRegionTagToRegionName(GetRegionTag(paragraph.Text));
            }

            if (!string.IsNullOrEmpty(region) && AddDefaultRegionIfNotExists(doc, region))
            {
                pNode.SetAttribute("region", region);
            }
            else    // !!! The region neither in the header, nor deafult. Warning?
            {
                pNode.SetAttribute("region", region);
            }

            List<string> attributesToRemove = new List<string>() { "id" };

            GetAllEffects(paragraph)
                .Where(eff => !string.IsNullOrEmpty(eff.Value))
                .Where(eff => pNode.Attributes[eff.Key] == null)
                .Where(eff => !attributesToRemove.Contains(eff.Key))
                .ToList()
                .ForEach(eff => pNode.SetAttribute(eff.Key, eff.Value));

            return pNode;
        }

        private XmlDocument GenerateDefaultHeader(string title)
        {
            XmlDocument doc = new XmlDocument();

            XmlDeclaration xmlDeclaration = doc.CreateXmlDeclaration("1.0", "utf-8", string.Empty);
            doc.AppendChild(xmlDeclaration);

            // Root
            XmlElement root = doc.CreateElement("tt", TTMLNamespace);

            root.SetAttribute("xmlns:ttm", TTMLMetadataNamespace);
            root.SetAttribute("xmlns:tts", TTMLStylingNamespace);
            root.SetAttribute("xmlns:ttp", TTMLParameterNamespace);
            root.SetAttribute("xmlns:ittm", IMSCMetadataNamespace);
            root.SetAttribute("xmlns:itts", IMSCStylingNamespace);
            root.SetAttribute("xmlns:ittp", IMSCParameterNamespace);

            root.SetAttribute("xml:lang", "en");

            doc.AppendChild(root);

            // Head
            XmlElement head = doc.CreateElement("head", TTMLNamespace);
            doc.DocumentElement.AppendChild(head);

            XmlElement metadata = doc.CreateElement("metadata", TTMLNamespace);
            head.AppendChild(metadata);

            XmlElement titleEl = doc.CreateElement("ttm", "title", TTMLMetadataNamespace);
            titleEl.InnerText = title;
            metadata.AppendChild(titleEl);

            XmlElement styling = doc.CreateElement("styling", TTMLNamespace);
            head.AppendChild(styling);

            XmlElement layout = doc.CreateElement("layout", TTMLNamespace);
            head.AppendChild(layout);

            // Body
            XmlElement body = doc.CreateElement("body", TTMLNamespace);
            root.AppendChild(body);

            XmlElement div = doc.CreateElement("div", TTMLNamespace);
            body.AppendChild(div);

            return doc;
        }

        private XmlDocument ImportHeader(string header)
        {
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(header);

            // Doc must contains root elemetn
            XmlElement root = doc.DocumentElement;

            if (root == null || root.Value != "tt")
            {
                throw new HeaderImportException();
            }

            VerifyLangAttr(root);

            // Head
            XmlElement head = root.SelectSingleNode("ttml:head", NamespaceManager) as XmlElement;

            if (head == null)
            {
                head = doc.CreateElement("head", TTMLNamespace);
                root.AppendChild(head);
            }

            return doc;
        }

        private void VerifyLangAttr(XmlElement node)
        {
            // Rename lang to xml:lang
            if (node.HasAttribute("lang"))
            {
                node.SetAttribute("xml:lang", node.GetAttribute("lang"));
                node.RemoveAttribute("lang");
            }

            // Lang must be defined(empty at least)
            if (!node.HasAttribute("xml:lang"))
            {
                node.SetAttribute("xml:lang", "");
            }
        }

        private XmlElement AddChildIfNotExists(XmlNode parent, string prefix, string name, string ns)
        {
            NameTable nt = new NameTable();
            XmlNamespaceManager nsmgr = new XmlNamespaceManager(nt);
            nsmgr.AddNamespace("ns", ns);

            XmlNode node = parent.SelectSingleNode(ns + "");
        }

        // Region helpers

        public static bool RegionTagExists(string s)
        {
            return Regex.IsMatch(s, @"^({\\an[1-9]})");
        }

        public static string RemoveRegionTag(string s)
        {
            return Regex.Replace(s, @"^({\\an[1-9]})", "");
        }

        public static string GetRegionTag(string s)
        {
            return Regex.Match(s, @"^({\\an[1-9]})").Value;
        }

        private string ConvertRegionTagToRegionName(string tag)
        {
            switch (tag)
            {
                case @"{\an1}": return "bottomLeft";
                case @"{\an2}": return "bottomCenter";
                case @"{\an3}": return "bottomRight";
                case @"{\an4}": return "centerLeft";
                case @"{\an5}": return "centerСenter";
                case @"{\an6}": return "centerRight";
                case @"{\an7}": return "topLeft";
                case @"{\an8}": return "topCenter";
                case @"{\an9}": return "topRight";
                default: return string.Empty;
            }
        }

        // Helpers

        public static List<string> GetRegions(XmlDocument doc)
        {
            return doc.DocumentElement.SelectNodes("ttml:head//ttml:region", NamespaceManager)
                .OfType<XmlDocument>()
                .Where(r => r.Attributes["xml:id"] != null)
                .Select(r => r.Attributes["xml:id"].Value)
                .ToList();
        }

        public static List<string> GetStyles(XmlDocument doc)
        {
            return doc.DocumentElement.SelectNodes("ttml:head//ttml:style", NamespaceManager)
                .OfType<XmlDocument>()
                .Where(r => r.Attributes["xml:id"] != null)
                .Select(r => r.Attributes["xml:id"].Value)
                .ToList();
        }

        public static List<KeyValuePair<string, string>> GetAllEffects(Paragraph paragraph)
        {
            return paragraph.Effect.Split('|')
                .Select(s => s.Split('='))
                .Where(pairArr => pairArr.Length == 2)
                .Select(pairArr => new KeyValuePair<string, string>(pairArr[0], pairArr[1]))
                .ToList();
        }

    }

    class HeaderImportException : Exception
    {
        public HeaderImportException()
        {
        }

        public HeaderImportException(string message) : base(message)
        {
        }

        public HeaderImportException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected HeaderImportException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
