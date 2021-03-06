﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Xml;

namespace Utils
{
    public static class Signer
    {
        public static string SignMessage(string message, string certificateID, X509Certificate2 certificate)
        {
            var document = new XmlDocument
            {
                PreserveWhitespace = false
            };
            document.LoadXml(message);

            // Добавляем XmlDeclaration -не думаю что это необходимо
            if (!(document.FirstChild is XmlDeclaration))
            {
                var declaration = document.CreateXmlDeclaration("1.0", "UTF-8", string.Empty);
                document.InsertBefore(declaration, document.FirstChild);
            }

            // Добавляем необходимые неймспейсы
            var ns = new XmlNamespaceManager(document.NameTable);
            ns.AddNamespace("s", "http://schemas.xmlsoap.org/soap/envelope/");
            var envelope = document.DocumentElement.SelectSingleNode(@"//s:Envelope", ns) as XmlElement;
            if (envelope == null)
            {
                throw new ApplicationException("Не найден тэг Envelope");
            }
            envelope.SetAttribute("xmlns:wsse", "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd");
            envelope.SetAttribute("xmlns:wsu", "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd");
            envelope.SetAttribute("xmlns:ds", "http://www.w3.org/2000/09/xmldsig#");

            // Ищем body и добавляем ему wsu:Id 
            var body = document.DocumentElement.SelectSingleNode(@"//s:Body", ns) as XmlElement;
            if (body == null)
                throw new ApplicationException("Не найден тэг Body");
            body.RemoveAllAttributes();
            var id = document.CreateAttribute("wsu", "Id",
                                                      "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd");
            id.InnerText = "body";
            body.Attributes.Append(id);

            // Получаем подпись
            var mySignedXml = new SmevSignedXml(document)
            {
                SigningKey = certificate.PrivateKey
            };
            var reference = new Reference
            {
                Uri = "#body",
                DigestMethod = "http://www.w3.org/2001/04/xmldsig-more#gostr3411"
            };
            var excC14Ntransform = new XmlDsigExcC14NTransform();
            reference.AddTransform(excC14Ntransform);
            mySignedXml.AddReference(reference);
            var keyInfo = new KeyInfo();
            keyInfo.AddClause(new KeyInfoX509Data(certificate));
            mySignedXml.KeyInfo = keyInfo;
            mySignedXml.SignedInfo.CanonicalizationMethod = excC14Ntransform.Algorithm;
            mySignedXml.SignedInfo.SignatureMethod = "http://www.w3.org/2001/04/xmldsig-more#gostr34102001-gostr3411";
            mySignedXml.ComputeSignature("ds");

            // Вставляем подпись в сообщение
            var namespaceManager = GetNewFilledNamespaceManager(document.NameTable);
            document.SelectSingleNode("/s:Envelope", namespaceManager).InsertBefore(document.CreateNode(XmlNodeType.Element, "s", "Header", namespaceManager.LookupNamespace("s")), document.SelectSingleNode("/s:Envelope/s:Body", namespaceManager));
            document.SelectSingleNode("/s:Envelope/s:Header", namespaceManager).AppendChild(document.CreateNode(XmlNodeType.Element, "wsse", "Security", namespaceManager.LookupNamespace("wsse"))).Attributes.SetNamedItem(document.CreateNode(XmlNodeType.Attribute, "s", "actor", namespaceManager.LookupNamespace("s"))).InnerText = "http://smev.gosuslugi.ru/actors/smev";
            document.SelectSingleNode("/s:Envelope/s:Header/wsse:Security", namespaceManager).AppendChild(document.CreateNode(XmlNodeType.Element, "wsse", "BinarySecurityToken", namespaceManager.LookupNamespace("wsse")));
            document.SelectSingleNode("/s:Envelope/s:Header/wsse:Security", namespaceManager).AppendChild(document.CreateNode(XmlNodeType.Element, "ds", "Signature", namespaceManager.LookupNamespace("ds")));
            document.SelectSingleNode("/s:Envelope/s:Header/wsse:Security/wsse:BinarySecurityToken", namespaceManager).Attributes.SetNamedItem(document.CreateNode(XmlNodeType.Attribute, "EncodingType", "")).InnerText = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-soap-message-security-1.0#Base64Binary";
            document.SelectSingleNode("/s:Envelope/s:Header/wsse:Security/wsse:BinarySecurityToken", namespaceManager).Attributes.SetNamedItem(document.CreateNode(XmlNodeType.Attribute, "ValueType", "")).InnerText = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-x509-token-profile-1.0#X509v3";
            document.SelectSingleNode("/s:Envelope/s:Header/wsse:Security/wsse:BinarySecurityToken", namespaceManager).Attributes.SetNamedItem(document.CreateNode(XmlNodeType.Attribute, "wsu", "Id", namespaceManager.LookupNamespace("wsu"))).InnerText = certificateID;
            document.SelectSingleNode("/s:Envelope/s:Header/wsse:Security/wsse:BinarySecurityToken", namespaceManager).InnerText = mySignedXml.GetXml("ds").SelectSingleNode("/ds:KeyInfo/ds:X509Data/ds:X509Certificate", namespaceManager).InnerText;
            document.SelectSingleNode("/s:Envelope/s:Header/wsse:Security/ds:Signature", namespaceManager).AppendChild(document.ImportNode(mySignedXml.GetXml("ds").SelectSingleNode("/ds:SignedInfo", namespaceManager), true));
            document.SelectSingleNode("/s:Envelope/s:Header/wsse:Security/ds:Signature", namespaceManager).AppendChild(document.ImportNode(mySignedXml.GetXml("ds").SelectSingleNode("/ds:SignatureValue", namespaceManager), true));
            document.SelectSingleNode("/s:Envelope/s:Header/wsse:Security/ds:Signature", namespaceManager).AppendChild(document.CreateNode(XmlNodeType.Element, "ds", "KeyInfo", namespaceManager.LookupNamespace("ds"))).AppendChild(document.CreateNode(XmlNodeType.Element, "wsse", "SecurityTokenReference", namespaceManager.LookupNamespace("wsse"))).AppendChild(document.CreateNode(XmlNodeType.Element, "wsse", "Reference", namespaceManager.LookupNamespace("wsse"))).Attributes.SetNamedItem(document.CreateNode(XmlNodeType.Attribute, "URI", "")).InnerText = "#" + certificateID;
            document.SelectSingleNode("/s:Envelope/s:Header/wsse:Security/ds:Signature/ds:KeyInfo/wsse:SecurityTokenReference/wsse:Reference", namespaceManager).Attributes.SetNamedItem(document.CreateNode(XmlNodeType.Attribute, "ValueType", "")).InnerText = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-x509-token-profile-1.0#X509v3";

            message = document.OuterXml;
            message = message.Replace("<s:Header />", "");
            return message;
        }


