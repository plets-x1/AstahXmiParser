using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Web;
using System.Xml;
using Plets.Core.ControlAndConversionStructures;
using Plets.Core.Interfaces;
using Plets.Modeling.Uml;

namespace Plets.Data.Xmi {
    public class SequenceDiagramImporter : Parser {
        #region Attributes
        private List<GeneralUseStructure> listModelingStructure;
        #endregion

        #region Constructor
        public SequenceDiagramImporter () {
            listModelingStructure = new List<GeneralUseStructure> ();
        }
        #endregion

        #region Public Methods
        public override StructureCollection ParserMethod (String path, ref String name, Tuple<String, Object>[] args) {
            ResetAttributes ();
            StructureCollection model = new StructureCollection ();
            XmlDocument document = new XmlDocument ();
            document.Load (path);
            listModelingStructure.Add (FromXmi (document, ref name));
            model.listGeneralStructure.AddRange (listModelingStructure);

            return model;
        }
        #endregion

        #region Private Methods
        private GeneralUseStructure FromXmi (XmlDocument document, ref String name) {
            UmlModel model = new UmlModel ();
            XmlNamespaceManager ns = new XmlNamespaceManager (document.NameTable);
            ns.AddNamespace ("JUDE", "http://objectclub.esm.co.jp/Jude/namespace/");
            ns.AddNamespace ("UML", "org.omg.xmi.namespace.UML");
            List<UmlClass> classes = new List<UmlClass> ();
            List<String> pairs = new List<String> ();
            UmlSequenceDiagram sequenceDiagram = null;

            foreach (XmlNode classNode in document.SelectNodes ("//UML:Namespace.ownedElement//UML:Class[@xmi.id]", ns)) {
                UmlClass sdClass = new UmlClass ();
                sdClass.Id = classNode.Attributes["xmi.id"].Value;
                sdClass.Name = classNode.Attributes["name"].Value;

                foreach (XmlNode operationNode in classNode.SelectNodes ("//UML:Class[@xmi.id='" + sdClass.Id + "']//UML:Operation[@xmi.id]", ns)) {
                    UmlMethod method = new UmlMethod ();
                    method.Id = operationNode.Attributes["xmi.id"].Value;
                    method.Name = operationNode.Attributes["name"].Value;
                    method.Abstract = Boolean.Parse (operationNode.Attributes["isAbstract"].Value);

                    #region Visibility - VERIFICAR QUAL É O CORRETO
                    foreach (XmlNode modelElementVisibilityNode in operationNode.SelectNodes ("//UML:Class[@xmi.id='" + sdClass.Id + "']//UML:Operation[@xmi.id='" + method.Id + "']//UML:ModelElement.visibility", ns)) {
                        method.Visibility = modelElementVisibilityNode.Attributes["xmi.value"].Value;
                    }

                    foreach (XmlNode featureVisibilityNode in operationNode.SelectNodes ("//UML:Class[@xmi.id='" + sdClass.Id + "']//UML:Operation[@xmi.id='" + method.Id + "']//UML:Feature.visibility", ns)) {
                        method.Visibility = featureVisibilityNode.Attributes["xmi.value"].Value;
                    }
                    #endregion

                    foreach (XmlNode parameterNode in operationNode.SelectNodes ("//UML:Class[@xmi.id='" + sdClass.Id + "']//UML:Operation[@xmi.id='" + method.Id + "']//UML:BehavioralFeature.parameter//UML:Parameter", ns)) {
                        UmlMethodParam methodParam = new UmlMethodParam ();
                        methodParam.Id = parameterNode.Attributes["xmi.id"].Value;
                        methodParam.Name = parameterNode.Attributes["name"].Value;
                        methodParam.Kind = parameterNode.Attributes["kind"].Value;

                        foreach (XmlNode parameterTypeClassifierNode in parameterNode.SelectNodes ("//UML:Parameter[@xmi.id='" + methodParam.Id + "']//UML:Classifier", ns)) {
                            String aux = parameterTypeClassifierNode.Attributes["xmi.idref"].Value;
                            methodParam.Type = GetParamType (document, ns, aux);
                        }
                        method.Params.Add (methodParam);
                    }
                    sdClass.Methods.Add (method);
                }
                classes.Add (sdClass);
            }

            XmlNodeList sequenceDiagramNodeList = document.SelectNodes ("//UML:Namespace.collaboration//UML:Collaboration[@xmi.id]", ns);

            foreach (XmlNode sequenceDiagramNode in sequenceDiagramNodeList) {
                sequenceDiagram = new UmlSequenceDiagram ();
                sequenceDiagram.Id = sequenceDiagramNode.Attributes["xmi.id"].Value;
                model.AddDiagram (sequenceDiagram);
                String pair = "";

                foreach (XmlNode classifierRoleNode in sequenceDiagramNode.SelectNodes ("//UML:ClassifierRole[@xmi.id]", ns)) {
                    String aux = classifierRoleNode.Attributes["xmi.id"].Value;
                    pair = classifierRoleNode.Attributes["xmi.id"].Value;
                    pair += "#";

                    foreach (XmlNode classifierRoleBaseNode in classifierRoleNode.SelectNodes ("//UML:ClassifierRole[@xmi.id='" + aux + "']//UML:ClassifierRole.base//UML:Classifier[@xmi.idref]", ns)) {
                        pair += classifierRoleBaseNode.Attributes["xmi.idref"].Value;
                    }
                    pairs.Add (pair);
                }
            }

            foreach (UmlClass sdClass in classes) {
                foreach (String pair in pairs) {
                    String[] splitted = pair.Split ('#');
                    if (sdClass.Id.Equals (splitted[1])) {
                        sdClass.IdRef = splitted[0];
                        break;
                    }
                }
                foreach (UmlMethod method in sdClass.Methods) {
                    sequenceDiagram.UmlObjects.Add (method);
                    foreach (UmlMethodParam param in method.Params) {
                        sequenceDiagram.UmlObjects.Add (param);
                    }
                }
                sequenceDiagram.UmlObjects.Add (sdClass);
            }

            foreach (UmlSequenceDiagram sequenceDiagramAux in model.Diagrams.OfType<UmlSequenceDiagram> ()) {
                XmlNodeList messageNodeList = document.SelectNodes ("//UML:Namespace.collaboration//UML:Collaboration[@xmi.id='" + sequenceDiagramAux.Id + "']//UML:Message[@xmi.id]", ns);
                foreach (XmlNode messageNode in messageNodeList) {
                    UmlMessage message = new UmlMessage ();
                    String storedID = "";
                    message.Id = messageNode.Attributes["xmi.id"].Value;
                    message.Name = messageNode.Attributes["name"].Value;

                    foreach (XmlNode taggedValuesNode in messageNode.SelectNodes ("//UML:Message[@xmi.id='" + message.Id + "']//UML:ModelElement.taggedValue//UML:TaggedValue", ns)) {
                        String tagName = (taggedValuesNode.Attributes["tag"].Value).ToUpper ();
                        String tagValue = HttpUtility.UrlDecode (taggedValuesNode.Attributes["value"].Value);
                        message.SetTaggedValue (tagName, tagValue);
                    }

                    foreach (XmlNode senderNode in messageNode.SelectNodes ("//UML:Message[@xmi.id='" + message.Id + "']//UML:Message.sender//UML:ClassifierRole[@xmi.idref]", ns)) {
                        message.Sender = (from c in sequenceDiagramAux.UmlObjects.OfType<UmlClass> () where c.IdRef.Equals (senderNode.Attributes["xmi.idref"].Value) select c).FirstOrDefault ();
                        break;
                    }

                    foreach (XmlNode receiverNode in messageNode.SelectNodes ("//UML:Message[@xmi.id='" + message.Id + "']//UML:Message.receiver//UML:ClassifierRole[@xmi.idref]", ns)) {
                        message.Receiver = (from c in sequenceDiagramAux.UmlObjects.OfType<UmlClass> () where c.IdRef.Equals (receiverNode.Attributes["xmi.idref"].Value) select c).FirstOrDefault ();
                        break;
                    }

                    foreach (XmlNode actionNode in messageNode.SelectNodes ("//UML:Message[@xmi.id='" + message.Id + "']//UML:Message.action//UML:Action[@xmi.id]", ns)) {
                        storedID = "";
                        try {
                            storedID = actionNode.Attributes["xmi.id"].Value;
                            message.ActionType = Convert.ToInt32 (actionNode.Attributes["actionType"].Value);
                        } catch {

                        }
                        foreach (XmlNode operationNode in actionNode.SelectNodes ("//UML:Action[@xmi.id='" + storedID + "']//UML:Operation", ns)) {
                            String aux = operationNode.Attributes["xmi.idref"].Value;
                            message.Method = GetMethod (aux, sequenceDiagram);
                        }
                    }

                    foreach (XmlNode judeMessagePresentation in document.SelectNodes ("//JUDE:MessagePresentation[@index]", ns)) {
                        String idAux = judeMessagePresentation.Attributes["xmi.id"].Value;
                        foreach (XmlNode umlMessageNode in judeMessagePresentation.SelectNodes ("//JUDE:MessagePresentation[@xmi.id='" + idAux + "']//JUDE:UPresentation.semanticModel//UML:Message[@xmi.idref='" + message.Id + "']", ns)) {
                            String aux2 = "";
                            try {
                                aux2 = judeMessagePresentation.Attributes["index"].Value;
                            } catch {

                            }
                            decimal index = decimal.Parse (aux2, new CultureInfo ("en-US"));
                            message.Index = Convert.ToDouble (index);
                            break;
                        }
                    }
                    sequenceDiagramAux.UmlObjects.Add (message);
                }
            }
            return model;
        }

