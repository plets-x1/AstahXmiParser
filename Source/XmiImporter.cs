using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Plets.Core.ControlAndConversionStructures;
using Plets.Core.Interfaces;
using Plets.Modeling.Uml;
using Plets.Util.CSV;

namespace Plets.Data.Xmi {
    /*
    /// <summary>
    /// <img src="images/Xmi.PNG"/>
    /// </summary>
    public static class NamespaceDoc
    {
    }*/

    public class XmiImporter : Parser {
        #region Attributes
        private List<String> parametersFiles;
        private List<CsvParamFile> paramFiles;
        private List<GeneralUseStructure> listModelingStructure;
        #endregion

        #region Constructor
        public XmiImporter () {
            listModelingStructure = new List<GeneralUseStructure> ();
        }
        #endregion

        #region Public Methods
        public override StructureCollection ParserMethod (String path, ref String name, Tuple<String, Object>[] args) {
            ResetAttributes ();
            StructureCollection model = new StructureCollection ();
            XmlDocument document = new XmlDocument ();
            document.Load (path);
            if (!IsAstah (document)) {
                throw new Exception ("The file loaded is not a XML generated by Astah. Please reload file.");
            }
            listModelingStructure.Add (FromXmi (document, ref name));
            //model.type=ControlStructure.StructureType.UmlModel;
            listModelingStructure.AddRange (ReadCsv (path));
            //listModelingStructure.Add(model);
            model.listGeneralStructure.AddRange (listModelingStructure);
            return model;
            //}
            //else
            //{
            //    XmlArgoUml argo = new XmlArgoUml();
            //    return argo.ParserMethod(path, ref name, args);
            //}
        }