        private static XmlNamespaceManager GetNewFilledNamespaceManager(XmlNameTable xmlNameTable)
        {
            var namespaceManager = new XmlNamespaceManager(xmlNameTable);
            namespaceManager.AddNamespace("s", "http://schemas.xmlsoap.org/soap/envelope/");
            namespaceManager.AddNamespace("wsse", "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd");
            namespaceManager.AddNamespace("wsu", "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd");
            namespaceManager.AddNamespace("smev", "http://smev.gosuslugi.ru/rev120315");
            namespaceManager.AddNamespace("ds", "http://www.w3.org/2000/09/xmldsig#");
            return namespaceManager;
        }

        public static bool CheckSignature(XmlDocument doc)
        {
            var nodeList = doc.GetElementsByTagName(
               "Security", "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd");

            for (var curSignature = 0; curSignature < nodeList.Count; curSignature++)
            {
                // Создаем объект SignedXml для проверки подписи документа.
                var signedXml = new SmevSignedXml(doc);

                // Вытаскиваем сертификат из подписи
                var certificateNode = ((XmlElement)(nodeList[curSignature])).GetElementsByTagName("BinarySecurityToken",
                                                                                               "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd");
                var rawCertData = Convert.FromBase64String(certificateNode[0].InnerText);
                var certificate = new X509Certificate2(rawCertData);

                var signature = ((XmlElement)nodeList[curSignature]).GetElementsByTagName("Signature", SignedXml.XmlDsigNamespaceUrl)[0];
                signedXml.LoadXml((XmlElement)signature);

                

                // Проверяем подпись
                var result = signedXml.CheckSignature(certificate, false);

                if (!result)
                    return false;
            }

            return true;
        }
    }
}