        private UmlMethod GetMethod (String aux, UmlSequenceDiagram sequenceDiagram) {
            foreach (UmlClass sdClass in sequenceDiagram.UmlObjects.OfType<UmlClass> ()) {
                foreach (UmlMethod method in sdClass.Methods) {
                    if (method.Id.Equals (aux)) {
                        return method;
                    }
                }
            }
            return null;
        }

        //#region OLD Version - Working for 1 param
        //private GeneralUseStructure FromXmi2(XmlDocument document, ref String name)
        //{
        //    UmlModel model = new UmlModel();
        //    XmlNamespaceManager ns = new XmlNamespaceManager(document.NameTable);
        //    ns.AddNamespace("JUDE", "http://objectclub.esm.co.jp/Jude/namespace/");
        //    ns.AddNamespace("UML", "org.omg.xmi.namespace.UML");
        //    List<UmlClass> classes = new List<UmlClass>();
        //    List<String> pairs = new List<String>();
        //    UmlSequenceDiagram sequenceDiagram = null;
        //    String storedID = "";

        //    foreach (XmlNode classNode in document.SelectNodes("//UML:Namespace.ownedElement//UML:Class[@xmi.id]", ns))
        //    {
        //        UmlClass sdClass = new UmlClass();
        //        sdClass.Id = classNode.Attributes["xmi.id"].Value;
        //        sdClass.Name = classNode.Attributes["name"].Value;
        //        classes.Add(sdClass);
        //    }