        public UmlModel FromXmi (XmlDocument doc, ref String name) {
            UmlModel model = new UmlModel ("");
            //uml and astah namespaces
            XmlNamespaceManager nsManager = new XmlNamespaceManager (doc.NameTable);
            nsManager.AddNamespace ("JUDE", "http://objectclub.esm.co.jp/Jude/namespace/");
            nsManager.AddNamespace ("UML", "org.omg.xmi.namespace.UML");

            XmlNodeList mod = doc.SelectNodes ("//UML:Model[@xmi.id]", nsManager);
            foreach (XmlNode node in mod) {
                name = node.Attributes["name"].Value;
                break;
            }
            //importing activity diagrams into model.
            XmlNodeList nodesWithActivityDiagrams = doc.SelectNodes ("//UML:ActivityGraph[@xmi.id]", nsManager);

            #region Activity Diagram
            if (nodesWithActivityDiagrams.Count != 0) {
                foreach (XmlNode node in nodesWithActivityDiagrams) {
                    UmlActivityDiagram actDiagram = new UmlActivityDiagram (node.Attributes["name"].Value);
                    actDiagram.Id = node.Attributes["xmi.id"].Value;
                    actDiagram.Name = node.Attributes["name"].Value;
                    model.AddDiagram (actDiagram);

                    #region Pseudostate
                    foreach (XmlNode pseudoNode in node.SelectNodes ("//UML:ActivityGraph[@xmi.id='" + actDiagram.Id + "']//UML:Pseudostate[@xmi.id]", nsManager)) {
                        UmlElement element = null;

                        if (pseudoNode.Attributes["kind"].Value.Equals ("initial")) {
                            element = new UmlInitialState ();
                        } else if (pseudoNode.Attributes["kind"].Value.Equals ("fork")) {
                            element = new UmlFork ();
                        } else if (pseudoNode.Attributes["kind"].Value.Equals ("junction")) {
                            element = new UmlDecision ();
                        } else if (pseudoNode.Attributes["kind"].Value.Equals ("join")) {
                            element = new UmlJoin ();
                        }

                        element.Name = pseudoNode.Attributes["name"].Value;
                        element.Id = pseudoNode.Attributes["xmi.id"].Value;

                        #region Tagged Values
                        foreach (XmlNode taggedValuesNode in pseudoNode.SelectNodes ("//UML:Pseudostate[@xmi.id='" + element.Id + "']//UML:ModelElement.taggedValue//UML:TaggedValue[@xmi.id]", nsManager)) {
                            //element.SetTaggedValue(taggedValuesNode.Attributes["tag"].Value, HttpUtility.UrlDecode(taggedValuesNode.Attributes["value"].Value));
                            element.SetTaggedValue (taggedValuesNode.Attributes["tag"].Value, taggedValuesNode.Attributes["value"].Value);
                        }
                        #endregion

                        actDiagram.UmlObjects.Add (element);
                    }
                    #endregion
                    #region ActionState
                    foreach (XmlNode stateNode in node.SelectNodes ("//UML:ActivityGraph[@xmi.id='" + actDiagram.Id + "']//UML:ActionState[@xmi.id]", nsManager)) {
                        UmlElement state = new UmlActionState ();
                        state.Id = stateNode.Attributes["xmi.id"].Value;
                        state.Name = stateNode.Attributes["name"].Value;

                        #region Tagged Values
                        foreach (XmlNode taggedValuesNode in stateNode.SelectNodes ("//UML:ActionState[@xmi.id='" + state.Id + "']//UML:ModelElement.taggedValue//UML:TaggedValue[@xmi.id]", nsManager)) {
                            #region Hyperlink
                            if (taggedValuesNode.Attributes["tag"].Value.Equals ("jude.hyperlink")) {
                                string nameToken = "type%3Dmodel%2Cname%3D";
                                string commentToken = "%2Cpath%3D%2Ccomment%3D";
                                int i1 = taggedValuesNode.Attributes["value"].Value.IndexOf (nameToken) + (nameToken.Length);
                                int i2 = taggedValuesNode.Attributes["value"].Value.IndexOf (commentToken);
                                String aux_value = taggedValuesNode.Attributes["value"].Value.Substring (i1, i2 - commentToken.Length + 1);
                                String aux_value2 = taggedValuesNode.Attributes["value"].Value.Substring (i2 + commentToken.Length);

                                state.SetTaggedValue (taggedValuesNode.Attributes["tag"].Value, aux_value);

                                if (!String.IsNullOrEmpty (aux_value2)) {
                                    state.SetTaggedValue ("cycles", aux_value2);
                                }

                                foreach (XmlNode jude in node.SelectNodes ("//JUDE:Diagram[@xmi.id='" + aux_value + "']//UML:ActivityGraph", nsManager)) {
                                    string idActivityGraph = jude.Attributes["xmi.idref"].Value;
                                    foreach (XmlNode item in node.SelectNodes ("//UML:ActivityGraph[xmi.id='" + idActivityGraph + "']", nsManager)) {
                                        state.SetTaggedValue ("jude.hyperlink", item.Attributes["name"].Value);
                                    }
                                }
                                foreach (XmlNode item in node.SelectNodes ("//JUDE:ActivityDiagram[@xmi.id='" + aux_value + "']//UML:ActivityGraph", nsManager)) {
                                    string idActivityGraph = item.Attributes["xmi.idref"].Value;
                                    foreach (XmlNode item1 in node.SelectNodes ("//UML:ActivityGraph[@xmi.id='" + idActivityGraph + "']", nsManager)) {
                                        state.SetTaggedValue ("jude.hyperlink", item1.Attributes["name"].Value);
                                    }
                                }
                            }
                            #endregion
                            else {
                                string tag = taggedValuesNode.Attributes["tag"].Value;
                                string
                                var = null;
                                try {
                                    var = taggedValuesNode.Attributes["value"].Value;
                                } catch (Exception) {

                                    state.SetTaggedValue (tag, "");
                                }
                                if (var != null)
                                    state.SetTaggedValue (tag,
                                        var);
                            }
                        }
                        #endregion

                        actDiagram.UmlObjects.Add (state);
                    }
                    #endregion
                    #region FinalState
                    foreach (XmlNode finalStateNode in node.SelectNodes ("//UML:ActivityGraph[@xmi.id='" + actDiagram.Id + "']//UML:FinalState[@xmi.id]", nsManager)) {
                        UmlElement finalState = new UmlFinalState ();
                        finalState.Name = finalStateNode.Attributes["name"].Value;
                        finalState.Id = finalStateNode.Attributes["xmi.id"].Value;

                        #region Tagged Values
                        foreach (XmlNode taggedValuesNode in finalStateNode.SelectNodes ("//UML:FinalState[@xmi.id='" + finalState.Id + "']//UML:ModelElement.taggedValue//UML:TaggedValue[@xmi.id]", nsManager)) {
                            //finalState.SetTaggedValue(taggedValuesNode.Attributes["tag"].Value, HttpUtility.UrlDecode(taggedValuesNode.Attributes["value"].Value));
                            finalState.SetTaggedValue (taggedValuesNode.Attributes["tag"].Value, taggedValuesNode.Attributes["value"].Value);
                        }
                        #endregion

                        actDiagram.UmlObjects.Add (finalState);
                    }
                    #endregion
                    #region Transition
                    //UmlTransition
                    foreach (XmlNode transitionNode in node.SelectNodes ("//UML:ActivityGraph[@xmi.id='" + actDiagram.Id + "']//UML:Transition[@xmi.id]", nsManager)) {
                        UmlTransition transition = new UmlTransition ();

                        transition.Id = transitionNode.Attributes["xmi.id"].Value;
                        transition.Name = transitionNode.Attributes["name"].Value;
                        // Tagged Values
                        foreach (XmlNode tagValuesNode in transitionNode.SelectNodes ("//UML:ActivityGraph[@xmi.id='" + actDiagram.Id + "']//UML:Transition[@xmi.id='" + transition.Id + "']//UML:TaggedValue", nsManager)) {
                            try {
                                //transition.SetTaggedValue(tagValuesNode.Attributes["tag"].Value, HttpUtility.UrlDecode(tagValuesNode.Attributes["value"].Value));
                                transition.SetTaggedValue (tagValuesNode.Attributes["tag"].Value.ToUpper (), tagValuesNode.Attributes["value"].Value);
                            } catch {
                                transition.SetTaggedValue (tagValuesNode.Attributes["tag"].Value.ToUpper (), "");
                            }
                        }
                        // Transition Source
                        foreach (XmlNode transitionSource in transitionNode.SelectNodes ("//UML:ActivityGraph[@xmi.id='" + actDiagram.Id + "']//UML:Transition[@xmi.id='" + transition.Id + "']//UML:Transition.source//UML:StateVertex", nsManager)) {
                            UmlElement element = actDiagram.GetElementById (transitionSource.Attributes["xmi.idref"].Value);
                            transition.Source = element;
                        }
                        // Transition Target
                        foreach (XmlNode transitionTarget in transitionNode.SelectNodes ("//UML:ActivityGraph[@xmi.id='" + actDiagram.Id + "']//UML:Transition[@xmi.id='" + transition.Id + "']//UML:Transition.target//UML:StateVertex", nsManager)) {
                            UmlElement element = actDiagram.GetElementById (transitionTarget.Attributes["xmi.idref"].Value);
                            transition.Target = element;
                        }
                        actDiagram.UmlObjects.Add (transition);
                    }
                    #endregion
                    #region Lanes
                    XmlNodeList partition = node.SelectNodes ("//UML:ActivityGraph[@xmi.id='" + actDiagram.Id + "']//UML:Partition[@xmi.id]", nsManager);
                    bool isDimension = false;

                    if (partition.Count != 0) {
                        foreach (XmlNode dimensionNode in partition) {
                            if (isDimension == false) {
                                isDimension = true;
                                UmlLane dimension = new UmlLane ();
                                dimension.Id = dimensionNode.Attributes["xmi.id"].Value;
                                dimension.Name = dimensionNode.Attributes["name"].Value;
                                foreach (XmlNode laneNode in dimensionNode.SelectNodes ("//UML:ActivityGraph[@xmi.id='" + actDiagram.Id + "']//UML:Partition[@xmi.id='" + dimension.Id + "']//JUDE:ModelElement", nsManager)) {
                                    XmiImporter importer = new XmiImporter ();
                                    UmlLane lane = importer.CreateLane (dimension.Id, node, laneNode.Attributes["xmi.idref"].Value, actDiagram, nsManager);
                                    actDiagram.UmlObjects.Add (lane);
                                    actDiagram.Lanes.Add (lane);
                                    dimension.ListLane.Add (lane);
                                }
                            }
                        }
                    }
                    #endregion
                }
            }
            #endregion

            //importing usecase diagrams into model.
            XmlNodeList nodesWithUseCaseDiagrams = doc.SelectNodes ("//UML:UseCase[@xmi.id]", nsManager);
            XmlNodeList nodesWithActorDiagrams = doc.SelectNodes ("//UML:Actor[@xmi.id]", nsManager);

            #region UseCase Diagrams
            //   if (nodesWithUseCaseDiagrams.Count != 0 && nodesWithActorDiagrams.Count != 0)
            {
                XmlNodeList modelElements = doc.SelectNodes ("//UML:Model[@xmi.id]", nsManager);

                foreach (XmlNode node in modelElements) {
                    UmlUseCaseDiagram useCaseDiagram = new UmlUseCaseDiagram ();
                    useCaseDiagram.Id = node.Attributes["xmi.id"].Value;
                    useCaseDiagram.Name = node.Attributes["name"].Value;
                    model.AddDiagram (useCaseDiagram);
                    #region Actor
                    foreach (XmlNode actorNode in node.SelectNodes ("//UML:Actor[@xmi.id]", nsManager)) {
                        UmlActor actor = new UmlActor ();
                        actor.Id = actorNode.Attributes["xmi.id"].Value;
                        actor.Name = actorNode.Attributes["name"].Value;

                        #region Tagged Values
                        foreach (XmlNode taggedValuesNode in actorNode.SelectNodes ("//UML:Actor[@xmi.id='" + actor.Id + "']//UML:ModelElement.taggedValue//UML:TaggedValue[@xmi.id]", nsManager)) {
                            #region Hyperlink
                            if (taggedValuesNode.Attributes["tag"].Value.Equals ("jude.hyperlink")) {
                                string aux_value = taggedValuesNode.Attributes["value"].Value.Substring (22);
                                aux_value = aux_value.Substring (0, aux_value.Length - 23);
                                actor.SetTaggedValue (taggedValuesNode.Attributes["tag"].Value, aux_value);
                                foreach (XmlNode jude in node.SelectNodes ("//JUDE:Diagram[@xmi.id='" + aux_value + "']//UML:ActivityGraph", nsManager)) {
                                    string idActivityGraph = jude.Attributes["xmi.idref"].Value;
                                    foreach (XmlNode item in node.SelectNodes ("//UML:ActivityGraph[xmi.id='" + idActivityGraph + "']", nsManager)) {
                                        actor.SetTaggedValue ("jude.hyperlink", item.Attributes["name"].Value);
                                    }
                                }
                                foreach (XmlNode item in node.SelectNodes ("//JUDE:ActivityDiagram[@xmi.id='" + aux_value + "']//UML:ActivityGraph", nsManager)) {
                                    string idActivityGraph = item.Attributes["xmi.idref"].Value;
                                    foreach (XmlNode item1 in node.SelectNodes ("//UML:ActivityGraph[@xmi.id='" + idActivityGraph + "']", nsManager)) {
                                        actor.SetTaggedValue ("jude.hyperlink", item1.Attributes["name"].Value);
                                    }
                                }
                            }
                            #endregion
                            else {
                                try {
                                    //actor.SetTaggedValue(taggedValuesNode.Attributes["tag"].Value, HttpUtility.UrlDecode(taggedValuesNode.Attributes["value"].Value));
                                    actor.SetTaggedValue (taggedValuesNode.Attributes["tag"].Value.ToUpper (), taggedValuesNode.Attributes["value"].Value);
                                } catch {
                                    actor.SetTaggedValue (taggedValuesNode.Attributes["tag"].Value.ToUpper (), "");
                                }
                            }
                        }
                        #endregion
                        useCaseDiagram.UmlObjects.Add (actor);
                    }
                    #endregion
                    #region UseCase
                    foreach (XmlNode useCaseNode in node.SelectNodes ("//UML:UseCase[@xmi.id]", nsManager)) {
                        UmlUseCase useCase = new UmlUseCase ();
                        useCase.Id = useCaseNode.Attributes["xmi.id"].Value;
                        useCase.Name = useCaseNode.Attributes["name"].Value;

                        #region Tagged Values
                        foreach (XmlNode taggedValuesNode in node.SelectNodes ("//UML:UseCase[@xmi.id='" + useCase.Id + "']//UML:ModelElement.taggedValue//UML:TaggedValue[@xmi.id]", nsManager)) {
                            #region Hyperlink
                            if (taggedValuesNode.Attributes["tag"].Value.Equals ("jude.hyperlink")) {
                                string aux_value = taggedValuesNode.Attributes["value"].Value.Substring (22);
                                aux_value = aux_value.Substring (0, aux_value.Length - 23);
                                foreach (XmlNode jude in node.SelectNodes ("//JUDE:Diagram[@xmi.id='" + aux_value + "']//UML:ActivityGraph", nsManager)) {
                                    string idActivityGraph = jude.Attributes["xmi.idref"].Value;
                                    foreach (XmlNode item in node.SelectNodes ("//UML:ActivityGraph[xmi.id='" + idActivityGraph + "']", nsManager)) {
                                        useCase.SetTaggedValue ("jude.hyperlink", item.Attributes["name"].Value);
                                    }
                                }

                                foreach (XmlNode item in node.SelectNodes ("//JUDE:ActivityDiagram[@xmi.id='" + aux_value + "']//UML:ActivityGraph", nsManager)) {
                                    string idActivityGraph = item.Attributes["xmi.idref"].Value;
                                    foreach (XmlNode item1 in node.SelectNodes ("//UML:ActivityGraph[@xmi.id='" + idActivityGraph + "']", nsManager)) {
                                        useCase.SetTaggedValue ("jude.hyperlink", item1.Attributes["name"].Value);
                                    }
                                }
                            }
                            #endregion
                            else {
                                try {
                                    //useCase.SetTaggedValue(taggedValuesNode.Attributes["tag"].Value, HttpUtility.UrlDecode(taggedValuesNode.Attributes["value"].Value));
                                    useCase.SetTaggedValue (taggedValuesNode.Attributes["tag"].Value.ToUpper (), taggedValuesNode.Attributes["value"].Value);
                                } catch {
                                    useCase.SetTaggedValue (taggedValuesNode.Attributes["tag"].Value.ToUpper (), "");
                                }
                            }
                        }
                        #endregion
                        useCaseDiagram.UmlObjects.Add (useCase);
                    }
                    #endregion
                    #region Association
                    foreach (XmlNode associationNode in node.SelectNodes ("//UML:Association[@xmi.id]", nsManager)) {
                        UmlAssociation association = new UmlAssociation ();
                        association.Id = associationNode.Attributes["xmi.id"].Value;
                        association.Name = associationNode.Attributes["name"].Value;

                        bool putEnd1 = false;

                        foreach (XmlNode associationEndNode in associationNode.SelectNodes ("//UML:Association[@xmi.id='" + association.Id + "']//UML:AssociationEnd//UML:AssociationEnd.participant//UML:Classifier[@xmi.idref]", nsManager)) {
                            foreach (UmlElement element in useCaseDiagram.UmlObjects.OfType<UmlElement> ()) {
                                if (!putEnd1) {
                                    if (element.Id.Equals (associationEndNode.Attributes["xmi.idref"].Value)) {
                                        association.End1 = element;
                                        putEnd1 = true;
                                    }
                                } else {
                                    if (element.Id.Equals (associationEndNode.Attributes["xmi.idref"].Value)) {
                                        association.End2 = element;
                                    }
                                }
                            }
                        }

                        #region Tagged Values
                        foreach (XmlNode taggedValuesNode in node.SelectNodes ("//UML:Association[@xmi.id='" + association.Id + "']//UML:ModelElement.taggedValue//UML:TaggedValue[@xmi.id]", nsManager)) {
                            try {
                                //association.SetTaggedValue(taggedValuesNode.Attributes["tag"].Value, HttpUtility.UrlDecode(taggedValuesNode.Attributes["value"].Value));
                                association.SetTaggedValue (taggedValuesNode.Attributes["tag"].Value, taggedValuesNode.Attributes["value"].Value);
                            } catch {
                                association.SetTaggedValue (taggedValuesNode.Attributes["tag"].Value, "");
                            }
                        }
                        #endregion
                        useCaseDiagram.UmlObjects.Add (association);
                    }

                    #endregion
                    #region Include
                    foreach (XmlNode includeNode in node.SelectNodes ("//UML:Include[@xmi.id]", nsManager)) {
                        UmlAssociation association = new UmlAssociation ();
                        association.Id = includeNode.Attributes["xmi.id"].Value;
                        association.Name = includeNode.Attributes["name"].Value;
                        association.Stereotypes.Add ("Include");

                        XmlNode begin = includeNode.SelectSingleNode ("//UML:Include[@xmi.id='" + association.Id + "']//UML:Include.base//UML:UseCase[@xmi.idref]", nsManager);
                        XmlNode end = includeNode.SelectSingleNode ("//UML:Include[@xmi.id='" + association.Id + "']//UML:Include.addition//UML:UseCase[@xmi.idref]", nsManager);

                        foreach (UmlElement element in useCaseDiagram.UmlObjects.OfType<UmlElement> ()) {
                            if (element.Id.Equals (begin.Attributes["xmi.idref"].Value)) {
                                association.End1 = element;
                            } else if (element.Id.Equals (end.Attributes["xmi.idref"].Value)) {
                                association.End2 = element;
                            }
                        }
                        useCaseDiagram.UmlObjects.Add (association);
                    }
                    #endregion
                    #region Extend
                    foreach (XmlNode extendNode in node.SelectNodes ("//UML:Extend[@xmi.id]", nsManager)) {
                        UmlAssociation association = new UmlAssociation ();
                        association.Id = extendNode.Attributes["xmi.id"].Value;
                        association.Name = extendNode.Attributes["name"].Value;
                        association.Stereotypes.Add ("Extend");

                        XmlNode end = extendNode.SelectSingleNode ("//UML:Extend[@xmi.id='" + association.Id + "']//UML:Extend.base//UML:UseCase[@xmi.idref]", nsManager);
                        XmlNode begin = extendNode.SelectSingleNode ("//UML:Extend[@xmi.id='" + association.Id + "']//UML:Extend.extension//UML:UseCase[@xmi.idref]", nsManager);

                        foreach (UmlElement element in useCaseDiagram.UmlObjects.OfType<UmlElement> ()) {
                            if (element.Id.Equals (begin.Attributes["xmi.idref"].Value)) {
                                association.End1 = element;
                            } else if (element.Id.Equals (end.Attributes["xmi.idref"].Value)) {
                                association.End2 = element;
                            }
                        }
                        useCaseDiagram.UmlObjects.Add (association);
                    }
                    #endregion
                }
            }
            #endregion
            return model;
        }

