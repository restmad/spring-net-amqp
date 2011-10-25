﻿
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;

using Spring.Messaging.Amqp.Core;
using Spring.Objects.Factory.Support;
using Spring.Objects.Factory.Xml;
using Spring.Util;

namespace Spring.Messaging.Amqp.Rabbit.Config
{
    /// <summary>
    /// A queue parser.
    /// </summary>
    public class QueueParser : AbstractSingleObjectDefinitionParser
    {
        private static readonly string ARGUMENTS_ELEMENT = "queue-arguments";

        private static readonly string DURABLE_ATTRIBUTE = "durable";

        private static readonly string EXCLUSIVE_ATTRIBUTE = "exclusive";

        private static readonly string AUTO_DELETE_ATTRIBUTE = "auto-delete";

        protected override bool ShouldGenerateIdAsFallback
        {
            get
            {
                return true;
            }
        }

        protected override Type GetObjectType(XmlElement element)
        {
            if (NamespaceUtils.IsAttributeDefined(element, "name"))
            {
                return typeof(Queue);
            }
            else
            {
                return typeof(AnonymousQueue);
            }
        }

        protected override void DoParse(XmlElement element, ParserContext parserContext, ObjectDefinitionBuilder builder)
        {
            if (!NamespaceUtils.IsAttributeDefined(element, "name") && !NamespaceUtils.IsAttributeDefined(element, ID_ATTRIBUTE))
            {
                parserContext.ReaderContext.ReportFatalException(element, "Queue must have either id or name (or both)");
            }

            var success = NamespaceUtils.AddConstructorArgValueIfAttributeDefined(builder, element, "name");

            if (!NamespaceUtils.IsAttributeDefined(element, "name"))
            {

                if (AttributeHasIllegalOverride(element, DURABLE_ATTRIBUTE, "false")
                    || AttributeHasIllegalOverride(element, EXCLUSIVE_ATTRIBUTE, "true")
                    || AttributeHasIllegalOverride(element, AUTO_DELETE_ATTRIBUTE, "true"))
                {
                    parserContext.ReaderContext.ReportFatalException(element, "Anonymous queue cannot specify durable='true', exclusive='false' or auto-delete='false'");
                    return;
                }

            }
            else
            {
                NamespaceUtils.AddConstructorArgBooleanValueIfAttributeDefined(builder, element, DURABLE_ATTRIBUTE, false);
                NamespaceUtils.AddConstructorArgBooleanValueIfAttributeDefined(builder, element, EXCLUSIVE_ATTRIBUTE, false);
                NamespaceUtils.AddConstructorArgBooleanValueIfAttributeDefined(builder, element, AUTO_DELETE_ATTRIBUTE, false);

            }

            var argumentsElement = element.GetElementsByTagName(ARGUMENTS_ELEMENT);

            if (argumentsElement != null && argumentsElement.Count == 1)
            {
                var map = parserContext.ParserHelper.ParseCustomElement(argumentsElement[0] as XmlElement, builder.RawObjectDefinition);
                builder.AddConstructorArg(map);
            }

        }

        private bool AttributeHasIllegalOverride(XmlElement element, string name, string allowed)
        {
            var result = element.GetAttributeNode(name) != null && element.GetAttributeNode(name).Specified && !allowed.Equals(element.GetAttribute(name));
            return result;
        }
    }
}