        //    XmlNodeList sequenceDiagramNodeList = document.SelectNodes("//UML:Namespace.collaboration//UML:Collaboration[@xmi.id]", ns);

        //    foreach (XmlNode sequenceDiagramNode in sequenceDiagramNodeList)
        //    {
        //        sequenceDiagram = new UmlSequenceDiagram();
        //        sequenceDiagram.Id = sequenceDiagramNode.Attributes["xmi.id"].Value;
        //        model.AddDiagram(sequenceDiagram);
        //        String pair = "";

        //        foreach (XmlNode classifierRoleNode in sequenceDiagramNode.SelectNodes("//UML:ClassifierRole[@xmi.id]", ns))
        //        {
        //            String aux = classifierRoleNode.Attributes["xmi.id"].Value;
        //            pair = classifierRoleNode.Attributes["xmi.id"].Value;
        //            pair += "#";

        //            foreach (XmlNode classifierRoleBaseNode in classifierRoleNode.SelectNodes("//UML:ClassifierRole[@xmi.id='" + aux + "']//UML:ClassifierRole.base//UML:Classifier[@xmi.idref]", ns))
        //            {
        //                pair += classifierRoleBaseNode.Attributes["xmi.idref"].Value;
        //            }
        //            pairs.Add(pair);
        //        }
        //    }

        //    foreach (UmlClass sdClass in classes)
        //    {
        //        foreach (String pair in pairs)
        //        {
        //            String[] splitted = pair.Split('#');
        //            if (sdClass.Id.Equals(splitted[1]))
        //            {
        //                sdClass.IdRef = splitted[0];
        //                break;
        //            }
        //        }
        //        sequenceDiagram.UmlObjects.Add(sdClass);
        //    }

        //    foreach (UmlSequenceDiagram sequenceDiagramAux in model.Diagrams.OfType<UmlSequenceDiagram>())
        //    {
        //        XmlNodeList messageNodeList = document.SelectNodes("//UML:Namespace.collaboration//UML:Collaboration[@xmi.id='" + sequenceDiagramAux.Id + "']//UML:Message[@xmi.id]", ns);
        //        foreach (XmlNode messageNode in messageNodeList)
        //        {
        //            UmlMethod method = new UmlMethod();
        //            method.Id = messageNode.Attributes["xmi.id"].Value;
        //            method.Name = messageNode.Attributes["name"].Value;