        public UmlLane CreateLane (String idDimension, XmlNode node, String id, UmlActivityDiagram actDiagram, XmlNamespaceManager nsManager) {
            UmlLane lane = new UmlLane ();
            foreach (XmlNode element in node.SelectNodes ("//UML:Partition[@xmi.id='" + id + "']", nsManager)) {
                String aux_lane_name = element.Attributes["name"].Value;
                lane.Id = element.Attributes["xmi.id"].Value;
                if (aux_lane_name.Contains ("+")) {
                    aux_lane_name = aux_lane_name.Replace ("+", "");
                    if (aux_lane_name.Equals ("")) {
                        lane.Name = "";
                    } else {
                        lane.Name = element.Attributes["name"].Value;
                    }
                } else {
                    lane.Name = element.Attributes["name"].Value;
                }

                if (!idDimension.Equals (lane.Id)) {
                    foreach (XmlNode modelElementNode in element.SelectNodes ("//UML:Partition[@xmi.id='" + lane.Id + "']//JUDE:ModelElement", nsManager)) {
                        if (actDiagram.GetElementById (modelElementNode.Attributes["xmi.idref"].Value) != null) {
                            lane.AddElement (actDiagram.GetElementById (modelElementNode.Attributes["xmi.idref"].Value));
                        }
                    }
                }
            }
            return lane;
        }
        #endregion

