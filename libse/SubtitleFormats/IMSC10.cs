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
            XmlDocument resultXml = GenerateTemplate(title);
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

        private XmlElement GenerateParagraph(Paragraph paragraph, string id, XmlDocument doc)
        {
            XmlElement pNode = doc.CreateElement("p", TTMLNamespace);

            try     // Try to convert pararagraph to ttml
            {
                string text = string.Join("<br/>", paragraph.Text.SplitToLines());
                XmlDocument paragraphContent = new XmlDocument();
                paragraphContent.LoadXml(string.Format("<root>{0}</root>", text));
                ConvertParagraphNodeToTTMLNode(paragraphContent.DocumentElement, doc, pNode);
            }
            catch  // Wrong markup, clear it
            {
                string text = Regex.Replace(paragraph.Text, "[<>]", "");
                XmlText textNode = doc.CreateTextNode(paragraph.Text);
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

            List<string> attributesToRemove = new List<string>() { "id" };

            GetAllEffects(paragraph)
                .Where(eff => pNode.Attributes[eff.Key] == null)
                .Where(eff => !attributesToRemove.Contains(eff.Key))
                .ToList()
                .ForEach(eff => pNode.SetAttribute(eff.Key, eff.Value));

            return pNode;
        }

        private XmlDocument GenerateTemplate(string title)
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

            return resultXml;
        }

        public static List<KeyValuePair<string, string>> GetAllEffects(Paragraph paragraph)
        {
            return paragraph.Effect.Split('|')
                .Select(s => s.Split('='))
                .Where(pairArr => pairArr.Length == 2)
                .Select(pairArr => new KeyValuePair<string, string>(pairArr[0], pairArr[1]))
                .ToList();
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