        //            try
        //            {
        //                method.Return = messageNode.Attributes["returnValue"].Value;
        //            }
        //            catch
        //            {
        //                method.Return = "";
        //            }

        //            foreach (XmlNode senderNode in messageNode.SelectNodes("//UML:Message.sender//UML:ClassifierRole[@xmi.idref]", ns))
        //            {
        //                method.Sender = (from c in sequenceDiagramAux.UmlObjects.OfType<UmlClass>()
        //                                 where c.IdRef.Equals(senderNode.Attributes["xmi.idref"].Value)
        //                                 select c).FirstOrDefault();
        //                break;
        //            }

        //            foreach (XmlNode receiverNode in messageNode.SelectNodes("//UML:Message.receiver//UML:ClassifierRole[@xmi.idref]", ns))
        //            {
        //                method.Receiver = (from c in sequenceDiagramAux.UmlObjects.OfType<UmlClass>()
        //                                   where c.IdRef.Equals(receiverNode.Attributes["xmi.idref"].Value)
        //                                   select c).FirstOrDefault();
        //                method.Receiver.Methods.Add(method);
        //                break;
        //            }

        //            foreach (XmlNode messageActionNode in messageNode.SelectNodes("//UML:Message[@xmi.id='" + method.Id + "']//UML:Message.action", ns))
        //            {
        //                foreach (XmlNode actionNode in messageActionNode.SelectNodes("//UML:Message[@xmi.id='" + method.Id + "']//UML:Message.action//UML:Action[@xmi.id]", ns))
        //                {
        //                    storedID = "";
        //                    try
        //                    {
        //                        storedID = actionNode.Attributes["xmi.id"].Value;
        //                        method.ActionType = Convert.ToInt32(actionNode.Attributes["actionType"].Value);
        //                        break;
        //                    }
        //                    catch
        //                    {

        //                    }
        //                }

        //                foreach (XmlNode expressionBodyNode in messageActionNode.SelectNodes("//UML:Action[@xmi.id='" + storedID + "']//UML:Argument.value//UML:Expression[@xmi.id]//UML:Expression.body", ns))
        //                {
        //                    String innerText = expressionBodyNode.InnerText;
        //                    UmlMethodParam methodParam = new UmlMethodParam();
        //                    try
        //                    {
        //                        methodParam.Name = innerText.Split(':')[0].Replace(" ", "");
        //                    }
        //                    catch
        //                    {

        //                    }
        //                    try
        //                    {
        //                        methodParam.Type = innerText.Split(':')[1].Replace(" ", "");
        //                    }
        //                    catch
        //                    {

        //                    }
        //                    method.Params.Add(methodParam);
        //                    break;
        //                }
        //            }

        //            foreach (XmlNode judeMessagePresentation in document.SelectNodes("//JUDE:MessagePresentation[@index]", ns))
        //            {
        //                String idAux = judeMessagePresentation.Attributes["xmi.id"].Value;
        //                foreach (XmlNode umlMessageNode in judeMessagePresentation.SelectNodes("//JUDE:MessagePresentation[@xmi.id='" + idAux + "']//JUDE:UPresentation.semanticModel//UML:Message[@xmi.idref='" + method.Id + "']", ns))
        //                {
        //                    String aux2 = "";
        //                    try
        //                    {
        //                        aux2 = judeMessagePresentation.Attributes["index"].Value;
        //                    }
        //                    catch
        //                    {

        //                    }
        //                    decimal index = decimal.Parse(aux2, new CultureInfo("en-US"));
        //                    method.Index = Convert.ToDouble(index);
        //                    break;
        //                }
        //            }
        //            sequenceDiagramAux.UmlObjects.Add(method);
        //        }
        //    }
        //    return null;
        //}
        //#endregion

        private String GetParamType (XmlDocument document, XmlNamespaceManager ns, String aux) {
            foreach (XmlNode classNode in document.SelectNodes ("//UML:Class[@xmi.id='" + aux + "']", ns)) {
                return classNode.Attributes["name"].Value;
            }

            foreach (XmlNode primitiveNode in document.SelectNodes ("//UML:Primitive[@xmi.id='" + aux + "']", ns)) {
                return primitiveNode.Attributes["name"].Value;
            }
            return null;
        }

        private void ResetAttributes () {
            listModelingStructure = new List<GeneralUseStructure> ();
        }
        #endregion
    }
}