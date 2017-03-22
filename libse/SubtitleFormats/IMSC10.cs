using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

                    namespaceManager.AddNamespace("itts", IMSCStylingNamespace);
                    namespaceManager.AddNamespace("ittp", IMSCParameterNamespace);
                    namespaceManager.AddNamespace("ittm", IMSCMetadataNamespace);
                }

                return namespaceManager;
            }
        }


        public override void LoadSubtitle(Subtitle subtitle, List<string> lines, string fileName)
        {
            base.LoadSubtitle(subtitle, lines, fileName);
        }

        public override string ToText(Subtitle subtitle, string title)
        {
            XmlDocument resultXml = new XmlDocument();

            XmlDeclaration xmlDeclaration = resultXml.CreateXmlDeclaration("1.0", "utf-8", string.Empty);
            resultXml.AppendChild(xmlDeclaration);

            // Root
            XmlElement root = resultXml.CreateElement("tt", TTMLNamespace);

            root.SetAttribute("xmlns:ttm", TTMLMetadataNamespace);
            root.SetAttribute("xmlns:tts", TTMLStylingNamespace);
            root.SetAttribute("xmlns:ttp", TTMLParameterNamespace);
            root.SetAttribute("xmlns:ittm", IMSCMetadataNamespace);
            root.SetAttribute("xmlns:itts", IMSCStylingNamespace);
            root.SetAttribute("xmlns:ittp", IMSCParameterNamespace);

            root.SetAttribute("xml:lang", "en");

            resultXml.AppendChild(root);

            // Head
            XmlElement head = resultXml.CreateElement("head", TTMLNamespace);
            resultXml.DocumentElement.AppendChild(head);

            XmlElement metadata = resultXml.CreateElement("metadata", TTMLNamespace);
            head.AppendChild(metadata);

            XmlElement titleEl = resultXml.CreateElement("ttm", "title", TTMLMetadataNamespace);
            titleEl.InnerText = title;
            metadata.AppendChild(titleEl);

            XmlElement styling = resultXml.CreateElement("styling", TTMLNamespace);
            head.AppendChild(styling);

            XmlElement layout = resultXml.CreateElement("layout", TTMLNamespace);
            head.AppendChild(layout);

            // Body
            XmlElement body = resultXml.CreateElement("body", TTMLNamespace);
            root.AppendChild(body);

            XmlElement div = resultXml.CreateElement("div", TTMLNamespace);
            body.AppendChild(div);

            for (int pNum = 0; pNum < subtitle.Paragraphs.Count; pNum++)
            {
                Paragraph paragrap = subtitle.Paragraphs[pNum];
                XmlElement paragraphNode = resultXml.CreateElement("p", TTMLNamespace);

                try     // Try to convert pararagraph to ttml
                {
                    string text = string.Join("<br/>", paragrap.Text.SplitToLines());
                    XmlDocument paragraphContent = new XmlDocument();
                    paragraphContent.LoadXml(string.Format("<root>{0}</root>", text));
                    ConvertParagraphNodeToTTMLNode(paragraphContent.DocumentElement, resultXml, paragraphNode);
                }
                catch  // Wrong markup, clear it
                {
                    string text = Regex.Replace(paragrap.Text, "[<>]", "");
                    XmlText textNode = resultXml.CreateTextNode(paragrap.Text);
                    paragraphNode.AppendChild(textNode);
                }

                XmlAttribute start = resultXml.CreateAttribute("begin");
                start.InnerText = ConvertToTimeString(paragrap.StartTime);
                paragraphNode.Attributes.Append(start);

                XmlAttribute id = resultXml.CreateAttribute("xml:id");
                id.InnerText = "p" + pNum;
                paragraphNode.Attributes.Append(id);

                XmlAttribute end = resultXml.CreateAttribute("end");
                end.InnerText = ConvertToTimeString(paragrap.EndTime);
                paragraphNode.Attributes.Append(end);

                div.AppendChild(paragraphNode);
            }

            return XmlDocumentToString(resultXml);
        }

        public string XmlDocumentToString(XmlDocument doc)
        {
            using (var stringWriter = new StringWriter())
            using (var xmlTextWriter = XmlWriter.Create(stringWriter))
            {
                doc.WriteTo(xmlTextWriter);
                xmlTextWriter.Flush();
                return stringWriter.GetStringBuilder().ToString();
            }
        }

        public static new string SetExtra(Paragraph p)
        {
            string style = string.IsNullOrEmpty(p.Style) ? "-" : p.Style;
            string lang = string.IsNullOrEmpty(p.Language) ? "-" : p.Language;
            string forced = p.Forced ? "y" : "n";

            return string.Format("{0} / {1} / {2}", style, lang, forced);
        }
    }
}
