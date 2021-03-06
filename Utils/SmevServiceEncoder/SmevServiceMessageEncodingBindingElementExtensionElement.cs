﻿using System;
using System.Configuration;
using System.ServiceModel.Channels;
using System.ServiceModel.Configuration;

namespace Utils
{
    public class SmevServiceMessageEncodingBindingElementExtensionElement : BindingElementExtensionElement
    {
        private const string LogPathName = "logPath";

        [ConfigurationProperty(LogPathName, IsRequired = false,
            DefaultValue = "D:\\ServiceLog\\")]
        public string LogPath
        {
            get
            {
                return (string)base[LogPathName];
            }

            set
            {
                base[LogPathName] = value;
            }
        }

        protected override BindingElement CreateBindingElement()
        {
            var bindingElement =
                 new SmevServiceMessageEncodingBindingElement(LogPath);
            ApplyConfiguration(bindingElement);
            return bindingElement;
        }

        public override Type BindingElementType
        {
            get { return typeof(SmevServiceMessageEncodingBindingElement); }
        }
    }
}