        #region Private Methods
        private void ResetAttributes () {
            parametersFiles = new List<String> ();
            paramFiles = new List<CsvParamFile> ();
            listModelingStructure = new List<GeneralUseStructure> ();
        }

        private List<GeneralUseStructure> ReadCsv (string path) {
            parametersFiles = new List<string> (Directory.GetFiles ((new FileInfo (path)).Directory.FullName, "*.csv"));

            if (parametersFiles != null) {
                paramFiles = new List<CsvParamFile> (parametersFiles.Count);
                foreach (String paramPath in parametersFiles) {
                    FileInfo f = new FileInfo (paramPath);
                    CsvParamFile csv = new CsvParamFile (f);
                    paramFiles.Add (csv);
                }
            }
            return paramFiles.Cast<GeneralUseStructure> ().ToList ();
        }

        private bool IsAstah (XmlDocument document) {
            XmlNamespaceManager nsManager = new XmlNamespaceManager (document.NameTable);
            nsManager.AddNamespace ("JUDE", "http://objectclub.esm.co.jp/Jude/namespace/");
            nsManager.AddNamespace ("UML", "org.omg.xmi.namespace.UML");

            XmlNodeList mod = document.SelectNodes ("//XMI", nsManager);
            String judeNS = null;

            foreach (XmlNode node in mod) {
                try {
                    judeNS = node.Attributes["xmlns:JUDE"].Value;

                    if (!String.IsNullOrEmpty (judeNS)) {
                        return true;
                    }
                } catch {
                    return false;
                }
            }
            return false;
        }
        #endregion
    }
}