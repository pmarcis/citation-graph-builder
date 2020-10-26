using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace GetSemanticScholarAuthorCitationGraph
{
    class Program
    {
        static void Main(string[] args)
        {
            bool registerOnlyRelevantReferences = true;
            bool ignoreReferences = true;
            string outFile = null;
            string authorId = null;
            int year = 0; //Since when to collect data
            for (int i=0;i<args.Length;i++)
            {
                if (args[i]=="-o" && args.Length>i+1)
                {
                    outFile = args[i + 1];
                    i++;
                }
                else if (args[i]== "-id" && args.Length > i + 1)
                {
                    authorId = args[i + 1];
                    i++;
                }
                else if (args[i] == "-y" && args.Length > i + 1)
                {
                    year = Convert.ToInt32(args[i + 1]);
                    i++;
                }
                else if (args[i] == "--include-relevant-references")
                {
                    ignoreReferences = false;
                    registerOnlyRelevantReferences = true;
                }
                else if (args[i] == "--include-all-references")
                {
                    ignoreReferences = false;
                    registerOnlyRelevantReferences = false;
                }
                else if (args[i] == "-h" || args[i]=="--help")
                {
                    PrintUsage();
                    return;
                }
            }

            //outFile and authorId are mandatory parameters.
            if (string.IsNullOrWhiteSpace(outFile)||string.IsNullOrWhiteSpace(authorId))
            {
                PrintUsage();
                return;
            }

            //Get author data including the list of all author's papers first.
            string url = "https://api.semanticscholar.org/v1/author/" + authorId;
            string authorData = ExecuteRequest(url);
            //We use dynamic as we do not want to code the full JSON structure, but want just a sub-set of it.
            dynamic authorObject = JsonConvert.DeserializeObject(authorData);
            string authorName = authorObject.name;
            Dictionary<string, string> papers = new Dictionary<string, string>();
            foreach (var paper in authorObject.papers)
            {
                if (paper.year!=null)
                {
                    if (Convert.ToInt32(paper.year) < year) continue;
                }
                else
                {
                    continue;//papers without a year are not valid papers.
                }
                string paperId = paper.paperId;
                string title = paper.title;
                if (!papers.ContainsKey(paperId))
                {
                    papers.Add(paperId, title);
                }
            }
            //We assume that the authorID points to all information of exactly one author.
            //Now, Semantic Scholar is rather noisy and there are plenty examples where author
            //information is scattered across multiple authorIDs, or one authorID contains data
            //from multiple authors. If that is the case, you might want to consider improving
            //the above code to acquire the correct list of interested papers...

            //paper URL: https://api.semanticscholar.org/v1/paper/05fb8c0eedd15b9c26aabf5e971185bffad3ad11

            Dictionary<string, string> coAuthors = new Dictionary<string, string>();
            Dictionary<string, dynamic> paperObjects = new Dictionary<string, dynamic>();

            foreach (string paperId in papers.Keys)
            {
                Console.Error.WriteLine("Sleeping");
                System.Threading.Thread.Sleep(4000);
                Console.Error.WriteLine("Continuing");
                url = "https://api.semanticscholar.org/v1/paper/" + paperId;
                string paperData = ExecuteRequest(url);
                dynamic paperObject = JsonConvert.DeserializeObject(paperData);
                paperObjects.Add(paperId, paperObject);
                foreach(var author in paperObject.authors)
                {
                    if (author.authorId == null) continue;
                    if (author.authorId.Value != authorId)
                        if (!coAuthors.ContainsKey(author.authorId.Value)) coAuthors.Add(author.authorId.Value, author.name.Value);
                }
            }
            /* Up to here we have acquired:
             * * Information about the author
             * * Information about all co-authors
             * * Information about the publications including:
             * * * All references within the publications;
             * * * All citations to the publications.
             * Now we have to construct information about the graph.
             * Nodes will contain papers summarised as (author et al. ...).
             * Nodes will have sizes - numbers of times cited.
             * Edges will be from citations to citations.
             */

            int selfCitations = 0;
            int coAuthorCitations = 0;
            int otherCitations = 0;
            Dictionary<string, int> paperNodeIds = new Dictionary<string, int>();
            Dictionary<int, GraphNode> graphNodes = new Dictionary<int, GraphNode>();
            Dictionary<int, HashSet<int>> edgeDict = new Dictionary<int, HashSet<int>>();
            Dictionary<int, GraphEdge> graphEdges = new Dictionary<int, GraphEdge>();
            int nextId = 0;
            int nextEdgeId = 0;
            foreach (string paperId in paperObjects.Keys)
            {
                var paperObject = paperObjects[paperId];
                int paperNodeIndex = -1;
                //Register current paper if it has not been registered yet.
                if (!paperNodeIds.ContainsKey(paperId))
                {
                    paperNodeIds.Add(paperId, nextId);
                    graphNodes.Add(nextId, new GraphNode());
                    GraphNode gn = graphNodes[nextId];
                    gn.size = paperObject.citations.Count+1;
                    gn.color = GraphColor.GetColor(ColorEnum.red);
                    if (paperObject.authors.Count==1)
                    {
                        string author = paperObject.authors[0].name;
                        if (author.Contains(" ")) author = author.Substring(author.LastIndexOf(' ') + 1);
                        gn.name = author + " (" + paperObject.year + ")";
                    }
                    else if (paperObject.authors.Count == 2)
                    {
                        string authorOne = paperObject.authors[0].name;
                        if (authorOne.Contains(" ")) authorOne = authorOne.Substring(authorOne.LastIndexOf(' ') + 1);
                        string authorTwo = paperObject.authors[1].name;
                        if (authorTwo.Contains(" ")) authorTwo = authorTwo.Substring(authorTwo.LastIndexOf(' ') + 1);
                        gn.name = authorOne + " & " + authorTwo + " (" + paperObject.year + ")";
                    }
                    else
                    {
                        string author = paperObject.authors[0].name;
                        if (author.Contains(" ")) author = author.Substring(author.LastIndexOf(' ') + 1);
                        gn.name = author + " et al. (" + paperObject.year + ")";
                    }
                    paperNodeIndex = nextId;
                    nextId++;
                }
                else
                {
                    paperNodeIndex = paperNodeIds[paperId];
                    graphNodes[paperNodeIndex].size = paperObject.citations.Count + 1;
                }
                if (!edgeDict.ContainsKey(paperNodeIndex)) edgeDict.Add(paperNodeIndex, new HashSet<int>());
                //Register all references if required
                if (!ignoreReferences)
                {
                    foreach (var reference in paperObject.references)
                    {
                        string refId = reference.paperId;
                        bool isSelfCitation = false;
                        bool isCoAuthorCitation = false;
                        int refNodeIndex = -1;
                        foreach (var refAuthor in reference.authors)
                        {
                            if (refAuthor.authorId == null) continue;
                            if (coAuthors.ContainsKey(refAuthor.authorId.Value)) isCoAuthorCitation = true;
                            if (refAuthor.authorId.Value == authorId) isSelfCitation = true;
                        }
                        if (!isCoAuthorCitation && !isSelfCitation && registerOnlyRelevantReferences) continue;
                        if (!paperNodeIds.ContainsKey(refId))
                        {
                            paperNodeIds.Add(refId, nextId);
                            graphNodes.Add(nextId, new GraphNode());
                            GraphNode gn = graphNodes[nextId];
                            gn.size = 1;
                            if (isSelfCitation) gn.color = GraphColor.GetColor(ColorEnum.red);
                            else if (isCoAuthorCitation) gn.color = GraphColor.GetColor(ColorEnum.orange);
                            else gn.color = GraphColor.GetColor(ColorEnum.green);
                            if (reference.authors.Count == 1)
                            {
                                string author = reference.authors[0].name.Value;
                                if (author.Contains(" ")) author = author.Substring(author.LastIndexOf(' ') + 1);
                                gn.name = author + " (" + reference.year + ")";
                            }
                            else if (reference.authors.Count == 2)
                            {
                                string authorOne = reference.authors[0].name.Value;
                                if (authorOne.Contains(" ")) authorOne = authorOne.Substring(authorOne.LastIndexOf(' ') + 1);
                                string authorTwo = reference.authors[1].name.Value;
                                if (authorTwo.Contains(" ")) authorTwo = authorTwo.Substring(authorTwo.LastIndexOf(' ') + 1);
                                gn.name = authorOne + " & " + authorTwo + " (" + reference.year + ")";
                            }
                            else if (reference.authors.Count > 0)
                            {
                                string author = reference.authors[0].name.Value;
                                if (author.Contains(" ")) author = author.Substring(author.LastIndexOf(' ') + 1);
                                gn.name = author + " et al. (" + reference.year + ")";
                            }
                            else
                            {
                                gn.name = reference.title.Value;
                            }
                            refNodeIndex = nextId;
                            nextId++;
                        }
                        else
                        {
                            refNodeIndex = paperNodeIds[refId];
                        }
                        if (!edgeDict[paperNodeIndex].Contains(refNodeIndex))
                        {
                            graphEdges.Add(nextEdgeId, new GraphEdge());
                            graphEdges[nextEdgeId].id = nextEdgeId;
                            graphEdges[nextEdgeId].from = paperNodeIndex;
                            graphEdges[nextEdgeId].to = refNodeIndex;
                            graphEdges[nextEdgeId].id = nextEdgeId;
                            graphEdges[nextEdgeId].weight = 1;
                            if (isSelfCitation)
                            {
                                graphEdges[nextEdgeId].color = GraphColor.GetColor(ColorEnum.red);
                            }
                            else if (isCoAuthorCitation)
                            {
                                graphEdges[nextEdgeId].color = GraphColor.GetColor(ColorEnum.orange);
                            }
                            else
                            {
                                graphEdges[nextEdgeId].color = GraphColor.GetColor(ColorEnum.green);
                            }
                            edgeDict[paperNodeIndex].Add(refNodeIndex);
                            nextEdgeId++;
                        }
                    }
                }

                //Register all citations
                foreach (var citation in paperObject.citations)
                {
                    string citId = citation.paperId;
                    bool isSelfCitation = false;
                    bool isCoAuthorCitation = false;
                    int citNodeIndex = -1;
                    foreach (var citAuthor in citation.authors)
                    {
                        if (citAuthor.authorId == null) continue;
                        if (coAuthors.ContainsKey(citAuthor.authorId.Value)) isCoAuthorCitation = true;
                        if (citAuthor.authorId == authorId) isSelfCitation = true;
                    }
                    if (isSelfCitation) selfCitations++;
                    else if (isCoAuthorCitation) coAuthorCitations++;
                    else otherCitations++;
                    if (!paperNodeIds.ContainsKey(citId))
                    {
                        paperNodeIds.Add(citId, nextId);
                        graphNodes.Add(nextId, new GraphNode());
                        GraphNode gn = graphNodes[nextId];
                        gn.size = 1;
                        if (isSelfCitation) gn.color = GraphColor.GetColor(ColorEnum.red);
                        else if (isCoAuthorCitation) gn.color = GraphColor.GetColor(ColorEnum.orange);
                        else gn.color = GraphColor.GetColor(ColorEnum.green);
                        if (citation.authors.Count == 1)
                        {
                            string author = citation.authors[0].name.Value;
                            if (author.Contains(" ")) author = author.Substring(author.LastIndexOf(' ') + 1);
                            gn.name = author + " (" + citation.year + ")";
                        }
                        else if (citation.authors.Count == 2)
                        {
                            string authorOne = citation.authors[0].name.Value;
                            if (authorOne.Contains(" ")) authorOne = authorOne.Substring(authorOne.LastIndexOf(' ') + 1);
                            string authorTwo = citation.authors[1].name.Value;
                            if (authorTwo.Contains(" ")) authorTwo = authorTwo.Substring(authorTwo.LastIndexOf(' ') + 1);
                            gn.name = authorOne + " & " + authorTwo + " (" + citation.year + ")";
                        }
                        else if (citation.authors.Count>0)
                        {
                            string author = citation.authors[0].name.Value;
                            if (author.Contains(" ")) author = author.Substring(author.LastIndexOf(' ') + 1);
                            gn.name = author + " et al. (" + citation.year + ")";
                        }
                        else
                        {
                            gn.name = citation.title.Value;
                        }
                        citNodeIndex = nextId;
                        nextId++;
                    }
                    else
                    {
                        citNodeIndex = paperNodeIds[citId];
                    }
                    if (!edgeDict.ContainsKey(citNodeIndex)) edgeDict.Add(citNodeIndex, new HashSet<int>());
                    if (!edgeDict[citNodeIndex].Contains(paperNodeIndex))
                    {
                        graphEdges.Add(nextEdgeId, new GraphEdge());
                        graphEdges[nextEdgeId].id = nextEdgeId;
                        graphEdges[nextEdgeId].from = citNodeIndex;
                        graphEdges[nextEdgeId].to = paperNodeIndex;
                        graphEdges[nextEdgeId].id = nextEdgeId;
                        graphEdges[nextEdgeId].weight = 1;
                        if (isSelfCitation)
                        {
                            graphEdges[nextEdgeId].color = GraphColor.GetColor(ColorEnum.red);
                        }
                        else if (isCoAuthorCitation)
                        {
                            graphEdges[nextEdgeId].color = GraphColor.GetColor(ColorEnum.orange);
                        }
                        else
                        {
                            graphEdges[nextEdgeId].color = GraphColor.GetColor(ColorEnum.green);
                        }
                        edgeDict[citNodeIndex].Add(paperNodeIndex);
                        nextEdgeId++;
                    }
                }
            }

            //We log relevant statistics in STD OUT.
            Console.Write(authorId);
            Console.Write("\t");
            Console.Write(authorName);
            Console.Write("\t");
            Console.Write(papers.Count);
            Console.Write("\t");
            Console.Write(selfCitations);
            Console.Write("\t");
            Console.Write(coAuthorCitations);
            Console.Write("\t");
            Console.WriteLine(otherCitations);

            //Create the GEXF file.
            XmlDocument xd = new XmlDocument();
            XmlDeclaration declaration = xd.CreateXmlDeclaration("1.0", "UTF-8", null);
            xd.AppendChild(declaration);
            XmlNode rootNode = xd.CreateElement("gexf");
            xd.AppendChild(rootNode);
            xd.DocumentElement.SetAttribute("xmlns", "http://www.gexf.net/1.2draft");
            xd.DocumentElement.SetAttribute("xmlns:viz", "http://www.gexf.net/1.2draft/viz");
            XmlAttribute version = xd.CreateAttribute("version");
            version.Value = "1.2";
            rootNode.Attributes.Append(version);
            XmlNode metaNode = xd.CreateElement("meta");
            XmlAttribute modifiedDate = xd.CreateAttribute("lastmodifieddate");
            modifiedDate.Value = DateTime.Now.ToString("yyyy-MM-dd");
            metaNode.Attributes.Append(modifiedDate);
            XmlNode creatorNode = xd.CreateElement("creator");
            creatorNode.InnerText = "GetSemanticScholarAuthorCitationGraph";
            metaNode.AppendChild(creatorNode);
            XmlNode descriptionNode = xd.CreateElement("description");
            descriptionNode.InnerText = "Citation graph for author "+authorId + " that "
                + (ignoreReferences?"does not include references.":"includes "
                    + (registerOnlyRelevantReferences? "references to co-authors and self.": "all references."));
            metaNode.AppendChild(descriptionNode);
            rootNode.AppendChild(metaNode);
            XmlNode graphNode = xd.CreateElement("graph");
            XmlAttribute modeAttribute = xd.CreateAttribute("mode");
            modeAttribute.Value = "static";
            graphNode.Attributes.Append(modeAttribute);
            XmlAttribute defaultEdgeTypeAttribute = xd.CreateAttribute("defaultedgetype");
            defaultEdgeTypeAttribute.Value = "directed";
            graphNode.Attributes.Append(defaultEdgeTypeAttribute);
            XmlElement nodesNode = xd.CreateElement("nodes");
            XmlElement edgesNode = xd.CreateElement("edges");
            //Add nodes (papers) to the graph.
            foreach(int id in graphNodes.Keys)
            {
                GraphNode gn = graphNodes[id];
                XmlNode nodeNode = xd.CreateElement("node");
                XmlAttribute idAttribute = xd.CreateAttribute("id");
                idAttribute.Value = id.ToString();
                nodeNode.Attributes.Append(idAttribute);
                XmlAttribute labelAttribute = xd.CreateAttribute("label");
                labelAttribute.Value = gn.name;
                nodeNode.Attributes.Append(labelAttribute);
                XmlNode colorNode = xd.CreateElement("viz","color", "http://www.gexf.net/1.2draft/viz");
                XmlAttribute rAttribute = xd.CreateAttribute("r");
                rAttribute.Value = gn.color.r.ToString();
                colorNode.Attributes.Append(rAttribute);
                XmlAttribute gAttribute = xd.CreateAttribute("g");
                gAttribute.Value = gn.color.g.ToString();
                colorNode.Attributes.Append(gAttribute);
                XmlAttribute bAttribute = xd.CreateAttribute("b");
                bAttribute.Value = gn.color.b.ToString();
                colorNode.Attributes.Append(bAttribute);
                XmlAttribute aAttribute = xd.CreateAttribute("a");
                aAttribute.Value = gn.color.a.ToString("0.00");
                colorNode.Attributes.Append(aAttribute);
                nodeNode.AppendChild(colorNode);
                nodesNode.AppendChild(nodeNode);
            }
            graphNode.AppendChild(nodesNode);
            //Add edges (citations and if required also references) to the graph.
            foreach (int id in graphEdges.Keys)
            {
                GraphEdge ge = graphEdges[id];
                XmlNode edgeNode = xd.CreateElement("edge");
                XmlAttribute idAttribute = xd.CreateAttribute("id");
                idAttribute.Value = id.ToString();
                edgeNode.Attributes.Append(idAttribute);
                XmlAttribute fromAttribute = xd.CreateAttribute("source");
                fromAttribute.Value = ge.from.ToString();
                edgeNode.Attributes.Append(fromAttribute);
                XmlAttribute toAttribute = xd.CreateAttribute("target");
                toAttribute.Value = ge.to.ToString();
                edgeNode.Attributes.Append(toAttribute);
                XmlNode colorNode = xd.CreateElement("color","viz");
                XmlAttribute rAttribute = xd.CreateAttribute("r");
                rAttribute.Value = ge.color.r.ToString();
                colorNode.Attributes.Append(rAttribute);
                XmlAttribute gAttribute = xd.CreateAttribute("g");
                gAttribute.Value = ge.color.g.ToString();
                colorNode.Attributes.Append(gAttribute);
                XmlAttribute bAttribute = xd.CreateAttribute("b");
                bAttribute.Value = ge.color.b.ToString();
                colorNode.Attributes.Append(bAttribute);
                XmlAttribute aAttribute = xd.CreateAttribute("a");
                aAttribute.Value = ge.color.a.ToString("0.00");
                colorNode.Attributes.Append(aAttribute);
                edgeNode.AppendChild(colorNode);
                edgesNode.AppendChild(edgeNode);
            }
            graphNode.AppendChild(edgesNode);
            rootNode.AppendChild(graphNode);
            //Save the GEXF file to the disk.
            xd.Save(outFile);
        }

        /// <summary>
        /// Prints usage/help information.
        /// </summary>
        private static void PrintUsage()
        {
            Console.WriteLine("GetSemanticScholarAuthorCitationGraph allows to query the Semantic Scholar API to build a citation graph for an author identified by an author ID (find the ID in the URL of the author's home page in Semantic Scholar).");
            Console.WriteLine("Usage:");
            Console.WriteLine("  Windows: .\\GetSemanticScholarAuthorCitationGraph [ARGS]");
            Console.WriteLine("  Linux: mono GetSemanticScholarAuthorCitationGraph [ARGS]");
            Console.WriteLine("  ARGS:");
            Console.WriteLine("        -o [File]                     - the output Graph Exchange XML Format (GEXF) file");
            Console.WriteLine("        -id [ID]                      - the author ID from Semantic Scholar");
            Console.WriteLine("        -y [Year]                     - starting which year to collect data (default: 0)");
            Console.WriteLine("        --include-relevant-references - include also references to co-author papers");
            Console.WriteLine("                                        (overrides all reference option)");
            Console.WriteLine("        --include-all-references      - include all references");
            Console.WriteLine("                                        (overrides relevant reference option)");
        }

        /// <summary>
        /// Exectutes a Web request and returns the response text.
        /// </summary>
        /// <param name="url">URL to execute</param>
        /// <returns>Response</returns>
        public static string ExecuteRequest(string url)
        {
            string html = string.Empty;
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.AutomaticDecompression = DecompressionMethods.GZip;

            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            using (Stream stream = response.GetResponseStream())
            using (StreamReader reader = new StreamReader(stream))
            {
                html = reader.ReadToEnd();
            }

            return html;
        }
    }
}
