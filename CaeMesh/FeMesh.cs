﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CaeGlobals;

namespace CaeMesh
{
    [Serializable]
    public enum MeshRepresentation
    {
        Geometry,
        Mesh,
        Results
    }

    [Serializable]
    public enum MeshType
    {
        Wire,
        Shell,
        Solid
    }

    [Serializable]
    class CellEdgeData
    {
        public int[] NodeIds;
        public List<int> CellIds;
    }

    [Serializable]
    public class FeMesh
    {
        // Variables                                                                                                                
        [NonSerialized]
        private Dictionary<int, FeNode> _nodes;
        [NonSerialized]
        private Dictionary<int, FeElement> _elements;

        private Dictionary<string, FeNodeSet> _nodeSets;
        private Dictionary<string, FeElementSet> _elementSets;
        private Dictionary<string, FeSurface> _surfaces;
        private Dictionary<string, FeReferencePoint> _referencePoints;
        private int _maxNodeId;
        private int _maxElementId;
        private BoundingBox _boundingBox;
        private Dictionary<string, BasePart> _parts;
        private MeshRepresentation _meshRepresentation;
        private bool _manifoldGeometry;


        // Properties                                                                                                               
        public Dictionary<int, FeNode> Nodes
        {
            get { return _nodes; }
            set { _nodes = value; }
        }
        public Dictionary<int, FeElement> Elements
        {
            get { return _elements; }
            set { _elements = value; }
        }
        public Dictionary<string, FeNodeSet> NodeSets
        {
            get { return _nodeSets; }
        }
        public Dictionary<string, FeElementSet> ElementSets
        {
            get { return _elementSets; }
        }
        public Dictionary<string, FeSurface> Surfaces
        {
            get { return _surfaces; }
        }
        public Dictionary<string, FeReferencePoint> ReferencePoints
        {
            get { return _referencePoints; }
        }
        public Dictionary<string, BasePart> Parts
        {
            get { return _parts; }
        }
        public int MaxNodeId
        {
            get { return _maxNodeId; }
        }
        public int MaxElementId
        {
            get { return _maxElementId; }
        }
        public bool ManifoldGeometry { get { return _manifoldGeometry; } }


        // Constructors                                                                                                             
        public FeMesh(Dictionary<int, FeNode> nodes, Dictionary<int, FeElement> elements, MeshRepresentation representation)
            : this(nodes, elements, representation, null)
        {
        }
        public FeMesh(Dictionary<int, FeNode> nodes, Dictionary<int, FeElement> elements, MeshRepresentation representation, 
                      List<InpElementSet> inpElementTypeSets)
            : this(nodes, elements, representation, inpElementTypeSets, null, false)
        {
        }
        public FeMesh(Dictionary<int, FeNode> nodes, Dictionary<int, FeElement> elements, MeshRepresentation representation,
                      List<InpElementSet> inpElementTypeSets, string partNamePrefix, bool convertToSecondOrder)
        {
            if (convertToSecondOrder) LinearToParabolic(ref nodes, ref elements);

            _nodes = nodes;
            _elements = elements;
            _meshRepresentation = representation;
            _manifoldGeometry = false;

            _nodeSets = new Dictionary<string, FeNodeSet>();
            _elementSets = new Dictionary<string, FeElementSet>();

            _surfaces = new Dictionary<string, FeSurface>();
            _referencePoints = new Dictionary<string, FeReferencePoint>();

            _parts = new Dictionary<string, BasePart>();
            ExtractPartsFast(inpElementTypeSets, partNamePrefix);

            FindMaxNodeAndElementIds();

            _boundingBox = new BoundingBox();
            ComputeBoundingBox();
        }
        public FeMesh(FeMesh mesh, string[] partsToKeep)
        {
            _parts = new Dictionary<string, BasePart>();
            foreach (var partName in partsToKeep)
            {
                _parts.Add(partName, mesh.Parts[partName].DeepCopy());
            }

            HashSet<int> nodeIds = new HashSet<int>();
            HashSet<int> elementIds = new HashSet<int>();
            foreach (var entry in _parts)
            {
                nodeIds.UnionWith(entry.Value.NodeLabels);
                elementIds.UnionWith(entry.Value.Labels);
            }

            _nodes = new Dictionary<int, FeNode>();
            foreach (var nodeId in nodeIds)
            {
                _nodes.Add(nodeId, mesh.Nodes[nodeId].DeepCopy());
            }

            _elements = new Dictionary<int, FeElement>();
            foreach (var elementId in elementIds)
            {
                _elements.Add(elementId, mesh.Elements[elementId].DeepCopy());
            }

            _nodeSets = new Dictionary<string, FeNodeSet>();
            _elementSets = new Dictionary<string, FeElementSet>();
            _surfaces = new Dictionary<string, FeSurface>();
            _referencePoints = new Dictionary<string, FeReferencePoint>();

            _maxNodeId = mesh._maxNodeId;
            _maxElementId = mesh._maxElementId;
            ComputeBoundingBox();
            _meshRepresentation = mesh._meshRepresentation;
            _manifoldGeometry = mesh.ManifoldGeometry;
        }

        private static void LinearToParabolic(ref Dictionary<int, FeNode> nodes, ref Dictionary<int, FeElement> elements)
        {
            CompareIntArray comparer = new CompareIntArray();
            Dictionary<int[], FeNode> midNodesDic = new Dictionary<int[], FeNode>(comparer);

            Dictionary<int, FeNode> nodesOut = new Dictionary<int, FeNode>(nodes);
            Dictionary<int, FeElement> elementsOut = new Dictionary<int, FeElement>();

            int maxNodeId = int.MinValue;
            foreach (var entry in nodes) if (entry.Key > maxNodeId) maxNodeId = entry.Key;

            int[] nodeIds;
            FeNode[] elNodes;
            FeElement linElement;
            FeElement parElement;
            foreach (var entry in elements)
            {
                linElement = entry.Value;
                if (linElement is LinearBeamElement)
                {
                    elNodes = new FeNode[3];                    
                    elNodes[0] = nodes[linElement.NodeIds[0]];
                    elNodes[1] = nodes[linElement.NodeIds[1]];
                    elNodes[2] = GetOrCreateMidNode(elNodes[0], elNodes[1], ref midNodesDic, ref maxNodeId);

                    nodeIds = new int[] { elNodes[0].Id, elNodes[1].Id, elNodes[2].Id };
                    parElement = new ParabolicBeamElement(entry.Key, nodeIds);
                }
                else if (linElement is LinearTriangleElement)
                {
                    elNodes = new FeNode[6];
                    elNodes[0] = nodes[linElement.NodeIds[0]];
                    elNodes[1] = nodes[linElement.NodeIds[1]];
                    elNodes[2] = nodes[linElement.NodeIds[2]];
                    elNodes[3] = GetOrCreateMidNode(elNodes[0], elNodes[1], ref midNodesDic, ref maxNodeId);
                    elNodes[4] = GetOrCreateMidNode(elNodes[1], elNodes[2], ref midNodesDic, ref maxNodeId);
                    elNodes[5] = GetOrCreateMidNode(elNodes[2], elNodes[0], ref midNodesDic, ref maxNodeId);

                    nodeIds = new int[] { elNodes[0].Id, elNodes[1].Id, elNodes[2].Id,
                                          elNodes[3].Id, elNodes[4].Id, elNodes[5].Id };
                    parElement = new ParabolicTriangleElement(entry.Key, nodeIds);
                }
                else if (linElement is LinearTetraElement linTetEl)
                {
                    elNodes = new FeNode[10];
                    elNodes[0] = nodes[linElement.NodeIds[0]];
                    elNodes[1] = nodes[linElement.NodeIds[1]];
                    elNodes[2] = nodes[linElement.NodeIds[2]];
                    elNodes[3] = nodes[linElement.NodeIds[3]];
                    elNodes[4] = GetOrCreateMidNode(elNodes[0], elNodes[1], ref midNodesDic, ref maxNodeId);
                    elNodes[5] = GetOrCreateMidNode(elNodes[1], elNodes[2], ref midNodesDic, ref maxNodeId);
                    elNodes[6] = GetOrCreateMidNode(elNodes[2], elNodes[0], ref midNodesDic, ref maxNodeId);
                    elNodes[7] = GetOrCreateMidNode(elNodes[0], elNodes[3], ref midNodesDic, ref maxNodeId);
                    elNodes[8] = GetOrCreateMidNode(elNodes[1], elNodes[3], ref midNodesDic, ref maxNodeId);
                    elNodes[9] = GetOrCreateMidNode(elNodes[2], elNodes[3], ref midNodesDic, ref maxNodeId);

                    nodeIds = new int[] { elNodes[0].Id, elNodes[1].Id, elNodes[2].Id, elNodes[3].Id,
                                          elNodes[4].Id, elNodes[5].Id, elNodes[6].Id,
                                          elNodes[7].Id, elNodes[8].Id, elNodes[9].Id };
                    parElement = new ParabolicTetraElement(entry.Key, nodeIds);
                }
                else throw new NotSupportedException();
                
                elementsOut.Add(parElement.Id, parElement);
            }

            // Add nodes
            foreach (var entry in midNodesDic) nodesOut.Add(entry.Value.Id, entry.Value);

            nodes = nodesOut;
            elements = elementsOut;
        }
        private static FeNode GetOrCreateMidNode(FeNode n1, FeNode n2, ref Dictionary<int[],FeNode> midNodes, ref int maxNodeId)
        {
            int[] ids;
            if (n1.Id < n2.Id) ids = new int[] { n1.Id, n2.Id };
            else ids = new int[] { n2.Id, n1.Id };

            double x, y, z;
            FeNode newNode;
            if (!midNodes.TryGetValue(ids, out newNode))
            {
                maxNodeId++;
                x = 0.5 * (n1.X + n2.X);
                y = 0.5 * (n1.Y + n2.Y);
                z = 0.5 * (n1.Z + n2.Z);
                newNode = new FeNode(maxNodeId, x, y, z);
                midNodes.Add(ids, newNode);
            }
            return newNode;
        }

        // Static methods                                                                                                           
        public static void WriteToBinaryFile(FeMesh mesh, System.IO.BinaryWriter bw)
        {
            if (mesh == null)
            {
                bw.Write((int)0);   // 0 nodes
                bw.Write((int)0);   // 0 elements
            }
            else
            {
                // Nodes
                Dictionary<int, FeNode> nodes = mesh.Nodes;
                if (nodes == null) bw.Write((int)0);
                else
                {
                    bw.Write(nodes.Count);
                    foreach (var entry in nodes)
                    {
                        bw.Write(entry.Key);
                        bw.Write(entry.Value.X);
                        bw.Write(entry.Value.Y);
                        bw.Write(entry.Value.Z);
                    }
                }

                // Elements
                Dictionary<int, FeElement> elements = mesh.Elements;
                if (elements == null) bw.Write((int)0);
                else
                {
                    bw.Write(elements.Count);
                    foreach (var entry in elements)
                    {
                        bw.Write(entry.Value.PartId);
                        bw.Write(entry.Key);
                        bw.Write(entry.Value.GetVtkCellType());

                        bw.Write(entry.Value.NodeIds.Length);
                        for (int i = 0; i < entry.Value.NodeIds.Length; i++)
                        {
                            bw.Write(entry.Value.NodeIds[i]);
                        }
                    }
                }
            }
        }
        public static void ReadFromBinaryFile(FeMesh mesh, System.IO.BinaryReader br)
        {
            if (mesh.Nodes == null) mesh.Nodes = new Dictionary<int, FeNode>();
            else mesh.Nodes.Clear();

            int numOfNodes = br.ReadInt32();
            int id;
            double x, y, z;
            for (int i = 0; i < numOfNodes; i++)
            {
                id = br.ReadInt32();
                x = br.ReadDouble();
                y = br.ReadDouble();
                z = br.ReadDouble();
                mesh.Nodes.Add(id, new FeNode(id, x, y, z));
            }

            if (mesh.Elements == null) mesh.Elements = new Dictionary<int, FeElement>();
            else mesh.Elements.Clear();

            int partId;
            int[] nodeIds;
            vtkCellType cellType;
            int numOfElements = br.ReadInt32();

            for (int i = 0; i < numOfElements; i++)
            {
                partId = br.ReadInt32();
                id = br.ReadInt32();
                cellType = (vtkCellType)br.ReadInt32();

                numOfNodes = br.ReadInt32();
                nodeIds = new int[numOfNodes];
                for (int j = 0; j < numOfNodes; j++)
                {
                    nodeIds[j] = br.ReadInt32();
                }

                switch (cellType)
                {
                    case vtkCellType.VTK_LINE:
                        mesh.Elements.Add(id, new LinearBeamElement(id, nodeIds) { PartId = partId });
                        break;
                    case vtkCellType.VTK_QUADRATIC_EDGE:
                        mesh.Elements.Add(id, new ParabolicBeamElement(id, nodeIds) { PartId = partId });
                        break;
                    case vtkCellType.VTK_TRIANGLE:
                        mesh.Elements.Add(id, new LinearTriangleElement(id, nodeIds) { PartId = partId });
                        break;
                    case vtkCellType.VTK_QUAD:
                        mesh.Elements.Add(id, new LinearQuadrilateralElement(id, nodeIds) { PartId = partId });
                        break;
                    case vtkCellType.VTK_QUADRATIC_TRIANGLE:
                        mesh.Elements.Add(id, new ParabolicTriangleElement(id, nodeIds) { PartId = partId });
                        break;
                    case vtkCellType.VTK_QUADRATIC_QUAD:
                        mesh.Elements.Add(id, new ParabolicQuadrilateralElement(id, nodeIds) { PartId = partId });
                        break;
                    case vtkCellType.VTK_TETRA:
                        mesh.Elements.Add(id, new LinearTetraElement(id, nodeIds) { PartId = partId });
                        break;
                    case vtkCellType.VTK_WEDGE:
                        mesh.Elements.Add(id, new LinearWedgeElement(id, nodeIds) { PartId = partId });
                        break;
                    case vtkCellType.VTK_HEXAHEDRON:
                        mesh.Elements.Add(id, new LinearHexaElement(id, nodeIds) { PartId = partId });
                        break;
                    case vtkCellType.VTK_QUADRATIC_TETRA:
                        mesh.Elements.Add(id, new ParabolicTetraElement(id, nodeIds) { PartId = partId });
                        break;
                    case vtkCellType.VTK_QUADRATIC_WEDGE:
                        mesh.Elements.Add(id, new ParabolicWedgeElement(id, nodeIds) { PartId = partId });
                        break;
                    case vtkCellType.VTK_QUADRATIC_HEXAHEDRON:
                        mesh.Elements.Add(id, new ParabolicHexaElement(id, nodeIds) { PartId = partId });
                        break;
                    default:
                        break;
                }
            }
        }
       

        // Methods                                                                                                                  
        public string[] CheckValidity(List<Tuple<NamedClass, string>> items)
        {
            // Tuple<NamedClass, string>   ...   Tuple<invalidItem, stepName>
            List<string> invalidItems = new List<string>();
            bool valid;

            // Node set
            FeNodeSet nodeSet;
            foreach (var entry in _nodeSets)
            {
                nodeSet = entry.Value;
                if (!nodeSet.Valid)
                {
                    items.Add(new Tuple<NamedClass, string>(nodeSet, null));
                    invalidItems.Add("Node set: " + nodeSet.Name);
                }
            }

            // Element set
            FeElementSet elementSet;
            foreach (var entry in _elementSets)
            {
                elementSet = entry.Value;
                if (!elementSet.Valid)
                {
                    items.Add(new Tuple<NamedClass, string>(elementSet, null));
                    invalidItems.Add("Element set: " + elementSet.Name);
                }
            }

            // Surfaces
            FeSurface surface = null;
            foreach (var entry in _surfaces)
            {
                surface = entry.Value;

                valid = surface.Valid;              // this is set to invalid after deleting a part
                if (!valid) surface.Valid = true;   // set this to true to detect a change in validity

                // type node and created from selection - surface creates a node set with name: surface.NodeSetName
                valid &= !(surface.Type == FeSurfaceType.Node && surface.CreatedFrom == FeSurfaceCreatedFrom.Selection
                          && !_nodeSets.ContainsValidKey(surface.NodeSetName));
                // from node set
                valid &= !(surface.CreatedFrom == FeSurfaceCreatedFrom.NodeSet && !_nodeSets.ContainsValidKey(surface.CreatedFromNodeSetName));
                // has element faces
                if (surface.ElementFaces != null)
                {
                    foreach (var faceEntry in surface.ElementFaces) valid &= _elementSets.ContainsValidKey(faceEntry.Value);
                }
                valid &= !(surface.Type == FeSurfaceType.Element && surface.Area <= 0);
                SetItemValidity(surface, valid, items);
                if (!valid && surface.Active) invalidItems.Add("Surface: " + surface.Name);
            }

            // Reference points
            FeReferencePoint referencePoint;
            foreach (var entry in _referencePoints)
            {
                referencePoint = entry.Value;
                valid = !(referencePoint.CreatedFrom != FeReferencePointCreatedFrom.Coordinates && !_nodeSets.ContainsValidKey(referencePoint.NodeSetName));
                SetItemValidity(referencePoint, valid, items);
                if (!valid && referencePoint.Active) invalidItems.Add("Reference point: " + referencePoint.Name);
            }

            return invalidItems.ToArray();
        }
        private void SetItemValidity(NamedClass item, bool validity, List<Tuple<NamedClass, string>> items)
        {
            // only changed items are added for the update
            if (item.Valid != validity)
            {
                item.Valid = validity;
                items.Add(new Tuple<NamedClass, string>(item, null));
            }
        }

        // Compare 
        public bool IsEqual(FeMesh mesh)
        {
            if (_nodes.Count != mesh.Nodes.Count) return false;
            if (_elements.Count != mesh.Elements.Count) return false;

            int n = Math.Min(1000, _nodes.Count);
            n = Math.Max(_nodes.Count / 1000, n);

            int nodeId;
            int[] keys = _nodes.Keys.ToArray();

            Random rand = new Random();
            FeNode node1;
            FeNode node2;
            for (int i = 0; i < n; i++)
            {
                nodeId = (int)(rand.NextDouble() * (keys.Length - 1));
                if (_nodes.TryGetValue(keys[nodeId], out node1) && mesh.Nodes.TryGetValue(keys[nodeId], out node2))
                {
                    if (!node1.IsEqual(node2))
                        return false;
                }
                else
                    return false;
            }
            return true;
        }

        private void FindMaxNodeAndElementIds()
        {
            // determine max node id
            _maxNodeId = 0;
            foreach (var entry in _nodes)
            {
                if (entry.Value.Id > _maxNodeId) _maxNodeId = entry.Value.Id;
            }

            // determine max element id
            _maxElementId = 0;
            foreach (var entry in _elements)
            {
                if (entry.Value.Id > _maxElementId) _maxElementId = entry.Value.Id;
            }
        }
        private void ComputeBoundingBox()
        {
            if (_boundingBox == null) _boundingBox = new BoundingBox();
            _boundingBox.Reset();

            if (_nodes != null && _nodes.Count > 0)
            {
                FeNode node;
                foreach (var entry in _parts)
                {
                    entry.Value.BoundingBox.Reset();
                    for (int i = 0; i < entry.Value.NodeLabels.Length; i++)
                    {
                        node = _nodes[entry.Value.NodeLabels[i]];
                        entry.Value.BoundingBox.CheckNode(node);
                        _boundingBox.CheckNode(node);
                    }
                }
            }
        }
        
        public double GetBoundingBoxSize()
        {
            return Math.Sqrt(Math.Pow(_boundingBox.MinX - _boundingBox.MaxX, 2) +
                             Math.Pow(_boundingBox.MinY - _boundingBox.MaxY, 2) +
                             Math.Pow(_boundingBox.MinZ - _boundingBox.MaxZ, 2));
        }

        public double GetBoundingBoxVolumeAsCubeSide()
        {
            return Math.Pow((_boundingBox.MaxX - _boundingBox.MinX) * 
                            (_boundingBox.MaxY - _boundingBox.MinY) *
                            (_boundingBox.MaxZ - _boundingBox.MinZ), 1.0 / 3.0);


        }

        public void ResetPartsColor()
        {
            foreach (var entry in _parts)
            {
                if (entry.Value.Color == System.Drawing.Color.Gray) // Gray if default color
                    //entry.Value.Color = Globals.PartColors[startWith++ % Globals.PartColors.Length];
                    SetPartsColorFromId(entry.Value);
            }
        }
        public void SetPartsColor(System.Drawing.Color color)
        {
            foreach (var entry in _parts)
            {
                entry.Value.Color = color;
            }
        }
        public void SetPartsColorFromId(BasePart part)
        {
            part.Color = Globals.PartColors[(part.PartId - 1) % Globals.PartColors.Length];
        }

        #region Parts  #############################################################################################################
        private void ExtractPartsFast(List<InpElementSet> inpElementTypeSets, string namePrefix)
        {
            System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();
            watch.Start();

            // for each node find it's connected elements
            FeElement element;
            Dictionary<int, List<FeElement>> nodeElements = new Dictionary<int, List<FeElement>>();

            foreach (var entry in _elements)
            {
                element = entry.Value;
                element.PartId = -1;

                foreach (var nodeId in element.NodeIds)
                {
                    if (nodeElements.ContainsKey(nodeId)) nodeElements[nodeId].Add(element);
                    else nodeElements.Add(nodeId, new List<FeElement>() { element });
                }
            }

            int partId = 0;
            string name;
            BasePart part;
            HashSet<int> partNodeIds = new HashSet<int>();
            List<int> sortedPartNodeIds = new List<int>();
            List<int> partElementIds = new List<int>();
            HashSet<Type> partElementTypes = new HashSet<Type>();
            HashSet<string> inpElementTypeNames = null;
            HashSet<int> inpElementTypeSetLabels = null;

            foreach (var entry in _elements)
            {
                element = entry.Value;
                if (element.PartId == -1)
                {
                    partId++;
                    element.PartId = partId;    // set Part Id to the seed element of the Flood Fill

                    partNodeIds.Clear();
                    partNodeIds.UnionWith(element.NodeIds);

                    partElementIds.Clear();
                    partElementIds.Add(entry.Key);

                    partElementTypes.Clear();
                    partElementTypes.Add(element.GetType());

                    // find inp element type set
                    if (inpElementTypeSets != null)
                    {
                        foreach (var elementTypeEntry in inpElementTypeSets)
                        {
                            if (elementTypeEntry.ElementLabels.Contains(element.Id))
                            {
                                if (inpElementTypeNames == null) inpElementTypeNames = new HashSet<string>();
                                inpElementTypeNames.UnionWith(elementTypeEntry.InpElementTypeNames);

                                inpElementTypeSetLabels = elementTypeEntry.ElementLabels;
                                break;
                            }
                        }
                    }

                    if (namePrefix != null && namePrefix != "") name = namePrefix + "-";
                    else name = "";

                    if (element is FeElement1D)
                    {
                        FloodFillFast<FeElement1D>(element, partId, nodeElements, ref partNodeIds, ref partElementIds, ref partElementTypes, inpElementTypeSetLabels);
                        name += "Wire_Part-";
                    }
                    else if (element is FeElement2D)
                    {
                        FloodFillFast<FeElement2D>(element, partId, nodeElements, ref partNodeIds, ref partElementIds, ref partElementTypes, inpElementTypeSetLabels);
                        name += "Shell_Part-";
                    }
                    else if (element is FeElement3D) 
                    {
                        FloodFillFast<FeElement3D>(element, partId, nodeElements, ref partNodeIds, ref partElementIds, ref partElementTypes, inpElementTypeSetLabels);
                        name += "Solid_Part-";
                    }
                    else throw new NotSupportedException();

                    // sort node ids
                    sortedPartNodeIds = new List<int>(partNodeIds);
                    sortedPartNodeIds.Sort();

                    // sort element ids
                    partElementIds.Sort();

                    name = NamedClass.GetNewValueName(_parts.Keys.ToArray(), name);
                  
                    if (_meshRepresentation == MeshRepresentation.Geometry)
                        part = new GeometryPart(name, partId, sortedPartNodeIds.ToArray(), partElementIds.ToArray(), partElementTypes.ToArray());
                    else if (_meshRepresentation == MeshRepresentation.Mesh)
                    {
                        part = new MeshPart(name, partId, sortedPartNodeIds.ToArray(), partElementIds.ToArray(), partElementTypes.ToArray());
                        if (inpElementTypeNames != null) (part as MeshPart).SetPropertiesFromInpElementTypeName(inpElementTypeNames.ToArray());
                    }
                    else if (_meshRepresentation == MeshRepresentation.Results)
                        part = new ResultPart(name, partId, sortedPartNodeIds.ToArray(), partElementIds.ToArray(), partElementTypes.ToArray());
                    else throw new NotSupportedException();

                    _parts.Add(name, part);
                }
            }
            watch.Stop();

            GeometryPart geometryPart;
            List<string> partsToRename = new List<string>();
            foreach (var entry in _parts)
            {
                part = entry.Value;
                if (part.PartType == PartType.Solid)
                {
                    ExtractSolidPartVisualization(part);
                }
                else if (part.PartType == PartType.Shell)
                {
                    ExtractShellPartVisualization(part);

                    if (part is GeometryPart)
                    {
                        // collect closed shell part names
                        geometryPart = part as GeometryPart;
                        if (geometryPart.ErrorNodeIds == null && geometryPart.ErrorElementIds == null) partsToRename.Add(geometryPart.Name);
                    }
                }
                else if (part.PartType == PartType.Wire)
                {
                    ExtractWirePartVisualization(part);
                }
            }

            // rename closed shell parts to solid parts
            foreach (var partName in partsToRename)
            {
                geometryPart = Parts[partName] as GeometryPart;
                Parts.Remove(partName);
                geometryPart.Name = geometryPart.Name.Replace("Shell", "Solid");
                Parts.Add(geometryPart.Name, geometryPart);
            }

            //ResetPartsColor();
        }
        private void FloodFillFast<T>(FeElement element, int partId, Dictionary<int, List<FeElement>> nodeElements, ref HashSet<int> partNodeIds,
                                      ref List<int> partElementIds, ref HashSet<Type> partElementTypes, HashSet<int> elementTypeSet)
        {
            UniqueQueue<FeElement> neighbours = new UniqueQueue<FeElement>();
            neighbours.Enqueue(element);

            FeElement el;
            while (neighbours.Count > 0)
            {
                el = neighbours.Dequeue();

                foreach (var nodeId in el.NodeIds)
                {
                    foreach (var currEl in nodeElements[nodeId])
                    {
                        if (currEl.PartId == -1 && currEl is T && !(elementTypeSet != null && !elementTypeSet.Contains(currEl.Id)))
                        {
                            currEl.PartId = partId;
                            neighbours.Enqueue(currEl);
                            partNodeIds.UnionWith(currEl.NodeIds);
                            partElementIds.Add(currEl.Id);
                            partElementTypes.Add(currEl.GetType());
                        }
                    }
                }
            }
        }

        // Visualization
        private void ExtractSolidPartVisualization(BasePart part)
        {
            System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();
            watch.Start();

            int[] elementIds = part.Labels;
            int[] sorted;
            CompareIntArray comparer = new CompareIntArray();
            Dictionary<int[], Tuple<int, int[]>> allCells = new Dictionary<int[], Tuple<int, int[]>>(elementIds.Length / 2, comparer);
            foreach (var id in elementIds)
            {
                foreach (int[] cell in ((FeElement3D)_elements[id]).GetAllVtkCells())
                {
                    sorted = cell.ToArray();
                    Array.Sort(sorted);
                    if (!allCells.Remove(sorted)) allCells.Add(sorted, new Tuple<int, int[]>(id, cell));
                }
            }
            watch.Stop();

            int[][] visualizationCells = new int[allCells.Count][];
            int[] visualizationCellsIds = new int[allCells.Count];
            int count = 0;
            foreach (var entry in allCells)
            {
                visualizationCellsIds[count] = entry.Value.Item1;
                visualizationCells[count++] = entry.Value.Item2.ToArray();
            }

            part.Visualization.CellIds = visualizationCellsIds;
            part.Visualization.Cells = visualizationCells;
            ExtractEdgesFromShellByAngle(part, 30);

            SplitVisualization(part);
        }
        private void ExtractShellPartVisualization(BasePart part)
        {
            System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();
            watch.Start();

            int count;
            int[] elementIds = part.Labels;
            int[][] visualizationCells = new int[elementIds.Length][];
            int[] visualizationCellsIds = new int[elementIds.Length];

            count = 0;
            foreach (var id in elementIds)
            {
                visualizationCellsIds[count] = id;
                visualizationCells[count++] = ((FeElement2D)_elements[id]).GetVtkNodeIds();
            }

            part.Visualization.CellIds = visualizationCellsIds;
            part.Visualization.Cells = visualizationCells;
            ExtractEdgesFromShellByAngle(part, 30);

            SplitVisualization(part);
        }
        private void ExtractWirePartVisualization(BasePart part)
        {
            int n = part.Labels.Length;
            int[] elementIds = part.Labels;

            int[] visualizationCellsIds = part.Labels;
            int[][] visualizationCells = new int[n][];

            int count = 0;
            foreach (var id in elementIds)
            {
                visualizationCells[count++] = _elements[id].GetVtkNodeIds();
            }

            part.Visualization.CellIds = visualizationCellsIds;
            part.Visualization.Cells = visualizationCells;
        }
        private void ExtractEdgesFromShellByAngle(BasePart part, double angle)
        {
            int[][] cells = part.Visualization.Cells;
            CompareIntArray comparer = new CompareIntArray();
            Dictionary<int[], CellEdgeData> allEdges = new Dictionary<int[], CellEdgeData>(comparer);

            int[] key;           
            CellEdgeData data;
            int[][] cellEdges;

            System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();
            watch.Start();

            // get all edges
            for (int i = 0; i < cells.Length; i++)
            {
                cellEdges = GetVisualizationEdgeCells(cells[i]);

                foreach (var cellEdge in cellEdges)
                {
                    key = cellEdge.ToArray();
                    Array.Sort(key);

                    if (key[0] == key[1] || (key.Length == 3 && key[1] == key[2]))
                    {
                        _manifoldGeometry = true;
                        continue;
                    }

                    if (allEdges.TryGetValue(key, out data))
                    {
                        if (data.CellIds.Count > 1)
                            _manifoldGeometry = true;
                        data.CellIds.Add(i);
                    }
                    else
                    {
                        allEdges.Add(key, new CellEdgeData() { NodeIds = cellEdge, CellIds = new List<int>() { i } });
                    }
                }
            }

            watch.Stop();

            // get only edges where cellst met at an angle > input angle
            // get free surface edges
            int[] cellsIds;
            int visualizationCell1i;
            int visualizationCell2i;
            double phi;
            List<int[]> edgeCells = new List<int[]>();

            angle *= Math.PI / 180;
            foreach (var entry in allEdges)
            {
                data = entry.Value;
                cellsIds = data.CellIds.ToArray();      // for faster loops

                if (cellsIds.Length == 1)               // free edges
                {
                    edgeCells.Add(data.NodeIds);
                    continue;
                }

                for (int i = 0; i < cellsIds.Length - 1; i++)
                {
                    for (int j = i + 1; j < cellsIds.Length; j++)
                    {
                        visualizationCell1i = cellsIds[i];
                        visualizationCell2i = cellsIds[j];

                        phi = ComputeAngleInRadFromCellIndices(cells[visualizationCell1i], cells[visualizationCell2i]);
                        if (phi < 0) phi = -phi;
                        if (phi > Math.PI / 2) phi = Math.PI - phi;
                        if (phi > angle) edgeCells.Add(data.NodeIds);
                    }
                }
            }
            part.Visualization.EdgeCells = edgeCells.ToArray();


            // get cell neighbours
            HashSet<int>[] cellNeighbours = new HashSet<int>[cells.Length];
            foreach (var entry in allEdges)
            {
                cellsIds = entry.Value.CellIds.ToArray();       // for faster loops

                if (cellsIds.Length == 1) continue;

                for (int i = 0; i < cellsIds.Length - 1; i++)
                {
                    for (int j = i + 1; j < cellsIds.Length; j++)
                    {
                        visualizationCell1i = cellsIds[i];
                        visualizationCell2i = cellsIds[j];

                        if (cellNeighbours[visualizationCell1i] == null) cellNeighbours[visualizationCell1i] = new HashSet<int>();
                        if (cellNeighbours[visualizationCell2i] == null) cellNeighbours[visualizationCell2i] = new HashSet<int>();

                        cellNeighbours[visualizationCell1i].Add(visualizationCell2i);
                        cellNeighbours[visualizationCell2i].Add(visualizationCell1i);
                    }
                }
            }

            List<int> badElementIds = new List<int>();
            List<int> badNodeIds = new List<int>();
            int[][] cellNeighboursArray = new int[cellNeighbours.Length][];
            int numNeighbours;
            for (int i = 0; i < cellNeighbours.Length; i++)
            {
                if (cells[i].Length == 3 || cells[i].Length == 6) numNeighbours = 3;
                else numNeighbours = 4;

                if (cellNeighbours[i] != null) cellNeighboursArray[i] = cellNeighbours[i].ToArray();
                // check for bad elements
                if (cellNeighbours[i] == null || cellNeighbours[i].Count != numNeighbours)
                {
                    badElementIds.Add(part.Visualization.CellIds[i]);
                    badNodeIds.AddRange(_elements[part.Visualization.CellIds[i]].NodeIds);
                }
            }
            part.Visualization.CellNeighboursOverEdge = cellNeighboursArray;

            if (part is GeometryPart gp)
            {
                if (badElementIds.Count > 0)
                    gp.ErrorElementIds = badElementIds.ToArray();
                if (badNodeIds.Count > 0)
                    gp.ErrorNodeIds = badNodeIds.ToArray();
            }
        }
        private void SplitVisualization(BasePart part)
        {
            SplitEdgesByVertices(part);
            ComputeEdgeLengths(part);
            SplitVisualizationFacesByEdges(part);
            ComputeFaceAreas(part);
        }
        private void SplitEdgesByVertices(BasePart part)
        {
            part.Visualization.EdgeCellIdsByEdge = null;

            // build part edges map
            CompareIntArray comparer = new CompareIntArray();
            Dictionary<int, List<int>> nodeEdgeCellIds = new Dictionary<int, List<int>>();  // this are edge cells connected to the node
            List<int> edgeCellIds;
            int nodeId;
            int[][] allEdgeCells = part.Visualization.EdgeCells;
            for (int i = 0; i < allEdgeCells.Length; i++)
            {
                for (int j = 0; j < 2; j++)
                {
                    nodeId = allEdgeCells[i][j];
                    if (nodeEdgeCellIds.TryGetValue(nodeId, out edgeCellIds)) edgeCellIds.Add(i);
                    else nodeEdgeCellIds.Add(nodeId, new List<int>() { i });
                }
            }

            HashSet<int> verticeNodeIds = new HashSet<int>();       // vertice connects more than two edge cells
            foreach (var entry in nodeEdgeCellIds)
            {
                if (entry.Value.Count != 2) verticeNodeIds.Add(entry.Key);
            }

            HashSet<int>[] edgeNeighboursHash = new HashSet<int>[allEdgeCells.Length];  // find edge neighbours
            foreach (var entry in nodeEdgeCellIds)
            {
                foreach (var edge1Id in entry.Value)
                {
                    if (edgeNeighboursHash[edge1Id] == null) edgeNeighboursHash[edge1Id] = new HashSet<int>();

                    foreach (var edge2Id in entry.Value)
                    {
                        if (edge1Id == edge2Id) continue;
                        else edgeNeighboursHash[edge1Id].Add(edge2Id);
                    }
                }
            }

            // speed up
            int[][] edgeNeighbours = new int[allEdgeCells.Length][];
            for (int i = 0; i < edgeNeighbours.Length; i++) edgeNeighbours[i] = edgeNeighboursHash[i].ToArray();

            // spread
            int[] oneEdgeCells;
            List<int[]> edges = new List<int[]>();
            HashSet<int> visitedEdgeCellIds = new HashSet<int>();

            for (int i = 0; i < allEdgeCells.Length; i++)
            {
                if (!visitedEdgeCellIds.Contains(i))
                {
                    oneEdgeCells = GetSplitEdgeByEdgeCellId(part, i, edgeNeighbours, verticeNodeIds);
                    edges.Add(oneEdgeCells);
                    visitedEdgeCellIds.UnionWith(oneEdgeCells);
                }
            }

            part.Visualization.VertexNodeIds = verticeNodeIds.ToArray();
            part.Visualization.EdgeCellIdsByEdge = edges.ToArray();            
        }
        private void ComputeEdgeLengths(BasePart part)
        {
            int edgeCellId;
            int[] edgeCell;
            Vec3D v1;
            Vec3D v2;
            VisualizationData visualization = part.Visualization;
            double[] edgeLengths = new double[visualization.EdgeCellIdsByEdge.Length];

            // for each edge
            for (int i = 0; i < visualization.EdgeCellIdsByEdge.Length; i++)                
            {
                edgeLengths[i] = 0;
                // for each edge cell
                for (int j = 0; j < visualization.EdgeCellIdsByEdge[i].Length; j++)         
                {
                    edgeCellId = visualization.EdgeCellIdsByEdge[i][j];
                    edgeCell = visualization.EdgeCells[edgeCellId];

                    if (edgeCell.Length == 2)
                    {
                        v1 = new Vec3D(_nodes[edgeCell[0]].Coor);
                        v2 = new Vec3D(_nodes[edgeCell[1]].Coor);
                        edgeLengths[i] += (v2 - v1).Len;
                    }
                    else if (edgeCell.Length == 3)
                    {
                        v1 = new Vec3D(_nodes[edgeCell[0]].Coor);
                        v2 = new Vec3D(_nodes[edgeCell[2]].Coor);
                        edgeLengths[i] += (v2 - v1).Len;

                        v1 = new Vec3D(_nodes[edgeCell[1]].Coor);
                        edgeLengths[i] += (v2 - v1).Len;
                    }
                    else throw new NotSupportedException();
                }
            }

            visualization.EdgeLengths = edgeLengths;
        }
        private void SplitVisualizationFacesByEdges(BasePart part)
        {
            part.Visualization.CellIdsByFace = null;

            if (part.PartType == PartType.Solid || part.PartType == PartType.Shell)
            {
                // build part edges map
                CompareIntArray comparer = new CompareIntArray();
                Dictionary<int[], int> modelEdges = new Dictionary<int[], int>(comparer);
                int[] sortedNodes;
                for (int i = 0; i < part.Visualization.EdgeCellIdsByEdge.Length; i++)
                {
                    foreach (var edgeCellId in part.Visualization.EdgeCellIdsByEdge[i])
                    {
                        sortedNodes = part.Visualization.EdgeCells[edgeCellId].ToArray();
                        Array.Sort(sortedNodes);
                        if (!modelEdges.ContainsKey(sortedNodes)) modelEdges.Add(sortedNodes, i);
                        else
                        {
                            // this gets hit on element selection on parabolic meshes
                            int error = 1;
                        }
                    }
                }

                int[] faceCellIds;
                int[] faceEdgeIds;
                List<int[]> visualizationFaces = new List<int[]>();
                List<int[]> faceEdges = new List<int[]>();
                HashSet<int> visitedVisualizationCellIds = new HashSet<int>();

                for (int cellId = 0; cellId < part.Visualization.CellIds.Length; cellId++)
                {
                    if (!visitedVisualizationCellIds.Contains(cellId))
                    {
                        GetSplitVisualizationFaceByCellId(part, cellId, modelEdges, out faceCellIds, out faceEdgeIds);
                        visualizationFaces.Add(faceCellIds);
                        faceEdges.Add(faceEdgeIds);
                        visitedVisualizationCellIds.UnionWith(faceCellIds);
                    }
                }

                part.Visualization.CellIdsByFace = visualizationFaces.ToArray();
                part.Visualization.FaceEdgeIds = faceEdges.ToArray();
            }
        }
        private void ComputeFaceAreas(BasePart part)
        {
            int faceCellId;
            int[] cell;
            VisualizationData visualization = part.Visualization;
            double[] faceAreas = new double[visualization.CellIdsByFace.Length];

            // for each face
            for (int i = 0; i < visualization.CellIdsByFace.Length; i++)                
            {
                faceAreas[i] = 0;
                // for each face cell
                for (int j = 0; j < visualization.CellIdsByFace[i].Length; j++)        
                {
                    faceCellId = visualization.CellIdsByFace[i][j];
                    cell = visualization.Cells[faceCellId];

                    if (cell.Length == 3)
                        faceAreas[i] += GeometryTools.TriangleArea(_nodes[cell[0]], _nodes[cell[1]], _nodes[cell[2]]);
                    else if (cell.Length == 4)
                        faceAreas[i] += GeometryTools.RectangleArea(_nodes[cell[0]], _nodes[cell[1]], _nodes[cell[2]],
                                                                    _nodes[cell[3]]);
                    else if (cell.Length == 6)
                        faceAreas[i] += GeometryTools.TriangleArea(_nodes[cell[0]], _nodes[cell[1]], _nodes[cell[2]],
                                                                   _nodes[cell[3]], _nodes[cell[4]], _nodes[cell[5]]);
                    else if (cell.Length == 8)
                        faceAreas[i] += GeometryTools.RectangleArea(_nodes[cell[0]], _nodes[cell[1]], _nodes[cell[2]],
                                                                    _nodes[cell[3]], _nodes[cell[4]], _nodes[cell[5]],
                                                                    _nodes[cell[6]], _nodes[cell[7]]);
                    else throw new NotSupportedException();
                }
            }

            visualization.FaceAreas = faceAreas;
        }
        public double ComputeAngleInRadFromCellIndices(int[] cell1, int[] cell2)
        {
            FeNode n1 = ComputeNormalFromCellIndices(cell1);
            FeNode n2 = ComputeNormalFromCellIndices(cell2);

            double scalar = n1.X * n2.X + n1.Y * n2.Y + n1.Z * n2.Z;
            if (scalar > 1) return 0;
            else if (scalar < -1) return Math.PI;
            else return Math.Acos(scalar);
        }
        private FeNode ComputeNormalFromCellIndices(int[] cell)
        {
            FeNode node1 = _nodes[cell[0]];
            FeNode node2 = _nodes[cell[1]];
            FeNode node3 = _nodes[cell[2]];

            return ComputeNormalFromCellIndices(node1, node2, node3);
        }
        private FeNode ComputeNormalFromCellIndices(FeNode n1, FeNode n2, FeNode n3)
        {
            FeNode v = new FeNode(0, n2.X - n1.X, n2.Y - n1.Y, n2.Z - n1.Z);
            FeNode w = new FeNode(0, n3.X - n1.X, n3.Y - n1.Y, n3.Z - n1.Z);

            FeNode n = new FeNode();
            n.X = v.Y * w.Z - v.Z * w.Y;
            n.Y = v.Z * w.X - v.X * w.Z;
            n.Z = v.X * w.Y - v.Y * w.X;

            double d = Math.Sqrt(Math.Pow(n.X, 2) + Math.Pow(n.Y, 2) + Math.Pow(n.Z, 2));
            if (d != 0)
            {
                n.X /= d;
                n.Y /= d;
                n.Z /= d;
            }
            return n;
        }
        public int[] GetSplitEdgeByEdgeCellId(BasePart part, int edgeCellId, int[][] edgeNeighbours, HashSet<int> verticeNodeIds)
        {
            int[][] edgeCells = part.Visualization.EdgeCells;
            if (edgeCells == null) return null;

            int nodeId;
            int[] edgeCell1;
            int[] edgeCell2;
            HashSet<int> newEdgeCellIds = new HashSet<int>();
            HashSet<int> edgeCellIds = new HashSet<int>();
            HashSet<int> notVisitedEdgeCellIds = new HashSet<int>();
            edgeCellIds.Add(edgeCellId);
            notVisitedEdgeCellIds.Add(edgeCellId);
            
            do
            {
                // find new edge candidates
                newEdgeCellIds.Clear();
                // search each not visited edge cell
                foreach (var notVisitedCellId in notVisitedEdgeCellIds)                     
                {
                    // continue if edge has at least one neighbour
                    if (edgeNeighbours[notVisitedCellId] != null)                           
                    {
                        edgeCell1 = edgeCells[notVisitedCellId];
                        // continue for each edge neighbour
                        foreach (var edgeNeighbourId in edgeNeighbours[notVisitedCellId])       
                        {
                            // is it a new edge cell
                            if (!edgeCellIds.Contains(edgeNeighbourId) && !newEdgeCellIds.Contains(edgeNeighbourId))    
                            {
                                edgeCell2 = edgeCells[edgeNeighbourId];

                                if (edgeCell1[0] == edgeCell2[0] || edgeCell1[0] == edgeCell2[1]) nodeId = edgeCell1[0];
                                else if (edgeCell1[1] == edgeCell2[0] || edgeCell1[1] == edgeCell2[1]) nodeId = edgeCell1[1];
                                else throw new NotSupportedException();

                                if (!verticeNodeIds.Contains(nodeId))   // stop the edge at the node that connects more than two edge cells
                                {
                                    newEdgeCellIds.Add(edgeNeighbourId);
                                }
                            }
                        }
                    }
                    else
                    {
                        int error = 1;
                    }
                }

                // add new edge candidates to edge and to cells to visit
                notVisitedEdgeCellIds.Clear();
                edgeCellIds.UnionWith(newEdgeCellIds);
                notVisitedEdgeCellIds.UnionWith(newEdgeCellIds);
            }
            while (notVisitedEdgeCellIds.Count > 0);

            return SortEdgeCellIds(part, edgeCellIds);
        }

        private int[] SortEdgeCellIds(BasePart part, HashSet<int> edgeCellIds)
        {
            int[][] edgeCells = part.Visualization.EdgeCells;

            int nodeId;
            List<int> ids;
            Dictionary<int, List<int>> nodeEdgeCellIds = new Dictionary<int, List<int>> ();
            foreach (int edgeCellId in edgeCellIds)
            {
                for (int i = 0; i < 2; i++)
                {
                    nodeId = edgeCells[edgeCellId][i];
                    if (nodeEdgeCellIds.TryGetValue(nodeId, out ids)) ids.Add(edgeCellId);
                    else nodeEdgeCellIds.Add(nodeId, new List<int>() { edgeCellId });
                }
            }
            
            int firstNodeId = -1;
            int lastNodeId = -1;
            foreach (var entry in nodeEdgeCellIds)
            {
                // find firts or last node id
                if (entry.Value.Count == 1) 
                {
                    if (firstNodeId == -1) firstNodeId = entry.Key;
                    else if (lastNodeId == -1)
                    {
                        // use frst id beforre overwriting it in the next line
                        lastNodeId = Math.Max(firstNodeId, entry.Key);     
                        firstNodeId = Math.Min(firstNodeId, entry.Key);
                    }
                    else throw new Exception("More than two nodes should not be external nodes!");
                }
            }

            // check for loop
            if (firstNodeId == -1)
            {
                firstNodeId = nodeEdgeCellIds.Keys.First();
                lastNodeId = firstNodeId;
            }

            int[] edgeCell;
            HashSet<int> visitedEdgeCellIds = new HashSet<int>();
            List<int> sortedEdgeCellIds = new List<int>();
            do
            {
                ids = nodeEdgeCellIds[firstNodeId];
                foreach (var cellId in ids)
                {
                    if (visitedEdgeCellIds.Add(cellId)) // if add possible 
                    {
                        edgeCell = edgeCells[cellId];
                        if (edgeCell[0] != firstNodeId) // swap
                        {
                            edgeCell[1] = edgeCell[0];
                            edgeCell[0] = firstNodeId;
                        }
                        sortedEdgeCellIds.Add(cellId);
                        firstNodeId = edgeCell[1];      // for the next edge cell the last node is the first

                        // if the first one is added break:
                        // by loops this prevents going in the opposite direction
                        break;                          
                    }
                }
            }

            while (firstNodeId != lastNodeId);

            return sortedEdgeCellIds.ToArray();
        }
        public void GetSplitVisualizationFaceByCellId(BasePart part, int cellId, Dictionary<int[], int> modelEdges, 
                                                      out int[] surfaceCellIds, out int[] surfaceEdgeIds)
        {
            surfaceCellIds = null;
            surfaceEdgeIds = null;

            int[][] visualizationCells = part.Visualization.Cells;
            int[][] allCellNeighbours = part.Visualization.CellNeighboursOverEdge;
            if (visualizationCells == null) return;

            // spread
            int edgeId;
            int[] edgeNodes;
            HashSet<int> surfaceEdgeIdsHash = new HashSet<int>();
            HashSet<int> cellNodes = new HashSet<int>();
            HashSet<int> newSurfaceCellIds = new HashSet<int>();
            HashSet<int> surfaceCellIdsHash = new HashSet<int>();
            HashSet<int> notVisitedCellIds = new HashSet<int>();

            surfaceCellIdsHash.Add(cellId);
            notVisitedCellIds.Add(cellId);
            
            do
            {
                // find new surface candidates
                newSurfaceCellIds.Clear();
                foreach (var notVisitedCellId in notVisitedCellIds)
                {
                    if (allCellNeighbours[notVisitedCellId] != null)
                    {
                        foreach (var neighbourId in allCellNeighbours[notVisitedCellId])
                        {
                            if (!surfaceCellIdsHash.Contains(neighbourId) && !newSurfaceCellIds.Contains(neighbourId))
                            {
                                cellNodes.Clear();
                                cellNodes.UnionWith(visualizationCells[notVisitedCellId]);
                                cellNodes.IntersectWith(visualizationCells[neighbourId]);
                                edgeNodes = cellNodes.ToArray();
                                Array.Sort(edgeNodes);

                                if (modelEdges.TryGetValue(edgeNodes, out edgeId)) surfaceEdgeIdsHash.Add(edgeId);
                                else newSurfaceCellIds.Add(neighbourId);
                            }
                        }
                    }
                    else
                    {
                        int error = 1;
                    }
                }

                // add new surface candidates to surface and to cells to visit
                notVisitedCellIds.Clear();
                surfaceCellIdsHash.UnionWith(newSurfaceCellIds);
                notVisitedCellIds.UnionWith(newSurfaceCellIds);
            }
            while (notVisitedCellIds.Count > 0);

            surfaceCellIds = surfaceCellIdsHash.ToArray();
            surfaceEdgeIds = surfaceEdgeIdsHash.ToArray();
        }

        public void RenumberVisualizationSurfaces(Dictionary<int, HashSet<int>> surfaceIdNodeIds)
        {
            int[] cellIdsByFace;
            VisualizationData vis;
            HashSet<int> faceNodeIds = new HashSet<int>();
            int[] newSurfaceIds;
            int[] oldSurfaceIds;
            int surfaceCount;

            // for each part
            foreach (var entry in _parts)
            {
                vis = entry.Value.Visualization;
                if (vis.CellIdsByFace == null) continue;    // for wire parts

                surfaceCount = 0;
                newSurfaceIds = new int[vis.CellIdsByFace.Length];
                oldSurfaceIds = new int[vis.CellIdsByFace.Length];

                // for each surface
                for (int i = 0; i < vis.CellIdsByFace.Length; i++)
                {
                    faceNodeIds.Clear();
                    cellIdsByFace = vis.CellIdsByFace[i];
                    for (int j = 0; j < cellIdsByFace.Length; j++) faceNodeIds.UnionWith(vis.Cells[cellIdsByFace[j]]);

                    // find the surface with the same node ids
                    foreach (var surfaceNodeIds in surfaceIdNodeIds)
                    {
                        if (surfaceNodeIds.Value.Count == faceNodeIds.Count &&
                            surfaceNodeIds.Value.Except(faceNodeIds).ToArray().Length == 0)
                        {
                            newSurfaceIds[surfaceCount] = surfaceNodeIds.Key;
                            oldSurfaceIds[surfaceCount] = surfaceCount;
                            surfaceCount++;
                        }
                    }
                }

                Array.Sort(newSurfaceIds, oldSurfaceIds);
                entry.Value.RenumberVisualizationSurfaces(oldSurfaceIds);
            }
        }

        public void MergeMeshParts(string[] partNamesToMerge, out MeshPart newMeshPart, out string[] mergedParts)
        {
            newMeshPart = null;
            mergedParts = null;
            if (partNamesToMerge == null || partNamesToMerge.Length < 2) return;

            // find parts to merge
            HashSet<int> allElementIds = new HashSet<int>();
            List<string> mergedPartsList = new List<string>();
            int minId = int.MaxValue;
            BasePart part;
            foreach (string partName in partNamesToMerge)
            {
                if (_parts.TryGetValue(partName, out part) && part is MeshPart meshPart)
                {
                    mergedPartsList.Add(partName);
                    allElementIds.UnionWith(meshPart.Labels);
                    if (meshPart.PartId < minId) minId = meshPart.PartId;
                }
            }
            if (mergedPartsList.Count == 1) return;

            mergedParts = mergedPartsList.ToArray();

            // remove parts
            foreach (var partName in mergedParts) _parts.Remove(partName);

            // create new part
            part = CreateBasePartFromElementIds(allElementIds.ToArray());

            newMeshPart = new MeshPart(part);
            newMeshPart.Name = NamedClass.GetNewValueName(_parts.Keys.ToArray(), "Merged_Part-");
            newMeshPart.PartId = minId;
            SetPartsColorFromId(newMeshPart);

            foreach (var elementId in newMeshPart.Labels)
            {
                _elements[elementId].PartId = minId;
            }

            // add new part
            _parts.Add(newMeshPart.Name, newMeshPart);

            // update bounding boxes
            ComputeBoundingBox();
        }

        public void CreateMeshPartsFromElementSets(string[] elementSetNames, out BasePart[] modifiedParts, out BasePart[] newParts)
        {
            // get parts from ids
            int maxPartId = -int.MaxValue;
            Dictionary<int, MeshPart> partIdNamePairs = new Dictionary<int, MeshPart>();
            foreach (var entry in _parts)
            {
                if (entry.Value is MeshPart mp)
                {
                    partIdNamePairs.Add(entry.Value.PartId, mp);
                    if (entry.Value.PartId > maxPartId) maxPartId = entry.Value.PartId;
                }
            }
            maxPartId++;

            // get element ids to remove from parts by partIds
            int partId;
            FeElementSet elementSet;
            FeElement element;
            List<string> newPartNames = new List<string>();
            Dictionary<int, List<int>> elementIdsToRemove = new Dictionary<int, List<int>>();
            List<int> elementIds;
            for (int i = 0; i < elementSetNames.Length; i++)
            {
                if (_elementSets.TryGetValue(elementSetNames[i], out elementSet))
                {
                    newPartNames.Add(elementSetNames[i]);

                    foreach (var elementId in elementSet.Labels)
                    {
                        element = _elements[elementId];
                        partId = element.PartId;

                        if (elementIdsToRemove.TryGetValue(partId, out elementIds)) elementIds.Add(elementId);
                        else elementIdsToRemove.Add(partId, new List<int>() { elementId });

                        element.PartId = maxPartId + i;
                    }
                }
            }           

            int count = 0;
            MeshPart meshPart;
            BasePart newBasePart;
            MeshPart newMeshPart;
            modifiedParts = new BasePart[elementIdsToRemove.Count];
            foreach (var entry in elementIdsToRemove)
            {
                meshPart = partIdNamePairs[entry.Key];
                meshPart.Labels = meshPart.Labels.Except(entry.Value).ToArray();

                newBasePart = CreateBasePartFromElementIds(meshPart.Labels);
                newMeshPart = new MeshPart(newBasePart);
                newMeshPart.Name = meshPart.Name;
                newMeshPart.PartId = meshPart.PartId;
                SetPartsColorFromId(newMeshPart);
                newMeshPart.CopyElementTypesFrom(meshPart);

                _parts[newMeshPart.Name] = newMeshPart; // replace part

                modifiedParts[count++] = newMeshPart;
            }


            // create new parts and remove element sets
            count = 0;
            newParts = new BasePart[newPartNames.Count];
            foreach (var newPartName in newPartNames)
            {
                newBasePart = CreateBasePartFromElementIds(_elementSets[newPartName].Labels);
                newMeshPart = new MeshPart(newBasePart);
                newMeshPart.Name = newPartName;
                newMeshPart.PartId = maxPartId + count;
                SetPartsColorFromId(newMeshPart);

                _parts.Add(newMeshPart.Name, newMeshPart);
                newParts[count++] = newMeshPart;
                _elementSets.Remove(newMeshPart.Name);
            }

            // update bounding boxes
            ComputeBoundingBox();
        }

        public BasePart CreateBasePartFromElementIds(int[] elementIds)
        {
            HashSet<Type> partElementTypes = new HashSet<Type>();
            HashSet<int> partNodeIds = new HashSet<int>();
            HashSet<int> partElementIds = new HashSet<int>();
            FeElement element;
            for (int i = 0; i < elementIds.Length; i++)
            {
                element = _elements[elementIds[i]];
                partElementIds.Add(element.Id);
                partElementTypes.Add(element.GetType());
                partNodeIds.UnionWith(element.NodeIds);
            }
            BasePart part = new BasePart("Part-from-element-Ids", -1, partNodeIds.ToArray(), 
                                         partElementIds.ToArray(), partElementTypes.ToArray());

            if (part.PartType == PartType.Solid) ExtractSolidPartVisualization(part);
            else if (part.PartType == PartType.Shell) ExtractShellPartVisualization(part);

            return part;
        }

        public BasePart GetPartContainingElementId(int elementId)
        {
            FeElement element = _elements[elementId];
            foreach (var entry in _parts)
            {
                if (entry.Value.PartId == element.PartId) return entry.Value;
            }
            return null;
        }

        public void ConvertLineFeElementsToEdges()
        {
            List<FeElement1D> edgeElements = new List<FeElement1D>();
            foreach (var entry in _elements)
            {
                if (entry.Value is FeElement1D element1D) edgeElements.Add(element1D);
            }

            bool add;
            int n1;
            int[] key;
            int[][] cells;
            int[][] cellEdges;
            List<int> cellIds;
            List<int[]> edgeCells = new List<int[]>();
            CompareIntArray comparer = new CompareIntArray();
            HashSet<int[]> sortedEdgeCells = new HashSet<int[]>(comparer);    // to be unique
            Dictionary<int, List<int>> nodeCellIds = new Dictionary<int, List<int>>();

            foreach (var entry in _parts)
            {
                if (entry.Value.PartType == PartType.Solid || entry.Value.PartType == PartType.Shell)
                {
                    // build a map of cellIds connected to each node
                    cells = entry.Value.Visualization.Cells;
                    nodeCellIds.Clear();
                    for (int i = 0; i < cells.Length; i++)
                    {
                        foreach (var nodeId in cells[i])
                        {
                            if (nodeCellIds.TryGetValue(nodeId, out cellIds)) cellIds.Add(i);
                            else nodeCellIds.Add(nodeId, new List<int>() { i });
                        }
                    }
                        
                    edgeCells.Clear();
                    sortedEdgeCells.Clear();

                    foreach (var element in edgeElements)
                    {
                        n1 = element.NodeIds[0];

                        if (nodeCellIds.TryGetValue(n1, out cellIds))           // get neighbouring cells
                        {
                            foreach (var cellId in cellIds)
                            {
                                cellEdges = GetVisualizationEdgeCells(cells[cellId]);  // get cell edges
                                foreach (var cellEdge in cellEdges)             // loop all cell edges
                                {
                                    add = true;
                                    foreach (var nodeId in element.NodeIds)     // check all element nodeIds
                                    {
                                        if (!cellEdge.Contains(nodeId))
                                        {
                                            add = false;
                                            break;
                                        }
                                    }
                                    if (add)
                                    {
                                        key = cellEdge.ToArray();
                                        Array.Sort(key);
                                        if (sortedEdgeCells.Add(key))
                                            edgeCells.Add(cellEdge);
                                    }
                                }
                            }
                        }
                    }

                    entry.Value.Visualization.EdgeCells = edgeCells.ToArray();

                    SplitVisualization(entry.Value);
                }
            }
        }

        #endregion #################################################################################################################

        #region Renumber  ##########################################################################################################
        public void RenumberNodes(int startId = 0)
        {
            Dictionary<int, int> newIds = new Dictionary<int, int>();
            Dictionary<int, FeNode> renNodes = new Dictionary<int, FeNode>();
            int oldId;
            int newId = startId;
            FeNode newNode;
            // renumber nodes and fill the lookup map oldIds
            foreach (FeNode node in _nodes.Values)
            {
                newNode = node;
                oldId = node.Id;

                newIds.Add(oldId, newId);
                newNode.Id = newId;
                renNodes.Add(newId, newNode);

                newId++;
            }
            _nodes = renNodes;
            _maxNodeId = newId - 1;


            // renumber element nodes
            FeElement newElement;
            foreach (FeElement element in _elements.Values)
            {
                newElement = element;
                for (int i = 0; i < newElement.NodeIds.Length; i++)
                {
                    newElement.NodeIds[i] = newIds[newElement.NodeIds[i]];
                }
            }

            // renumber node sets
            FeGroup newSet;
            foreach (FeGroup nodeSet in _nodeSets.Values)
            {
                newSet = nodeSet;
                for (int i = 0; i < newSet.Labels.Length; i++)
                {
                    newSet.Labels[i] = newIds[newSet.Labels[i]];
                }
            }

            // renumber 3D part's nodes, visualization cells and edges
            int[][] cells;
            BasePart part;
            foreach (var entry in _parts)
            {
                part = entry.Value;

                for (int i = 0; i < part.NodeLabels.Length; i++)
                {
                    part.NodeLabels[i] = newIds[part.NodeLabels[i]];
                }

                part.RenumberVisualizationNodes(newIds);

                //cells = part.Visualization.Cells;
                //if (cells != null)
                //{
                //    for (int i = 0; i < cells.Length; i++)
                //    {
                //        for (int j = 0; j < cells[i].Length; j++)
                //        {
                //            cells[i][j] = newIds[cells[i][j]];
                //        }
                //    }
                //}

                //// visualizationCellNeighbours are counted in local ids from 0 to cell.length
                ////cells = entry.Value.Visualization.CellNeighbours;

                //cells = part.Visualization.EdgeCells;
                //if (cells != null)
                //{
                //    for (int i = 0; i < cells.Length; i++)
                //    {
                //        for (int j = 0; j < cells[i].Length; j++)
                //        {
                //            cells[i][j] = newIds[cells[i][j]];
                //        }
                //    }
                //}
            }
        }
        public void RenumberElements(int startId = 0)
        {
            Dictionary<int, int> newIds = new Dictionary<int, int>();
            Dictionary<int, FeElement> renumberedElements = new Dictionary<int, FeElement>();
            int oldId;
            int newId = startId;
            FeElement newElement;
            // renumber elements and fill the lookup map oldIds
            foreach (var entry in _elements)
            {
                newElement = entry.Value;
                oldId = entry.Key;

                newIds.Add(oldId, newId);
                newElement.Id = newId;
                renumberedElements.Add(newId, newElement);

                newId++;
            }
            _elements = renumberedElements;
            _maxElementId = newId - 1;

            // renumber element sets
            FeGroup newSet;
            foreach (var entry in _elementSets)
            {
                newSet = entry.Value;
                for (int i = 0; i < newSet.Labels.Length; i++)
                {
                    newSet.Labels[i] = newIds[newSet.Labels[i]];
                }
            }
            
            BasePart part;
            foreach (var entry in _parts)
            {
                // renumber parts
                part = entry.Value;
                for (int i = 0; i < part.Labels.Length; i++)
                {
                    part.Labels[i] = newIds[part.Labels[i]];
                }

                // renumber 3D part's visualization cells
                part.RenumberVisualizationElements(newIds);                
            }
        }
        public void RenumberPart(string partName, int newPartId)
        {
            int oldPartId;
            BasePart part = _parts[partName];
            oldPartId = part.PartId;
            part.PartId = newPartId;
            SetPartsColorFromId(part);

            foreach (var entry in _elements)
            {
                if (entry.Value.PartId == oldPartId) entry.Value.PartId = newPartId;
            }
        }
        public void RenumberParts(int startId = 0)
        {
            Dictionary<int, int> newId = new Dictionary<int, int>();
            foreach (var part in _parts)
            {
                newId.Add(part.Value.PartId, startId);
                part.Value.PartId = startId;
                SetPartsColorFromId(part.Value);
                startId++;
            }

            foreach (var entry in _elements)
            {
                entry.Value.PartId = newId[entry.Value.PartId];
            }
        }
            
       
        #endregion #################################################################################################################

        // Surfaces 
        public void GetSurfaceGeometry(string name, out double[][] nodeCoor, out int[][] cells, out int[] cellTypes)
        {
            FeSurface surface = _surfaces[name];
            KeyValuePair<FeFaceName, string>[] elementSets = surface.ElementFaces.ToArray();

            List<int[]> cellList = new List<int[]>();
            FeElement element;

            foreach (var entry in elementSets)
            {
                foreach (int elementId in _elementSets[entry.Value].Labels)
                {
                    element = _elements[elementId];
                    cellList.Add(element.GetVtkCellFromFaceName(entry.Key));
                }
            }
            cells = cellList.ToArray();

            GetSurfaceGeometry(cells, out nodeCoor, out cellTypes);
        }
        public void GetSurfaceGeometry(int[][] cells, out double[][] nodeCoor, out int[] cellTypes)
        {
            // get the node ids of the used nodes
            HashSet<int> nodesNeeded = new HashSet<int>();
            for (int i = 0; i < cells.Length; i++) nodesNeeded.UnionWith(cells[i]);

            // create node array and a lookup table
            nodeCoor = new double[nodesNeeded.Count][];
            Dictionary<int, int> oldNew = new Dictionary<int, int>();
            int count = 0;
            foreach (int id in nodesNeeded)
            {
                nodeCoor[count] = _nodes[id].Coor;
                oldNew.Add(id, count);
                count++;
            }

            // renumber triangles and add cell type
            cellTypes = new int[cells.Length];
            for (int i = 0; i < cells.Length; i++)
            {
                for (int j = 0; j < cells[i].Length; j++)
                {
                    cells[i][j] = oldNew[cells[i][j]];
                }

                if (cells[i].Length == 3) cellTypes[i] = (int)vtkCellType.VTK_TRIANGLE;
                else if (cells[i].Length == 4) cellTypes[i] = (int)vtkCellType.VTK_QUAD;
                else if (cells[i].Length == 6) cellTypes[i] = (int)vtkCellType.VTK_QUADRATIC_TRIANGLE;
                else if (cells[i].Length == 8) cellTypes[i] = (int)vtkCellType.VTK_QUADRATIC_QUAD;
                else throw new NotSupportedException();
            }
        }
        public void GetSurfaceEdgesGeometry(string name, out double[][] nodeCoor, out int[][] cells, out int[] cellTypes)
        {
            FeSurface surface = _surfaces[name];
            KeyValuePair<FeFaceName, string>[] elementSets = surface.ElementFaces.ToArray();

            List<int[]> cellList = new List<int[]>();
            FeElement element;

            foreach (var entry in elementSets)
            {
                foreach (int elementId in _elementSets[entry.Value].Labels)
                {
                    element = _elements[elementId];
                    cellList.Add(element.GetVtkCellFromFaceName(entry.Key));
                }
            }

            // get edges
            cells = GetFreeEdgesFromVisualizationCells(cellList.ToArray());

            GetSurfaceEdgesGeometry(cells, out nodeCoor, out cellTypes);
        }
        public void GetSurfaceEdgesGeometry(int[][] cells, out double[][] nodeCoor, out int[] cellTypes)
        {
            // get the node ids of the used nodes
            HashSet<int> nodesNeeded = new HashSet<int>();
            for (int i = 0; i < cells.Length; i++) nodesNeeded.UnionWith(cells[i]);

            // create node array and a lookup table
            nodeCoor = new double[nodesNeeded.Count][];
            Dictionary<int, int> oldNew = new Dictionary<int, int>();
            int count = 0;
            foreach (int id in nodesNeeded)
            {
                nodeCoor[count] = _nodes[id].Coor;
                oldNew.Add(id, count);
                count++;
            }

            // renumber edge cell node ids
            cellTypes = new int[cells.Length];
            for (int i = 0; i < cells.Length; i++)
            {
                for (int j = 0; j < cells[i].Length; j++)
                {
                    cells[i][j] = oldNew[cells[i][j]];

                }
                if (cells[i].Length == 2) cellTypes[i] = (int)vtkCellType.VTK_LINE;
                else if (cells[i].Length == 3) cellTypes[i] = (int)vtkCellType.VTK_QUADRATIC_EDGE;
                else throw new NotSupportedException();
            }
        }

        public void CreateSurfaceItems(FeSurface surface)
        {
            surface.ClearElementFaces();     // area = 0;

            if (surface.Type == FeSurfaceType.Node) CreateSurfaceNodeSet(surface);
            else if (surface.Type == FeSurfaceType.Element) CreateSurfaceFaces(surface);
            else throw new CaeException("Surface type not supported.");
        }
        private void CreateSurfaceNodeSet(FeSurface surface)
        {
            double area;
            int[] nodeIds;
            Dictionary<FeFaceName, List<int>> elementSets;

            if (surface.CreatedFrom == FeSurfaceCreatedFrom.Selection)
            {
                CreateSurfaceFacesFromSelection(surface.FaceIds, out nodeIds, out elementSets, out area);

                // node set
                string nodeSetName = GetNextFreeInternalName(_nodeSets) + surface.Name;
                FeNodeSet nodeSet = new FeNodeSet(nodeSetName, nodeIds);
                nodeSet.Internal = true;
                UpdateNodeSetCenterOfGravity(nodeSet);
                _nodeSets.Add(nodeSetName, nodeSet);
                surface.NodeSetName = nodeSetName;
            }
            else if (surface.CreatedFrom == FeSurfaceCreatedFrom.NodeSet)
            {
                surface.NodeSetName = surface.CreatedFromNodeSetName;
            }
            else throw new NotSupportedException();

            surface.Area = 0;
        }
        private void CreateSurfaceFaces(FeSurface surface)
        {
            double area;
            int[] nodeIds;
            Dictionary<FeFaceName, List<int>> elementSets;

            surface.ClearElementFaces(); // area = 0 

            if (surface.CreatedFrom == FeSurfaceCreatedFrom.Selection)
            {
                CreateSurfaceFacesFromSelection(surface.FaceIds, out nodeIds, out elementSets, out area);
                surface.Area = area;
            }
            else if (surface.CreatedFrom == FeSurfaceCreatedFrom.NodeSet)
            {
                CreateSurfaceFacesFromNodeSet(surface, out nodeIds, out elementSets, out area);
                surface.Area = area;
            }
            else throw new NotSupportedException();

            if (elementSets.Count == 0)
            {
                surface.NodeSetName = null;
                surface.ClearElementFaces();
                return;
            }

            // node set
            string nodeSetName = GetNextFreeInternalName(_nodeSets) + surface.Name;
            FeNodeSet nodeSet = new FeNodeSet(nodeSetName, nodeIds);
            nodeSet.Internal = true;
            UpdateNodeSetCenterOfGravity(nodeSet);
            _nodeSets.Add(nodeSetName, nodeSet);
            surface.NodeSetName = nodeSetName;

            // element sets
            FeElementSet elementSet;
            string elementSetName;
            foreach (KeyValuePair<FeFaceName, List<int>> entry in elementSets)
            {
                elementSetName = GetNextFreeInternalName(_elementSets) + surface.Name + "_" + entry.Key;
                elementSet = new FeElementSet(elementSetName, entry.Value.ToArray());
                elementSet.Internal = true;
                _elementSets.Add(elementSetName, elementSet);
                surface.AddElementFace(entry.Key, elementSetName);
            }
        }        
        private void GetNodeAndElementIdsFromNodeSetSurface(FeSurface surface, out int[] nodeIds, out int[] elementIds)
        {
            //if (surface.CreatedFrom == FeSurfaceCreatedFrom.Selection)
            //{
            //    HashSet<int> hashElementIds = new HashSet<int>();
            //    HashSet<int> hashNodeIds = new HashSet<int>();

            //    BasePart part;
            //    int localvisualizationCellId;
            //    foreach (var faceId in surface.FaceIds)
            //    {
            //        GetLocalvisualizationCellId(faceId, out part, out localvisualizationCellId);
            //        if (part != null)
            //        {
            //            foreach (var nodeId in part.Visualization.Cells[localvisualizationCellId]) hashNodeIds.Add(nodeId);
            //            hashElementIds.Add(part.Visualization.CellIds[localvisualizationCellId]);
            //        }
            //    }

            //    nodeIds = hashNodeIds.ToArray();
            //    elementIds = hashElementIds.ToArray();
            //}
            //else
            if (surface.CreatedFrom == FeSurfaceCreatedFrom.NodeSet)
            {
                if (_nodeSets.ContainsKey(surface.CreatedFromNodeSetName))
                {
                    HashSet<int> allNodeSetIds = new HashSet<int>(_nodeSets[surface.CreatedFromNodeSetName].Labels);
                    HashSet<int> visualizationNodeIds = new HashSet<int>();
                    HashSet<int> hashElementIds = new HashSet<int>();

                    // for each node get all elements
                    int elementId;
                    List<int> listElementIds;
                    Dictionary<int, List<int>> nodeElementIds = new Dictionary<int, List<int>>();
                    foreach (var entry in _parts)
                    {
                        for (int i = 0; i < entry.Value.Visualization.Cells.Length; i++)
                        {
                            elementId = entry.Value.Visualization.CellIds[i];
                            foreach (var nodeId in entry.Value.Visualization.Cells[i])
                            {
                                if (allNodeSetIds.Contains(nodeId))
                                {
                                    visualizationNodeIds.Add(nodeId);
                                    hashElementIds.Add(elementId);

                                    if (nodeElementIds.TryGetValue(nodeId, out listElementIds)) listElementIds.Add(elementId);
                                    else nodeElementIds.Add(nodeId, new List<int> { elementId });
                                }
                            }
                        }
                    }
                    nodeIds = visualizationNodeIds.ToArray();
                    elementIds = hashElementIds.ToArray();
                }
                else // return empty sets
                {
                    nodeIds = new int[0];
                    elementIds = new int[0];
                }
            }
            else throw new CaeException("The surface is not created from node set.");
        }
        private void CreateSurfaceFacesFromSelection(int[] surfaceFaceIds, out int[] nodeIds, out Dictionary<FeFaceName, List<int>> elementSets, out double area)
        {
            nodeIds = null;
            elementSets = new Dictionary<FeFaceName, List<int>>();
            area = 0;

            List<int> elementIds;
            HashSet<int> faceNodeIds = new HashSet<int>();
            HashSet<int> hashElementIds = new HashSet<int>();
            HashSet<int> allNodeIds = new HashSet<int>();
            Dictionary<FeFaceName, double> faces;

            int[] cell;
            foreach (var faceId in surfaceFaceIds)
            {
                cell = GetCellFromFaceId(faceId, out FeElement element);

                faceNodeIds.Clear();
                faceNodeIds.UnionWith(cell);
                allNodeIds.UnionWith(faceNodeIds);

                faces = element.GetFaceNamesAndAreasFromNodeSet(faceNodeIds, _nodes);

                foreach (var entry in faces)
                {
                    area += entry.Value;

                    if (elementSets.TryGetValue(entry.Key, out elementIds)) elementIds.Add(element.Id);
                    else elementSets.Add(entry.Key, new List<int>() { element.Id });
                }
            }
            nodeIds = allNodeIds.ToArray();
        }
        private void CreateSurfaceFacesFromNodeSet(FeSurface surface, out int[] nodeIds, out Dictionary<FeFaceName, List<int>> elementSets, out double area)
        {
            int[] elementIds;
            elementSets = new Dictionary<FeFaceName, List<int>>();
            area = 0;

            GetNodeAndElementIdsFromNodeSetSurface(surface, out nodeIds, out elementIds);

            // to speed up the search
            HashSet<int> nodeSetLookUp = new HashSet<int>(nodeIds);

            Dictionary<FeFaceName, double> faces;
            foreach (int elementID in elementIds)
            {
                faces = _elements[elementID].GetFaceNamesAndAreasFromNodeSet(nodeSetLookUp, _nodes);
                foreach (var entry in faces)
                {
                    area += entry.Value;

                    if (elementSets.ContainsKey(entry.Key)) elementSets[entry.Key].Add(elementID);
                    else elementSets.Add(entry.Key, new List<int>() { elementID });
                }
            }
        }
        public void UpdateSurfaceArea(FeSurface surface)
        {
            if (surface.Type == FeSurfaceType.Node)
            {
                surface.Area = 0;
            }
            else if (surface.Type == FeSurfaceType.Element)
            {
                double area;
                int[] nodeIds;
                Dictionary<FeFaceName, List<int>> elementSets;

                if (surface.CreatedFrom == FeSurfaceCreatedFrom.Selection)
                {
                    CreateSurfaceFacesFromSelection(surface.FaceIds, out nodeIds, out elementSets, out area);
                }
                else if (surface.CreatedFrom == FeSurfaceCreatedFrom.NodeSet)
                {
                    CreateSurfaceFacesFromNodeSet(surface, out nodeIds, out elementSets, out area);
                }
                else throw new CaeException("The surface created from faces is not supported.");

                surface.Area = area;
            }
            else throw new CaeException("The surface type is not supported.");
        }
        public int[] GetCellFromFaceId(int faceId, out FeElement element)
        {
            element = null;
            int elementId = faceId / 10;
            int vtkCellId = faceId % 10;

            if (_elements.TryGetValue(elementId, out element))
            {
                element = _elements[elementId];
                if (element is FeElement3D element3D)
                {
                    int[][] vtkCells = element3D.GetAllVtkCells();
                    return vtkCells[vtkCellId];
                }
                else if (element is LinearTriangleElement ltElement)    // geometry
                {
                    return ltElement.GetVtkNodeIds();
                }
                else throw new NotSupportedException();
            }
            else throw new CaeGlobals.CaeException("The selected face id does not exist.");
        }
       
        private string GetNextFreeInternalName<T>(Dictionary<string, T> dictionary)
        {
            int n = 0;
            bool contains = true;
            while (contains)
            {
                n++;
                contains = false;
                foreach (var entry in dictionary)
                {
                    if (entry.Key.StartsWith("internal-" + n + "_"))
                    {
                        contains = true;
                        break;
                    }
                }
            }
            return "internal-" + n + "_";
        }

        #region Extraction  ########################################################################################################
        public int[] GetPartNodeIds(int elementId)
        {
            // Find the part
            BasePart part = null;
            int elementPartId = _elements[elementId].PartId;

            foreach (var entry in _parts)
            {
                if (entry.Value.PartId == elementPartId)
                {
                    part = entry.Value;
                    break;
                }
            }

            return part.NodeLabels;
        }
        public int[] GetPartElementIds(int elementId)
        {
            // Find the part
            BasePart part = null;
            int elementPartId = _elements[elementId].PartId;

            foreach (var entry in _parts)
            {
                if (entry.Value.PartId == elementPartId)
                {
                    part = entry.Value;
                    break;
                }
            }

            return part.Labels.ToArray();
        }
        public int[][] GetEdgeCells(int elementId, int[] edgeNodeIds)
        {
            BasePart part;
            int partEdgeId;
            return GetEdgeCells(elementId, edgeNodeIds, out part, out partEdgeId);
        }
        public int[][] GetEdgeCells(int elementId, int[] edgeNodeIds, out BasePart part, out int edgeId)
        {
            // get all faces containing at least 1 node id
            int[] faceIds = GetVisualizationFaceIds(edgeNodeIds, new int[] { elementId }, false, false);

            bool add;
            int[] cell = null;
            FeElement element = null;
            HashSet<int> hashCell;

            // find a face containing all node ids
            foreach (int faceId in faceIds)
            {
                cell = GetCellFromFaceId(faceId, out element);
                if (cell.Length < edgeNodeIds.Length) continue;

                hashCell = new HashSet<int>(cell);
                add = true;
                for (int i = 0; i < edgeNodeIds.Length; i++)
                {
                    if (!hashCell.Contains(edgeNodeIds[i]))
                    {
                        add = false;
                        break;
                    }
                }
                if (add) break;
            }

            // find face edge cells that are on "surface" edges
            CompareIntArray comparer = new CompareIntArray();
            int edgeCellId;
            int[] edgeCell;
            Dictionary<int[], int> edgeCellEdgeId = new Dictionary<int[], int>(comparer);

            edgeId = -1;
            part = GetPartContainingElementId(elementId);
            if (part == null) return null;
            VisualizationData visualization = part.Visualization;

            HashSet<int> intersection = new HashSet<int>();
            for (int i = 0; i < visualization.EdgeCellIdsByEdge.Length; i++)
            {
                for (int j = 0; j < visualization.EdgeCellIdsByEdge[i].Length; j++)
                {
                    edgeCellId = visualization.EdgeCellIdsByEdge[i][j];
                    edgeCell = visualization.EdgeCells[edgeCellId].ToArray();

                    intersection.Clear();
                    intersection.UnionWith(edgeCell);
                    intersection.IntersectWith(cell);

                    if (intersection.Count > 0) edgeCellEdgeId.Add(edgeCell, i);
                }
            }

            // if the face is connected to more than one "surface" edge, find the one with the most equal nodes
            if (edgeCellEdgeId.Count == 1) edgeId = edgeCellEdgeId.Values.First();
            else if (edgeCellEdgeId.Count > 1)
            {
                int maxEqualNodes = 0;
                foreach (var entry in edgeCellEdgeId)
                {
                    intersection.Clear();
                    intersection.UnionWith(edgeNodeIds);
                    intersection.IntersectWith(entry.Key);

                    if (intersection.Count > maxEqualNodes)
                    {
                        maxEqualNodes = intersection.Count;
                        edgeId = entry.Value;
                    }
                    else if (intersection.Count == maxEqualNodes && intersection.Count == 1 &&
                             intersection.First() == edgeNodeIds[0])    // first edgeNode is the closest
                    {
                        maxEqualNodes = intersection.Count;
                        edgeId = entry.Value;
                    }
                }
            }

            if (edgeId != -1)
            {
                int[][] edgeCells = new int[visualization.EdgeCellIdsByEdge[edgeId].Length][];
                for (int i = 0; i < edgeCells.Length; i++)
                {
                    edgeCells[i] = visualization.EdgeCells[visualization.EdgeCellIdsByEdge[edgeId][i]];
                }
                return edgeCells;
            }
            else return null;
        }
        public int[] GetEdgeNodeIds(int elementId, int[] edgeNodeIds)
        {
            int[][] edgeCells = GetEdgeCells(elementId, edgeNodeIds);
            if (edgeCells != null)
            {
                HashSet<int> nodeIds = new HashSet<int>();
                foreach (var edgeCell in edgeCells)
                {
                    nodeIds.UnionWith(edgeCell);
                }
                return nodeIds.ToArray();
            }
            else return null;
        }
        public bool GetFaceId(int elementId, int[] cellFaceGlobalNodeIds, out BasePart part, out int faceId)
        {
            // Find the part
            part = null;
            faceId = -1;
            int elementPartId = _elements[elementId].PartId;

            foreach (var entry in _parts)
            {
                if (entry.Value.PartId == elementPartId)
                {
                    part = entry.Value;
                    break;
                }
            }

            // Find the picked surface cell
            int[][] cells = part.Visualization.Cells;
            int[] cellIds = part.Visualization.CellIds;
            if (cells == null) return false;

            int faceCellId = -1;
            int numberOfSameNodes = cellFaceGlobalNodeIds.Length;
            HashSet<int> faceNodeIds = new HashSet<int>(cellFaceGlobalNodeIds);
            int count;
            for (int i = 0; i < cells.Length; i++)
            {
                if (part.Visualization.CellIds[i] == elementId)     // is this one of element cells
                {
                    count = 0;
                    for (int j = 0; j < cells[i].Length; j++)
                    {
                        if (faceNodeIds.Contains(cells[i][j]))
                        {
                            count++;
                            if (count == numberOfSameNodes)
                            {
                                faceCellId = i;
                                break;
                            }
                        }
                    }
                }
                if (faceCellId != -1) break;
            }
            if (faceCellId == -1) return false;

            for (int i = 0; i < part.Visualization.CellIdsByFace.Length; i++)
            {
                if (part.Visualization.CellIdsByFace[i].Contains(faceCellId))
                {
                    faceId = i;
                    break;
                }
            }

            return true;
        }
        public int[] GetSurfaceNodeIds(int elementId, int[] cellFaceGlobalNodeIds)
        {
            BasePart part;
            int faceId;
            if (GetFaceId(elementId, cellFaceGlobalNodeIds, out part, out faceId))
            {
                int[][] cells = part.Visualization.Cells;
                HashSet<int> surfaceNodes = new HashSet<int>();
                if (faceId != -1)
                {
                    foreach (var surfaceCellId in part.Visualization.CellIdsByFace[faceId])
                    {
                        surfaceNodes.UnionWith(cells[surfaceCellId]);
                    }
                }
                return surfaceNodes.ToArray();
            }
            else return null;
        }
        public int[] GetEdgeByAngleNodeIds(int elementId, int[] edgeGlobalNodeIds, double angle)
        {
            // Find the part
            BasePart part = null;
            int elementPartId = _elements[elementId].PartId;

            foreach (var entry in _parts)
            {
                if (entry.Value.PartId == elementPartId)
                {
                    part = entry.Value;
                    break;
                }
            }

            // Build node neighbours map
            int[][] cells = part.Visualization.Cells;
            CompareIntArray comparer = new CompareIntArray();
            Dictionary<int[], int[]> allEdges = new Dictionary<int[], int[]>(comparer);

            Dictionary<int, HashSet<int>> nodeNeighbours = new Dictionary<int, HashSet<int>>();
            HashSet<int> neighbours;

            int n1Id, n2Id;
            int[] lookUp;
            int[] lookUp3 = new int[] { 0, 1, 2, 0 };
            int[] lookUp4 = new int[] { 0, 1, 2, 3, 0 };
            int[] lookUp6 = new int[] { 0, 3, 1, 4, 2, 5, 0 };
            int[] lookUp8 = new int[] { 0, 4, 1, 5, 2, 6, 3, 7, 0 };

            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i].Length == 3) lookUp = lookUp3;
                else if (cells[i].Length == 4) lookUp = lookUp4;
                else if (cells[i].Length == 6) lookUp = lookUp6;
                else if (cells[i].Length == 8) lookUp = lookUp8;
                else throw new NotSupportedException();

                for (int j = 0; j < cells[i].Length; j++)
                {
                    n1Id = cells[i][lookUp[j]];
                    n2Id = cells[i][lookUp[j + 1]];

                    if (nodeNeighbours.TryGetValue(n1Id, out neighbours)) neighbours.Add(n2Id);
                    else nodeNeighbours.Add(n1Id, new HashSet<int>() { n2Id });

                    if (nodeNeighbours.TryGetValue(n2Id, out neighbours)) neighbours.Add(n1Id);
                    else nodeNeighbours.Add(n2Id, new HashSet<int>() { n1Id });
                }
            }

            // spread
            n1Id = edgeGlobalNodeIds[0];
            n2Id = edgeGlobalNodeIds[1];
            int n3Id = -1;
            HashSet<int> allNodeIds = new HashSet<int>(edgeGlobalNodeIds);
            angle *= Math.PI / 180;
            // go forward
            while (true)
            {
                n3Id = GetNextEdgeNodeId(n1Id, n2Id, nodeNeighbours[n2Id], angle);
                if (n3Id >= 0 && !allNodeIds.Contains(n3Id))
                {
                    allNodeIds.Add(n3Id);
                    n1Id = n2Id;
                    n2Id = n3Id;
                }
                else break;
            }
            // go backward
            n1Id = edgeGlobalNodeIds[1];
            n2Id = edgeGlobalNodeIds[0];
            while (true)
            {
                n3Id = GetNextEdgeNodeId(n1Id, n2Id, nodeNeighbours[n2Id], angle);
                if (n3Id >= 0 && !allNodeIds.Contains(n3Id))
                {
                    allNodeIds.Add(n3Id);
                    n1Id = n2Id;
                    n2Id = n3Id;
                }
                else break;
            }

            return allNodeIds.ToArray();
        }
        public int[] GetSurfaceByAngleNodeIds(int elementId, int[] cellFaceGlobalNodeIds, double angle)
        {
            // Find the part
            BasePart part = null;
            int elementPartId = _elements[elementId].PartId;

            foreach (var entry in _parts)
            {
                if (entry.Value.PartId == elementPartId)
                {
                    part = entry.Value;
                    break;
                }
            }

            // Find the picked surface cell
            int[][] cells = part.Visualization.Cells;
            int[] cellIds = part.Visualization.CellIds;
            if (cells == null) return null;

            int surfaceCellId = -1;
            int numberOfSameNodes = cellFaceGlobalNodeIds.Length;
            HashSet<int> faceNodeIds = new HashSet<int>(cellFaceGlobalNodeIds);
            int count;
            for (int i = 0; i < cells.Length; i++)
            {
                if (part.Visualization.CellIds[i] == elementId)     // is this one of element cells
                {
                    count = 0;
                    for (int j = 0; j < cells[i].Length; j++)
                    {
                        if (faceNodeIds.Contains(cells[i][j]))
                        {
                            count++;
                            if (count == numberOfSameNodes)
                            {
                                surfaceCellId = i;
                                break;
                            }
                        }
                    }
                }
                if (surfaceCellId != -1) break;
            }
            if (surfaceCellId == -1) return null;

            // spread
            int[][] allCellNeighbours = part.Visualization.CellNeighboursOverEdge;
            HashSet<int> surfaceCellIds = new HashSet<int>();
            HashSet<int> notVisitedCellIds = new HashSet<int>();
            surfaceCellIds.Add(surfaceCellId);
            notVisitedCellIds.Add(surfaceCellId);

            double alpha;
            angle *= Math.PI / 180;
            HashSet<int> newSurfaceCellIds = new HashSet<int>();
            do
            {
                // find new surface candidates
                newSurfaceCellIds.Clear();
                foreach (var notVisitedCellId in notVisitedCellIds)
                {
                    foreach (var neighbourId in allCellNeighbours[notVisitedCellId])
                    {
                        if (!surfaceCellIds.Contains(neighbourId) && !newSurfaceCellIds.Contains(neighbourId))
                        {
                            alpha = ComputeAngleInRadFromCellIndices(cells[notVisitedCellId], cells[neighbourId]);
                            if (alpha <= angle)
                            {
                                newSurfaceCellIds.Add(neighbourId);
                            }
                        }
                    }
                }

                // add new surface candidates to surface and to cells to visit
                notVisitedCellIds.Clear();
                foreach (var newSurfaceCellId in newSurfaceCellIds)
                {
                    surfaceCellIds.Add(newSurfaceCellId);
                    notVisitedCellIds.Add(newSurfaceCellId);
                }
            }
            while (newSurfaceCellIds.Count > 0);

            HashSet<int> surfaceNodes = new HashSet<int>();
            foreach (var cellId in surfaceCellIds) surfaceNodes.UnionWith(cells[cellId]);

            return surfaceNodes.ToArray();
        }
        public int[] GetElementIdsFromNodeIds(int[] nodeIds, bool containsEdge, bool containsFace, bool containsElement)
        {
            if (nodeIds == null) return null;

            HashSet<int> allNodeIds = new HashSet<int>(nodeIds);
            HashSet<int> allElementIds = new HashSet<int>();

            bool parabolic;
            int minNumberOfNodesToContain = 1;
            int countNodes;
            FeElement element;
            int vtkType;

            foreach (var entry in _elements)
            {
                element = entry.Value;
                vtkType = element.GetVtkCellType();

                parabolic = FeElement.IsParabolic(element);
                if (containsEdge)
                {
                    minNumberOfNodesToContain = 2;
                }
                else if (containsFace)
                {
                    if (parabolic) minNumberOfNodesToContain = 4;
                    else minNumberOfNodesToContain = 3;
                }
                else if (containsElement) minNumberOfNodesToContain = element.NodeIds.Length;
                else minNumberOfNodesToContain = 1;

                countNodes = 0;
                for (int i = 0; i < element.NodeIds.Length; i++)
                {
                    if (allNodeIds.Contains(element.NodeIds[i]))
                    {
                        countNodes++;
                    }
                    if (countNodes >= minNumberOfNodesToContain) break;
                }

                if (countNodes >= minNumberOfNodesToContain)
                    allElementIds.Add(entry.Key);
            }
            // return a copy
            return allElementIds.ToArray();
        }
        public int[] GetVisualizationFaceIds(int[] nodeIds, int[] elementIds, bool containsEdge, bool containsFace)
        {
            HashSet<int> hashElementIds = new HashSet<int>(elementIds);
            HashSet<int> hashNodeIds = new HashSet<int>();
            HashSet<int> globalVisualizationFaceIds = new HashSet<int>();

            // get all visualization cell ids
            int elementId;
            int vtkCellId;
            int count;
            int[] cell;
            int minNumberOfNodes = 1;
            FeElement element;
            CompareIntArray comparer = new CompareIntArray();

            if (nodeIds != null && nodeIds.Length > 0) hashNodeIds.UnionWith(nodeIds);

            foreach (var entry in _parts)
            {
                for (int i = 0; i < entry.Value.Visualization.CellIds.Length; i++)
                {
                    elementId = entry.Value.Visualization.CellIds[i];
                    if (hashElementIds.Contains(elementId))
                    {
                        count = 0;
                        cell = entry.Value.Visualization.Cells[i];   // these are surface cells
                        foreach (int nodeId in cell)
                        {
                            if (containsEdge)
                            {
                                if (cell.Length <= 4) minNumberOfNodes = 2;     // linear
                                else minNumberOfNodes = 3;                      // parabolic
                            }
                            else if (containsFace)
                            {
                                if (cell.Length <= 4) minNumberOfNodes = 3;     // linear
                                else minNumberOfNodes = 4;                      // parabolic
                            }
                            else
                            {
                                if (hashNodeIds.Count > 0) minNumberOfNodes = 1;
                                else minNumberOfNodes = -1;
                            }

                            if (hashNodeIds.Contains(nodeId))
                            {
                                count++;
                                if (count >= minNumberOfNodes) break;
                            }
                        }
                        if (count >= minNumberOfNodes)
                        {
                            element = _elements[elementId];
                            if (element is FeElement3D element3D)
                            {
                                vtkCellId = element3D.GetVtkCellIdFromCell(cell);
                                if (vtkCellId != -1) globalVisualizationFaceIds.Add(10 * elementId + vtkCellId);
                                else throw new Exception();
                            }
                            else if (element is LinearTriangleElement ltElement) // geometry
                            {
                                globalVisualizationFaceIds.Add(10 * elementId + 0);
                            }
                            else throw new NotSupportedException();
                        }
                    }
                }
            }
            return globalVisualizationFaceIds.ToArray();
        }
        public int[] GetVisibleVisualizationFaceIds()
        {
            // get all visualization cell ids
            int elementId;
            int vtkCellId;
            int[] cell;
            FeElement element;
            HashSet<int> visualizationFaceIds = new HashSet<int>();

            foreach (var entry in _parts)
            {
                if (entry.Value.Visible)
                {
                    for (int i = 0; i < entry.Value.Visualization.CellIds.Length; i++)
                    {
                        cell = entry.Value.Visualization.Cells[i];   // these are surface cells
                        elementId = entry.Value.Visualization.CellIds[i];
                        element = _elements[elementId];
                        if (element is FeElement3D element3D)
                        {
                            vtkCellId = element3D.GetVtkCellIdFromCell(cell);
                            if (vtkCellId != -1) visualizationFaceIds.Add(10 * elementId + vtkCellId);
                            else throw new Exception();
                        }
                        else throw new NotSupportedException();
                    }
                }
            }
            return visualizationFaceIds.ToArray();
        }
        
        public int GetGeometryId(double[] point, double dist, int elementId, int[] edgeNodeIds, int[] cellFaceNodeIds)
        {
            int itemId = -1;
            int typeId = -1;        // 1 ... vertex, 2 ... edge, 3 ... surface
            int partId = -1; 
            int geometryId;

            BasePart part;
            int partEdgeId;

            int[][] edgeCells = GetEdgeCells(elementId, edgeNodeIds, out part, out partEdgeId);
            partId = part.PartId;

            if (edgeCells != null)
            {
                double dSeg;
                double dMin = double.MaxValue;
                foreach (var cell in edgeCells)
                {
                    dSeg = Geometry.PointToSegmentDistance(point, _nodes[cell[0]].Coor, _nodes[cell[1]].Coor);
                    if (dSeg < dMin) dMin = dSeg;
                }

                if (dMin <= dist)
                {
                    Vec3D n1 = new Vec3D(_nodes[edgeCells[0][0]].Coor);
                    Vec3D n2 = new Vec3D(_nodes[edgeCells.Last()[1]].Coor);
                    Vec3D p = new Vec3D(point);

                    double d1 = (n1 - p).Len;
                    double d2 = (n2 - p).Len;

                    if (d1 < dist || d2 < dist) 
                    {
                        // Vertex
                        int nodeId;
                        if (d1 < dist) nodeId = edgeCells[0][0];
                        else nodeId = edgeCells.Last()[1];

                        for (int i = 0; i < part.Visualization.VertexNodeIds.Length; i++)
                        {
                            if (part.Visualization.VertexNodeIds[i] == nodeId)
                            {
                                itemId = i;
                                typeId = 1;
                                break;
                            }
                        }
                    }
                    else // Edge
                    {
                        itemId = partEdgeId;
                        typeId = 2;
                    }
                }
            }

            if (itemId == -1)
            {
                // surface
                int faceId;
                if (GetFaceId(elementId, cellFaceNodeIds, out part, out faceId))
                {
                    itemId = faceId;
                    typeId = 3;
                }
            }

            geometryId = itemId * 100000 + typeId * 10000 + partId;
            return geometryId;
        }
        public int[] GetNodeIdsFromGeometryId(int geometryId)
        {
            int partId = geometryId % 10000;
            int typeId = (geometryId / 10000) % 10;
            int itemId = geometryId / 100000;

            // Find pyrt by id
            BasePart part = null;
            foreach (var entry in _parts)
            {
                if (entry.Value.PartId == partId)
                {
                    part = entry.Value;
                    break;
                }
            }

            if (part == null) return null;
            VisualizationData vis = part.Visualization;
            HashSet<int> nodeIds = new HashSet<int>();

            if (typeId == 1)
            {
                nodeIds.Add(vis.VertexNodeIds[itemId]);
            }
            else if (typeId == 2)
            {
                foreach (var edgeCellId in vis.EdgeCellIdsByEdge[itemId])
                {
                    nodeIds.UnionWith(vis.EdgeCells[edgeCellId]);
                }
            }
            else if (typeId == 3)
            {
                foreach (var cellId in vis.CellIdsByFace[itemId])
                {
                    nodeIds.UnionWith(vis.Cells[cellId]);
                }
            }
            else throw new NotSupportedException();

            return nodeIds.ToArray();
        }

        private int GetNextEdgeNodeId(int n1Id, int n2Id, HashSet<int> n2Neighbours, double angle)
        {
            double minAngle = double.MaxValue;
            int minNodeId = -1;
            double alpha;

            foreach (int nId3 in n2Neighbours)
            {
                if (nId3 != n1Id)   // skip the first node
                {
                    alpha = GetEdgeAngle(n1Id, n2Id, nId3);
                    if (alpha <= angle && alpha < minAngle)
                    {
                        minAngle = alpha;
                        minNodeId = nId3;
                    }
                }
            }

            return minNodeId;
        }
        private double GetEdgeAngle(int n1Id, int n2Id, int n3Id)
        {
            double[] n1 = _nodes[n1Id].Coor;
            double[] n2 = _nodes[n2Id].Coor;
            double[] n3 = _nodes[n3Id].Coor;

            double[] a = new double[] { n2[0] - n1[0], n2[1] - n1[1], n2[2] - n1[2] };
            double[] b = new double[] { n3[0] - n2[0], n3[1] - n2[1], n3[2] - n2[2] };

            double d = Math.Sqrt(Math.Pow(a[0], 2) + Math.Pow(a[1], 2) + Math.Pow(a[2], 2));
            a[0] /= d;
            a[1] /= d;
            a[2] /= d;

            d = Math.Sqrt(Math.Pow(b[0], 2) + Math.Pow(b[1], 2) + Math.Pow(b[2], 2));
            b[0] /= d;
            b[1] /= d;
            b[2] /= d;

            double cosAngle = Math.Min(1, a[0] * b[0] + a[1] * b[1] + a[2] * b[2]);

            return Math.Acos(cosAngle);
        }
       

        #endregion #################################################################################################################

        #region Add entities #######################################################################################################
        public FeNode AddNodeByCoor(double x, double y, double z)
        {
            _maxNodeId++;
            FeNode node = new FeNode(_maxNodeId, x, y, z);
            _nodes.Add(node.Id, node);
            _boundingBox.CheckNode(node);
            return node;
        }
        public void AddNodeSet(FeNodeSet nodeSet)
        {
            FeNodeSet existingNodeSet;

            // sort labels
            Array.Sort(nodeSet.Labels);

            if (_nodeSets.TryGetValue(nodeSet.Name, out existingNodeSet))        // in Calculix the sets with the same name are merged
            {
                existingNodeSet.Labels = existingNodeSet.Labels.Concat(nodeSet.Labels).Distinct().ToArray();
            }
            else
            {
                List<int> nodeIds = new List<int>();

                // add only node ids of existing elements
                foreach (int nodeId in nodeSet.Labels)
                {
                    if (_nodes.ContainsKey(nodeId)) nodeIds.Add(nodeId);
                }

                if (nodeIds.Count > 0)
                {
                    FeNodeSet newNodeSet = new FeNodeSet(nodeSet.Name, nodeIds.ToArray());
                    UpdateNodeSetCenterOfGravity(newNodeSet);
                    if (nodeSet.Labels.Length != newNodeSet.Labels.Length) newNodeSet.Valid = false;
                    _nodeSets.Add(newNodeSet.Name, newNodeSet);
                }
            }
        }
        public void AddNodeSetFromElementSet(string elementSetName)
        {
            FeNodeSet nodeSet = GetNodeSetFromPartOrElementSet(elementSetName);
            _nodeSets.Add(nodeSet.Name, nodeSet);
        }
        public FeNodeSet GetNodeSetFromPartOrElementSet(string regionName)
        {
            FeGroup group;
            if (_elementSets.ContainsKey(regionName)) group = _elementSets[regionName];
            else if (_parts.ContainsKey(regionName)) group = _parts[regionName];
            else throw new CaeException("The element set name or part name does not exist.");

            // create a node set from the element set
            HashSet<int> nodeIds = new HashSet<int>();
            FeElement element;
            for (int i = 0; i < group.Labels.Length; i++)
            {
                element = _elements[group.Labels[i]];
                for (int j = 0; j < element.NodeIds.Length; j++) nodeIds.Add(element.NodeIds[j]);
            }

            string nodeSetName = regionName + "_el";
            FeNodeSet nodeSet = new FeNodeSet(nodeSetName, nodeIds.ToArray());
            UpdateNodeSetCenterOfGravity(nodeSet);
            return nodeSet;
        }
        public void AddElementSet(FeElementSet elementSet)
        {
            FeElementSet existingElementSet;
            BasePart part;

            // sort labels
            Array.Sort(elementSet.Labels);

            if (_elementSets.TryGetValue(elementSet.Name, out existingElementSet))     // in Calculix the sets with the same name are merged
            {
                existingElementSet.Labels = existingElementSet.Labels.Concat(elementSet.Labels).Distinct().ToArray();
                return;
            }
            else if (_parts.TryGetValue(elementSet.Name, out part))                    // does a part exists
            {
                CompareIntArray comparer = new CompareIntArray();
                if (comparer.Equals(part.Labels, elementSet.Labels)) return;           // skip element sets with the same name and ids as parts
                else
                { 
                    // rename part;
                    HashSet<string> allNames = new HashSet<string>(_elementSets.Keys);
                    allNames.UnionWith(_parts.Keys);
                    _parts.Remove(part.Name);
                    part.Name = NamedClass.GetNewValueName(allNames.ToArray(), part.Name.Split('-')[0] + "-");
                    _parts.Add(part.Name, part);
                }
            }

            List<int> elementIds = new List<int>();

            // add only element ids of existing elements
            foreach (int elementId in elementSet.Labels)
            {
                if (_elements.ContainsKey(elementId)) elementIds.Add(elementId);
            }

            if (elementIds.Count > 0)
            {
                FeElementSet newElementSet = new FeElementSet(elementSet.Name, elementIds.ToArray());
                if (elementSet.Labels.Length != newElementSet.Labels.Length) newElementSet.Valid = false;
                _elementSets.Add(newElementSet.Name, newElementSet);
            }
        }
        public void AddSurface(FeSurface surface)
        {
            if (surface.CreatedFrom == FeSurfaceCreatedFrom.Faces)
            {
                AddSurfaceFromFaces(ref surface);
            }
            else if (surface.CreatedFrom == FeSurfaceCreatedFrom.NodeSet)
            {
                AddSurfaceFromNodeSet(surface);
            }
            else throw new CaeException("Function 'Add surface' only usable for surfaces created from faces.");

            _surfaces.Add(surface.Name, surface);
        }
        private void AddSurfaceFromFaces(ref FeSurface surface)
        {
            FeElementSet elementSet;
            List<FeFaceName> tmpFaces = new List<FeFaceName>();
            Dictionary<int, List<FeFaceName>> allElementIdsFaces = new Dictionary<int, List<FeFaceName>>();

            foreach (var entry in surface.ElementFaces)
            {
                if (_elementSets.TryGetValue(entry.Value, out elementSet))
                {
                    foreach (int elementId in elementSet.Labels)
                    {
                        if (allElementIdsFaces.TryGetValue(elementId, out tmpFaces)) tmpFaces.Add(entry.Key);
                        else allElementIdsFaces.Add(elementId, new List<FeFaceName>() { entry.Key });
                    }
                    elementSet.Internal = true;     // hide element set
                }
                else surface.Valid = false;
            }

            FeElement element;
            List<int> faceIds = new List<int>();
            HashSet<int> allNodeIds = new HashSet<int>();
            CompareIntArray comparer = new CompareIntArray();

            int vtkCellId;
            int[] cell;
            foreach (var entry in allElementIdsFaces)
            {
                element = _elements[entry.Key];
                if (element is FeElement3D element3D)
                {
                    foreach (var faceName in entry.Value)
                    {
                        cell = element3D.GetVtkCellFromFaceName(faceName);
                        vtkCellId = element3D.GetVtkCellIdFromCell(cell);
                        if (vtkCellId != -1)
                        {
                            allNodeIds.UnionWith(cell);
                            faceIds.Add(10 * element.Id + vtkCellId);
                        }
                    }
                }
            }

            FeSurface surfaceFromFaceIds = new FeSurface(surface.Name, faceIds.ToArray(), null);
            foreach (var entry in surface.ElementFaces) surfaceFromFaceIds.AddElementFace(entry.Key, entry.Value);

            // node set
            string nodeSetName = GetNextFreeInternalName(_nodeSets) + surfaceFromFaceIds.Name;
            FeNodeSet nodeSet = new FeNodeSet(nodeSetName, allNodeIds.ToArray());
            nodeSet.Internal = true;
            UpdateNodeSetCenterOfGravity(nodeSet);
            _nodeSets.Add(nodeSetName, nodeSet);
            surfaceFromFaceIds.NodeSetName = nodeSetName;

            UpdateSurfaceArea(surfaceFromFaceIds);

            surface = surfaceFromFaceIds;
        }
        private void AddSurfaceFromNodeSet(FeSurface surface)
        {
            CreateSurfaceItems(surface);
        }
        public void AddReferencePoint(string name, double x, double y, double z)
        {
            FeReferencePoint point = new FeReferencePoint(name, x, y, z);
            _referencePoints.Add(name, point);
        }

        public string[] AddMesh(FeMesh mesh)
        {
            int count;
            string entryName;

            // Renumber nodes
            mesh.RenumberNodes(_maxNodeId + 1);
            foreach (var entry in mesh.Nodes)
            {
                _nodes.Add(entry.Key, entry.Value);
            }
            _maxNodeId = mesh.MaxNodeId;

            // Renumber elements
            mesh.RenumberElements(_maxElementId + 1);
            foreach (var entry in mesh.Elements)
            {
                _elements.Add(entry.Key, entry.Value);
            }
            _maxElementId = mesh.MaxElementId;



            // Add and rename nodeSets
            count = 1;
            foreach (var entry in mesh.NodeSets)
            {
                entryName = entry.Key;
                if (_nodeSets.ContainsKey(entryName))
                {
                    entryName += "_Im-";
                    while (_nodeSets.ContainsKey(entryName + count.ToString())) count++;
                    entryName += count.ToString();
                    entry.Value.Name = entryName;
                }
                _nodeSets.Add(entry.Value.Name, entry.Value);
            }

            // Add and rename elementSets
            count = 1;
            foreach (var entry in mesh.ElementSets)
            {
                entryName = entry.Key;
                if (_elementSets.ContainsKey(entryName))
                {
                    entryName += "_Im-";
                    while (_elementSets.ContainsKey(entryName + count.ToString())) count++;
                    entryName += count.ToString();
                    entry.Value.Name = entryName;
                }
                _elementSets.Add(entry.Value.Name, entry.Value);
            }

            // Renumber parts
            int maxPartID = 0;
            foreach (var entry in _parts)
            {
                if (entry.Value.PartId > maxPartID) maxPartID = entry.Value.PartId;
            }
            mesh.RenumberParts(maxPartID + 1);
           

            // Add and rename parts
            count = 1;
            List<string> addedPartNames = new List<string>();
            foreach (var entry in mesh.Parts)
            {
                entryName = entry.Key;
                if (_parts.ContainsKey(entryName))
                {
                    entryName += "_Copy-";
                    while (_parts.ContainsKey(entryName + count.ToString())) count++;
                    entryName += count.ToString();
                }

                entry.Value.Name = entryName;
                _parts.Add(entryName, entry.Value);
                addedPartNames.Add(entryName);
            }
            return addedPartNames.ToArray();
        }
        public string[] AddPartsFromMesh(FeMesh mesh, string[] partNames)
        {
            FeMesh partialMesh = new FeMesh(mesh, partNames);
            return AddMesh(partialMesh);
        }
        #endregion #################################################################################################################

        #region Remove entities ####################################################################################################
        public string[] RemoveUnreferencedNodes(HashSet<int> possiblyUnrefNodeIds, bool removeEmptyNodeSets)
        {
            // for each node find it's connected elements
            Dictionary<int, List<FeElement>> nodeElements = new Dictionary<int, List<FeElement>>();

            foreach (var entry in _elements)
            {
                foreach (var nodeId in entry.Value.NodeIds)
                {
                    if (nodeElements.ContainsKey(nodeId)) nodeElements[nodeId].Add(entry.Value);
                    else nodeElements.Add(nodeId, new List<FeElement>() { entry.Value });
                }
            }

            // get unreferenced nodes
            HashSet<int> unreferenced = new HashSet<int>();
            foreach (var nodeId in possiblyUnrefNodeIds)
            {
                if (!nodeElements.ContainsKey(nodeId)) unreferenced.Add(nodeId);
            }

            // remove unreferenced nodes
            foreach (var key in unreferenced) _nodes.Remove(key);

            // remove unreferenced nodes from node sets
            List<int> newNodeSetLabels = new List<int>();
            List<string> emptyNodeSets = new List<string>();
            List<string> changedNodeSets = new List<string>();
            foreach (var entry in _nodeSets)
            {
                newNodeSetLabels.Clear();
                foreach (var id in entry.Value.Labels)
                {
                    if (!unreferenced.Contains(id)) newNodeSetLabels.Add(id);
                }
                if (newNodeSetLabels.Count != entry.Value.Labels.Length)
                {
                    entry.Value.Labels = newNodeSetLabels.ToArray();
                    changedNodeSets.Add(entry.Key);
                }
                if (entry.Value.Labels.Length == 0) emptyNodeSets.Add(entry.Key);
            }

            // changed node sets
            FeNodeSet nodeSet;
            foreach (string name in changedNodeSets)
            {
                nodeSet = _nodeSets[name];
                UpdateNodeSetCenterOfGravity(nodeSet);
                nodeSet.CreationData = null;    // change creation data (from mouse selection) to ids
                nodeSet.Valid = false;          // mark it as unvalid to highlight it for the user
            }

            // remove empty node sets
            if (removeEmptyNodeSets)
            { 
                foreach (var name in emptyNodeSets) _nodeSets.Remove(name);
            }

            return emptyNodeSets.ToArray();
        }
        public string[] RemoveElementsFromElementSets(HashSet<int> removedElementIds, bool removeEmptyElementSets)
        {
            List<int> newElementSetLabels = new List<int>();
            List<string> emptyElementSets = new List<string>();
            List<string> changedElementSets = new List<string>();

            foreach (var entry in _elementSets)
            {
                newElementSetLabels.Clear();
                foreach (var id in entry.Value.Labels)
                {
                    if (!removedElementIds.Contains(id)) newElementSetLabels.Add(id);
                }
                if (newElementSetLabels.Count != entry.Value.Labels.Length)
                {
                    entry.Value.Labels = newElementSetLabels.ToArray();
                    changedElementSets.Add(entry.Key);
                }
                if (entry.Value.Labels.Length == 0) emptyElementSets.Add(entry.Key);
            }

            // changed element sets
            FeElementSet elementSet;
            foreach (string name in changedElementSets)
            {
                elementSet = _elementSets[name];
                elementSet.CreationData = null;     // change creation data (from mouse selection) to created from ids
                elementSet.Valid = false;           // mark it as unvalid to highlight it for the user
            }

            // remove empty element sets
            if (removeEmptyElementSets)
            {
                foreach (var name in emptyElementSets) _elementSets.Remove(name);
            }

            return changedElementSets.ToArray();
        }
        public string[] RemoveElementsFromSurfaceFaces(HashSet<int> removedElementIds,
                                                       bool removeEmptySurfaces, 
                                                       bool removeForRemeshing)
        {
            int elementId;
            List<int> newSurfaceFaceIds = new List<int>();
            List<string> changedSurfaces = new List<string>();
            List<string> emptySurfaces = new List<string>();
            foreach (var entry in _surfaces)
            {
                newSurfaceFaceIds.Clear();

                if (entry.Value.FaceIds != null)
                {
                    foreach (var faceId in entry.Value.FaceIds)
                    {
                        elementId = faceId / 10;
                        if (!removedElementIds.Contains(elementId)) newSurfaceFaceIds.Add(faceId);
                    }
                    if (entry.Value.FaceIds.Length != newSurfaceFaceIds.Count)
                    {
                        entry.Value.FaceIds = newSurfaceFaceIds.ToArray();
                        changedSurfaces.Add(entry.Key);
                    }
                    if (entry.Value.FaceIds.Length == 0) emptySurfaces.Add(entry.Key);
                }
            }


            // changed surfaces
            bool geometryBased;
            FeSurface surface;
            foreach (string name in changedSurfaces)
            {
                surface = _surfaces[name];
                geometryBased = surface.CreationData.IsGeometryBased();

                // do not change the geometry based surface if remeshing is done
                if (!(removeForRemeshing && geometryBased)) 
                {
                    surface = new FeSurface(_surfaces[name]);
                    surface.CreationData = new Selection();
                    surface.CreationData.Add(new SelectionNodeIds(vtkSelectOperation.None, false, surface.FaceIds));
                    surface.Valid = false;      // mark it as unvalid to highlight it for the user
                    _surfaces[name] = surface;
                }
            }

            // remove empty surfaces
            if (removeEmptySurfaces)
            {
                string[] nodeSets;
                string[] elementSets;
                RemoveSurfaces(emptySurfaces.ToArray(), out nodeSets, out elementSets);
            }

            return changedSurfaces.ToArray();
        }

        public int[] RemoveParts(string[] partNames, out string[] removedParts, bool removeForRemeshing)
        {
            int[] removedPartIds = new int[partNames.Length];
            HashSet<int> possiblyUnrefNodeIds = new HashSet<int>();
            HashSet<string> removedPartsHashSet = new HashSet<string>();
            HashSet<int> removedElementIds = new HashSet<int>();

            int partCount = 0;
            foreach (var name in partNames)
            {
                if (!_parts.ContainsKey(name))
                {
                    removedPartIds[partCount] = -1;
                    continue;
                }
                removedPartIds[partCount] = _parts[name].PartId;

                // remove elements
                foreach (int elementId in _parts[name].Labels)
                {
                    foreach (int nodeId in _elements[elementId].NodeIds)
                    {
                        possiblyUnrefNodeIds.Add(nodeId);
                        removedElementIds.Add(elementId);
                    }

                    _elements.Remove(elementId);
                }

                // remove parts
                _parts.Remove(name);
                removedPartsHashSet.Add(name);

                partCount++;
            }
            removedParts = removedPartsHashSet.ToArray();

            // remove unreferenced nodes and keep empty node sets
            RemoveUnreferencedNodes(possiblyUnrefNodeIds, false);

            // remove elements from element sets and find empty element sets but do not remove them
            string[] changedElementSets = RemoveElementsFromElementSets(removedElementIds, false);

            // find changed surface
            string[] changedSurfaces = RemoveElementsFromSurfaceFaces(removedElementIds, false, removeForRemeshing);

            ComputeBoundingBox();

            return removedPartIds;
        }
        public void RemoveSurfaces(string[] surfaceNames, out string[] removedNodeSets, out string[] removedElementSets)
        {
            HashSet<string> removedElementSetsHashSet = new HashSet<string>();
            HashSet<string> removedNodeSetsHashSet = new HashSet<string>();
            FeSurface surface;
            foreach (var name in surfaceNames)
            {
                // remove old element sets
                surface = Surfaces[name];
                if (surface.ElementFaces != null)
                {
                    foreach (var entry in surface.ElementFaces)
                    {
                        if (_elementSets.ContainsKey(entry.Value))
                        {
                            _elementSets.Remove(entry.Value);
                            removedElementSetsHashSet.Add(entry.Value);
                        }
                    }
                }

                if (!(surface.Type == FeSurfaceType.Node && surface.CreatedFrom == FeSurfaceCreatedFrom.NodeSet)) 
                {
                    if (surface.NodeSetName != null) // null is in the case when no elements were found to form a surface
                    {
                        removedNodeSetsHashSet.Add(surface.NodeSetName);
                        _nodeSets.Remove(surface.NodeSetName);
                    }
                }
                
                // remove surface
                Surfaces.Remove(name);
            }
            removedNodeSets = removedNodeSetsHashSet.ToArray();
            removedElementSets = removedElementSetsHashSet.ToArray();
        }
        public void RemoveElementsByType<T>()
        {
            HashSet<int> removedElementIds = new HashSet<int>();
            HashSet<int> possiblyUnrefNodeIds = new HashSet<int>();

            foreach (var entry in _elements)
            {
                if (entry.Value is T)
                {
                    // get removed element ids
                    foreach (int nodeId in entry.Value.NodeIds) possiblyUnrefNodeIds.Add(nodeId);
                    removedElementIds.Add(entry.Key);
                }
            }
            // remove elements
            foreach (var elementId in removedElementIds) _elements.Remove(elementId);

            List<int> newLabels = new List<int>();
            List<string> emptyElementSets = new List<string>();

            // remove elements from element sets
            foreach (var entry in _elementSets)
            {
                newLabels.Clear();
                foreach (var id in entry.Value.Labels)
                {
                    if (!removedElementIds.Contains(id)) newLabels.Add(id);
                }
                if (newLabels.Count == 0) emptyElementSets.Add(entry.Key);
                else entry.Value.Labels = newLabels.ToArray();
            }

            foreach (var key in emptyElementSets)
            {
                _elementSets.Remove(key);
            }

            List<string> emptyParts = new List<string>();

            // remove elements from Parts
            foreach (var entry in _parts)
            {
                newLabels.Clear();
                foreach (var id in entry.Value.Labels)
                {
                    if (!removedElementIds.Contains(id)) newLabels.Add(id);
                }
                if (newLabels.Count == 0) emptyParts.Add(entry.Key);
                else entry.Value.Labels = newLabels.ToArray();
            }

            foreach (var key in emptyParts)
            {
                _parts.Remove(key);
            }

            RemoveUnreferencedNodes(possiblyUnrefNodeIds, true);

            ComputeBoundingBox();
        }

        #endregion #################################################################################################################



        // Nodes 
        public double[][] GetAllNodeCoor()
        {
            double[][] coor = new double[_nodes.Count][];
            int count = 0;
            foreach (int key in _nodes.Keys)
            {
                coor[count++] = _nodes[key].Coor;
            }
            return coor;
        }
        public string[] GetNodeSetNames()
        {
            return _nodeSets.Keys.ToArray();
        }
        public void UpdateNodeSetCenterOfGravity(FeNodeSet nodeSet)
        {
            double[] coor;
            double[] centerOfGravity = new double[3];
            double[][] boundingBox = new double[3][];

            for (int i = 0; i < 3; i++) boundingBox[i] = new double[2];

            boundingBox[0][0] = double.MaxValue;
            boundingBox[1][0] = double.MaxValue;
            boundingBox[2][0] = double.MaxValue;
            boundingBox[0][1] = -double.MaxValue;
            boundingBox[1][1] = -double.MaxValue;
            boundingBox[2][1] = -double.MaxValue;

            if (nodeSet.Labels != null && nodeSet.Labels.Length > 0)
            {
                foreach (var nodeId in nodeSet.Labels)
                {
                    coor = _nodes[nodeId].Coor;

                    for (int i = 0; i < 3; i++)
                    {
                        centerOfGravity[i] += coor[i];

                        if (coor[i] < boundingBox[i][0]) boundingBox[i][0] = coor[i];
                        if (coor[i] > boundingBox[i][1]) boundingBox[i][1] = coor[i];
                    }
                }
                centerOfGravity[0] /= nodeSet.Labels.Length;
                centerOfGravity[1] /= nodeSet.Labels.Length;
                centerOfGravity[2] /= nodeSet.Labels.Length;
            }

            nodeSet.CenterOfGravity = centerOfGravity;
            nodeSet.BoundingBox = boundingBox;
        }
        public double[][] GetNodeSetCoor(int[] nodeIds)
        {
            double[][] coor = null;

            coor = new double[nodeIds.Length][];
            for (int i = 0; i < nodeIds.Length; i++)
            {
                coor[i] = _nodes[nodeIds[i]].Coor;
            }

            return coor;
        }
        public int[] GetVisibleNodeIds()
        {
            HashSet<int> ids = new HashSet<int>();
            foreach (var entry in _parts)
            {
                if (entry.Value.Visible) ids.UnionWith(entry.Value.NodeLabels);
            }
            return ids.ToArray();
        }

        // Elements 
        public string[] GetElementSetNames()
        {
            return _elementSets.Keys.ToArray();
        }
        public void GetElementFaceCenter(int elementId, FeFaceName faceName, out double[] faceCenter)
        {
            FeNode[] nodes;
            FeElement element = _elements[elementId];
            int[] nodeIds = element.GetNodeIdsFromFaceName(faceName);
            faceCenter = null;
            if (element is LinearTetraElement || element is ParabolicTetraElement)
            {
                nodes = new FeNode[3];
                faceCenter = new double[nodes.Length];
                for (int i = 0; i < nodes.Length; i++)
                {
                    nodes[i] = _nodes[nodeIds[i]];
                    faceCenter[0] += nodes[i].X;
                    faceCenter[1] += nodes[i].Y;
                    faceCenter[2] += nodes[i].Z;
                }
                faceCenter[0] /= nodes.Length;
                faceCenter[1] /= nodes.Length;
                faceCenter[2] /= nodes.Length;
            }
            else if (element is LinearWedgeElement || element is ParabolicWedgeElement)
            {
                if (faceName == FeFaceName.S1 || faceName == FeFaceName.S2) nodes = new FeNode[3];
                else nodes = new FeNode[4];

                faceCenter = new double[nodes.Length];
                for (int i = 0; i < nodes.Length; i++)
                {
                    nodes[i] = _nodes[nodeIds[i]];
                    faceCenter[0] += nodes[i].X;
                    faceCenter[1] += nodes[i].Y;
                    faceCenter[2] += nodes[i].Z;
                }
                faceCenter[0] /= nodes.Length;
                faceCenter[1] /= nodes.Length;
                faceCenter[2] /= nodes.Length;
            }
            else if (element is LinearHexaElement || element is ParabolicHexaElement)
            {
                nodes = new FeNode[4];
                faceCenter = new double[nodes.Length];
                for (int i = 0; i < nodes.Length; i++)
                {
                    nodes[i] = _nodes[nodeIds[i]];
                    faceCenter[0] += nodes[i].X;
                    faceCenter[1] += nodes[i].Y;
                    faceCenter[2] += nodes[i].Z;
                }
                faceCenter[0] /= nodes.Length;
                faceCenter[1] /= nodes.Length;
                faceCenter[2] /= nodes.Length;
            }
            else throw new NotSupportedException();
        }
        public void GetElementFaceNormal(int elementId, FeFaceName faceName, out double[] faceNormal)
        {
            FeNode[] nodes;
            FeElement element = _elements[elementId];
            int[] nodeIds = element.GetNodeIdsFromFaceName(faceName);
            faceNormal = null;
            if (element is LinearTetraElement || element is ParabolicTetraElement)
            {
                nodes = new FeNode[3];
                for (int i = 0; i < nodes.Length; i++) nodes[i] = _nodes[nodeIds[i]];

                // element normal to inside
                faceNormal = ComputeNormalFromCellIndices(nodes[0], nodes[1], nodes[2]).Coor;
            }
            else if (element is LinearWedgeElement || element is ParabolicWedgeElement)
            {
                if (faceName == FeFaceName.S1 || faceName == FeFaceName.S2) nodes = new FeNode[3];
                else nodes = new FeNode[4];

                for (int i = 0; i < nodes.Length; i++) nodes[i] = _nodes[nodeIds[i]];

                // element normal to inside
                if (faceName == FeFaceName.S1) faceNormal = ComputeNormalFromCellIndices(nodes[0], nodes[1], nodes[2]).Coor;
                // element normal to outside
                else faceNormal = ComputeNormalFromCellIndices(nodes[0], nodes[2], nodes[1]).Coor;
            }
            else if (element is LinearHexaElement || element is ParabolicHexaElement)
            {
                nodes = new FeNode[4];
                for (int i = 0; i < nodes.Length; i++) nodes[i] = _nodes[nodeIds[i]];

                // element normal to inside
                faceNormal = ComputeNormalFromCellIndices(nodes[0], nodes[1], nodes[2]).Coor;
            }
            else throw new NotSupportedException();
        }
        public void GetElementFaceCenterAndNormal(int elementId, FeFaceName faceName, out double[] faceCenter, out double[] faceNormal)
        {
            FeNode[] nodes;
            FeElement element = _elements[elementId];
            int[] nodeIds = element.GetNodeIdsFromFaceName(faceName);
            faceCenter = null;
            faceNormal = null;
            if (element is LinearTetraElement || element is ParabolicTetraElement)
            {
                nodes = new FeNode[3];
                faceCenter = new double[nodes.Length];
                for (int i = 0; i < nodes.Length; i++)
                {
                    nodes[i] = _nodes[nodeIds[i]];
                    faceCenter[0] += nodes[i].X;
                    faceCenter[1] += nodes[i].Y;
                    faceCenter[2] += nodes[i].Z;
                }
                faceCenter[0] /= nodes.Length;
                faceCenter[1] /= nodes.Length;
                faceCenter[2] /= nodes.Length;

                // element normal to inside
                faceNormal = ComputeNormalFromCellIndices(nodes[0], nodes[1], nodes[2]).Coor;
            }
            else if (element is LinearWedgeElement || element is ParabolicWedgeElement)
            {
                if (faceName == FeFaceName.S1 || faceName == FeFaceName.S2) nodes = new FeNode[3];
                else nodes = new FeNode[4];
                
                faceCenter = new double[nodes.Length];
                for (int i = 0; i < nodes.Length; i++)
                {
                    nodes[i] = _nodes[nodeIds[i]];
                    faceCenter[0] += nodes[i].X;
                    faceCenter[1] += nodes[i].Y;
                    faceCenter[2] += nodes[i].Z;
                }
                faceCenter[0] /= nodes.Length;
                faceCenter[1] /= nodes.Length;
                faceCenter[2] /= nodes.Length;

                // element normal to inside
                if (faceName == FeFaceName.S1) faceNormal = ComputeNormalFromCellIndices(nodes[0], nodes[1], nodes[2]).Coor;
                // element normal to outside
                else faceNormal = ComputeNormalFromCellIndices(nodes[0], nodes[2], nodes[1]).Coor;
            }
            else if (element is LinearHexaElement || element is ParabolicHexaElement)
            {
                nodes = new FeNode[4];
                faceCenter = new double[nodes.Length];
                for (int i = 0; i < nodes.Length; i++)
                {
                    nodes[i] = _nodes[nodeIds[i]];
                    faceCenter[0] += nodes[i].X;
                    faceCenter[1] += nodes[i].Y;
                    faceCenter[2] += nodes[i].Z;
                }
                faceCenter[0] /= nodes.Length;
                faceCenter[1] /= nodes.Length;
                faceCenter[2] /= nodes.Length;

                // element normal to inside
                faceNormal = ComputeNormalFromCellIndices(nodes[0], nodes[1], nodes[2]).Coor;
            }
            else throw new NotSupportedException();
        }        
        public int[] GetVisibleElementIds()
        {
            HashSet<int> ids = new HashSet<int>();
            foreach (var entry in _parts)
            {
                if (entry.Value.Visible) ids.UnionWith(entry.Value.Labels);
            }
            return ids.ToArray();
        }
      

        // Cells 
        public void GetAllNodesAndCells(FeGroup elementSet, out int[] nodeIds, out double[][] nodeCoor, out int[] cellIds, out int[][] cells, out int[] cellTypes)
        {
            cellIds = elementSet.Labels;
            cells = new int[cellIds.Length][];
            cellTypes = new int[cellIds.Length];
            int i = 0;
            FeElement element;

            System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();
            watch.Start();

            if (elementSet is BasePart part)
            {
                foreach (var elemId in part.Labels)
                {
                    element = _elements[elemId];
                    // copy the array because it will be renumbered
                    cells[i] = element.GetVtkNodeIds();
                    cellTypes[i] = element.GetVtkCellType();
                    i++;
                }

                //foreach (var entry in _elements)    // POSSIBLY SLOW
                //{
                //    element = entry.Value;
                //    if (part.PartId == element.PartId)
                //    {
                //        // copy the array because it will be renumbered
                //        cells[i] = element.GetVtkNodeIds();
                //        cellTypes[i] = element.GetVtkCellType();
                //        i++;
                //    }
                //}
            }
            else
            {
                // get all cells and all node ids for elementSet
                for (i = 0; i < cellIds.Length; i++)
                {
                    // copy the array because it will be renumbered
                    element = _elements[cellIds[i]];
                    cells[i] = element.GetVtkNodeIds();
                    cellTypes[i] = element.GetVtkCellType();
                }
            }
            nodeIds = GetRenumberedNodesAndCells(out nodeCoor, ref cells);
        }
        public void GetVisualizationNodesAndCells(BasePart part, out int[] nodeIds, out double[][] nodeCoor, out int[] cellIds, out int[][] cells, out int[] cellTypes)
        {
            cellIds = part.Visualization.CellIds.ToArray();
            int[][] visualizationCells = part.Visualization.Cells;

            List<int> nodeIdsList = new List<int>();
            List<double[]> nodeCoorList = new List<double[]>();
            List<int> cellIdsList = new List<int>();
            List<int[]> cellsList = new List<int[]>();
            List<int> cellTypesList = new List<int>();
            int visualizationCellId;

            // Create visualization for each visualization face
            if (part.PartType == PartType.Solid || part.PartType == PartType.Shell)
            {
                //if (part.Visualization.CellIdsByFace != null)
                {
                    for (int i = 0; i < part.Visualization.CellIdsByFace.Length; i++)
                    {
                        cellIds = new int[part.Visualization.CellIdsByFace[i].Length];
                        cells = new int[part.Visualization.CellIdsByFace[i].Length][];
                        cellTypes = new int[part.Visualization.CellIdsByFace[i].Length];

                        for (int j = 0; j < part.Visualization.CellIdsByFace[i].Length; j++)
                        {
                            visualizationCellId = part.Visualization.CellIdsByFace[i][j];
                            cellIds[j] = part.Visualization.CellIds[visualizationCellId];
                            cells[j] = visualizationCells[visualizationCellId].ToArray(); // node ids in cells will be renumbered

                            if (cells[j].Length == 3) cellTypes[j] = (int)vtkCellType.VTK_TRIANGLE;
                            else if (cells[j].Length == 4) cellTypes[j] = (int)vtkCellType.VTK_QUAD;
                            else if (cells[j].Length == 6) cellTypes[j] = (int)vtkCellType.VTK_QUADRATIC_TRIANGLE;
                            else if (cells[j].Length == 8) cellTypes[j] = (int)vtkCellType.VTK_QUADRATIC_QUAD;
                            else throw new NotSupportedException();
                        }
                        nodeIds = GetRenumberedNodesAndCells(nodeIdsList.Count, out nodeCoor, ref cells);

                        nodeIdsList.AddRange(nodeIds);
                        nodeCoorList.AddRange(nodeCoor);
                        cellIdsList.AddRange(cellIds);
                        cellsList.AddRange(cells);
                        cellTypesList.AddRange(cellTypes);
                    }
                }

                nodeIds = nodeIdsList.ToArray();
                nodeCoor = nodeCoorList.ToArray();
                cellIds = cellIdsList.ToArray();
                cells = cellsList.ToArray();
                cellTypes = cellTypesList.ToArray();
            }
            else if (part.PartType == PartType.Wire)
            {
                cellIds = part.Visualization.CellIds.ToArray();
                cells = new int[visualizationCells.Length][];
                cellTypes = new int[visualizationCells.Length];

                for (int i = 0; i < visualizationCells.Length; i++)
                {
                    cells[i] = visualizationCells[i].ToArray();

                    if (cells[i].Length == 2) cellTypes[i] = (int)vtkCellType.VTK_LINE;
                    else if (cells[i].Length == 3) cellTypes[i] = (int)vtkCellType.VTK_QUADRATIC_EDGE;
                    else throw new NotSupportedException();
                }

                nodeIds = GetRenumberedNodesAndCells(out nodeCoor, ref cells);
            }
            else
            {
                throw new NotSupportedException();
            }

            
        }
        public void GetNodesAndCellsForModelEdges(FeGroup elementSet, out int[] nodeIds, out double[][] nodeCoor, out int[][] cells, out int[] cellTypes)
        {
            nodeIds = null;
            nodeCoor = null;
            cells = null;
            cellTypes = null;

            if (elementSet is BasePart part)
            {
                int[][] edgeCells;
                if (part.PartType == PartType.Solid || part.PartType == PartType.Shell) edgeCells = part.Visualization.EdgeCells;
                else if (part.PartType == PartType.Wire) edgeCells = part.Visualization.Cells;
                else throw new Exception();

                GetNodesAndCellsForEdges(edgeCells, out nodeIds, out nodeCoor, out cells, out cellTypes);
            }
        }
        public void GetNodesAndCellsForEdges(int[][] edgeCells, out int[] nodeIds, out double[][] nodeCoor, out int[][] cells, out int[] cellTypes)
        {
            cells = new int[edgeCells.Length][];
            cellTypes = new int[edgeCells.Length];
            for (int i = 0; i < edgeCells.Length; i++)
            {
                cells[i] = new int[edgeCells[i].Length];
                edgeCells[i].CopyTo(cells[i], 0);

                if (cells[i].Length == 2) cellTypes[i] = (int)vtkCellType.VTK_LINE;
                else if (cells[i].Length == 3) cellTypes[i] = (int)vtkCellType.VTK_QUADRATIC_EDGE;
                else throw new NotSupportedException();
            }

            nodeIds = GetRenumberedNodesAndCells(out nodeCoor, ref cells);
        }
        private int[] GetRenumberedNodesAndCells(out double[][] nodeCoor, ref int[][] cells)
        {
            return GetRenumberedNodesAndCells(0, out nodeCoor, ref cells);
        }
        private int[] GetRenumberedNodesAndCells(int firstNodeId, out double[][] nodeCoor, ref int[][] cells)
        {
            HashSet<int> nodeIds = new HashSet<int>();

            // get all cells and all nodes ids for elementSet
            for (int i = 0; i < cells.Length; i++)
            {
                for (int j = 0; j < cells[i].Length; j++)
                {
                    nodeIds.Add(cells[i][j]);
                }
            }

            // get all node coordinates and prepare re-numbering map
            Dictionary<int, int> oldIds = new Dictionary<int, int>();   // the order of items is not retained
            int[] orderedNodeIds = new int[nodeIds.Count];
            nodeCoor = new double[nodeIds.Count][];
            int count = 0;
            foreach (int nodeId in nodeIds)
            {
                nodeCoor[count] = _nodes[nodeId].Coor;
                oldIds.Add(nodeId, count);
                orderedNodeIds[count] = nodeId;
                count++;
            }

            for (int i = 0; i < cells.Length; i++)
            {
                for (int j = 0; j < cells[i].Length; j++)
                {
                    cells[i][j] = oldIds[cells[i][j]] + firstNodeId;
                }
            }

            return orderedNodeIds;       // return ordered node ids for the nodeCoords
        }
        public int[][] GetFreeEdgesFromVisualizationCells(int[][] cells)
        {
            CompareIntArray comparer = new CompareIntArray();
            HashSet<int[]> freeEdges = new HashSet<int[]>(comparer);

            int[] key;
            int[][] cellEdges;

            // get free edges
            for (int i = 0; i < cells.Length; i++)
            {
                cellEdges = GetVisualizationEdgeCells(cells[i]);
                foreach (var cellEdge in cellEdges)
                {
                    key = cellEdge.ToArray();
                    Array.Sort(key);
                    if (!freeEdges.Add(key)) freeEdges.Remove(key);
                }
            }

            return freeEdges.ToArray();
        }
        public int[][] GetVisualizationEdgeCells(int[] cell)
        {
            //int[] lookUp3 = new int[] { 0, 1, 2, 0 };
            //int[] lookUp4 = new int[] { 0, 1, 2, 3, 0 };
            //int[] lookUp6 = new int[] { 0, 3, 1, 4, 2, 5, 0 };
            //int[] lookUp8 = new int[] { 0, 4, 1, 5, 2, 6, 3, 7, 0 };

            if (cell.Length == 3)
            {
                return new int[][] {    new int[] { cell[0], cell[1] },
                                        new int[] { cell[1], cell[2] },
                                        new int[] { cell[2], cell[0] } };
            }
            else if (cell.Length == 4)
            {
                return new int[][] {    new int[] { cell[0], cell[1] },
                                        new int[] { cell[1], cell[2] },
                                        new int[] { cell[2], cell[3] },
                                        new int[] { cell[3], cell[0] } };
            }
            else if (cell.Length == 6)
            {
                return new int[][] {    new int[] { cell[0], cell[1], cell[3] },
                                        new int[] { cell[1], cell[2], cell[4] },
                                        new int[] { cell[2], cell[0], cell[5] } };

                //return new int[][] {    new int[] { cell[0], cell[3], cell[1] },
                //                        new int[] { cell[1], cell[4], cell[2] },
                //                        new int[] { cell[2], cell[5], cell[0] } };
            }
            else if (cell.Length == 8)
            {
                return new int[][] {    new int[] { cell[0], cell[1], cell[4] },
                                        new int[] { cell[1], cell[2], cell[5] },
                                        new int[] { cell[2], cell[3], cell[6] },
                                        new int[] { cell[3], cell[0], cell[7] } };

                //return new int[][] {    new int[] { cell[0], cell[4], cell[1] },
                //                        new int[] { cell[1], cell[5], cell[2] },
                //                        new int[] { cell[2], cell[6], cell[3] },
                //                        new int[] { cell[3], cell[7], cell[0] } };
            }
            else throw new NotSupportedException();
        }

        // Analyze
        public double GetShortestEdgeLen()
        {
            double min = double.MaxValue;
            VisualizationData visualization;
            foreach (var entry in _parts)
            {
                visualization = entry.Value.Visualization;
                // for each edge
                for (int i = 0; i < visualization.EdgeLengths.Length; i++)
                {
                    if (visualization.EdgeLengths[i] < min) min = visualization.EdgeLengths[i];
                }
            }
            return min;
        }
        /// <summary>
        /// Returns an array of edges. Each edge is an array of connecteds points and each point is a 3D coordinate - array
        /// </summary>
        /// <param name="minEdgeLen"></param>
        /// <returns></returns>
        public double[][][] GetShortEdges(double minEdgeLen)
        {
            int edgeCellId;
            int[] nodeIds;
            List<double[]> oneEdgeNodeCoor = new List<double[]>();
            List<double[][]> allNodeCoor = new List<double[][]>();

            VisualizationData visualization;
            foreach (var entry in _parts)
            {
                visualization = entry.Value.Visualization;
                // for each edge
                for (int i = 0; i < visualization.EdgeLengths.Length; i++)                          
                {
                    if (visualization.EdgeLengths[i] < minEdgeLen)
                    {
                        oneEdgeNodeCoor.Clear();
                        // each edge cell
                        for (int j = 0; j < visualization.EdgeCellIdsByEdge[i].Length; j++)         
                        {
                            edgeCellId = visualization.EdgeCellIdsByEdge[i][j];
                            nodeIds = visualization.EdgeCells[edgeCellId];

                            // for each cell add the first node
                            oneEdgeNodeCoor.Add(_nodes[nodeIds[0]].Coor);
                            // for parabolic cell add the the middle node
                            if (nodeIds.Length == 3) oneEdgeNodeCoor.Add(_nodes[nodeIds[2]].Coor);
                            // for the last cell add the last node
                            if (j == visualization.EdgeCellIdsByEdge[i].Length - 1) oneEdgeNodeCoor.Add(_nodes[nodeIds[1]].Coor);
                        }

                        allNodeCoor.Add(oneEdgeNodeCoor.ToArray());
                    }
                }
            }
            return allNodeCoor.ToArray();
        }
        public double GetSmallestFace()
        {
            double min = double.MaxValue;
            VisualizationData visualization;
            foreach (var entry in _parts)
            {
                visualization = entry.Value.Visualization;
                // for each face
                for (int i = 0; i < visualization.FaceAreas.Length; i++)
                {
                    if (visualization.FaceAreas[i] < min) min = visualization.FaceAreas[i];
                }
            }
            return min;
        }
        public int[][] GetSmallestFaces(double minFaceArea)
        {
            int cellId;
            int[] cell;
            List<int[]> cells = new List<int[]>();

            VisualizationData visualization;
            foreach (var entry in _parts)
            {
                visualization = entry.Value.Visualization;
                // for each face
                for (int i = 0; i < visualization.FaceAreas.Length; i++)
                {
                    if (visualization.FaceAreas[i] < minFaceArea)
                    {
                        // each edge cell
                        for (int j = 0; j < visualization.CellIdsByFace[i].Length; j++)
                        {
                            cellId = visualization.CellIdsByFace[i][j];
                            cell = visualization.Cells[cellId];
                            cells.Add(cell.ToArray());
                        }
                    }
                }
            }
            return cells.ToArray();
        }

        // Read - Write
        public void WriteEdgeNodesToFile(GeometryPart part, string fileName)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("int		number of additional edges");
            sb.AppendLine(part.Visualization.EdgeCells.Length.ToString());

            FeNode node1;
            FeNode node2;
            foreach (int[] cell in part.Visualization.EdgeCells)
            {
                if (cell.Length > 2) throw new NotSupportedException();
                node1 = Nodes[cell[0]];
                node2 = Nodes[cell[1]];
                sb.AppendLine(String.Format("{0} {1} {2}", (float)node1.X, (float)node1.Y, (float)node1.Z));
                sb.AppendLine(String.Format("{0} {1} {2}", (float)node2.X, (float)node2.Y, (float)node2.Z));
            }

            System.IO.File.WriteAllText(fileName, sb.ToString());
        }
       
        // Transform
        public string[] TranslateParts(string[] partNames, double[] vector, bool copy)
        {            
            string[] translatedPartNames = partNames.ToArray();

            if (copy)
            {
                FeMesh mesh = this.DeepCopy();
                translatedPartNames = this.AddPartsFromMesh(mesh, translatedPartNames);
            }

            HashSet<int> nodeLabels = new HashSet<int>();
            foreach (var partName in translatedPartNames) nodeLabels.UnionWith(_parts[partName].NodeLabels);

            FeNode node;
            foreach (var nodeId in nodeLabels)
            {
                node = _nodes[nodeId];
                node.X += vector[0];
                node.Y += vector[1];
                node.Z += vector[2];
                _nodes[nodeId] = node;
            }

            // update node sets
            foreach (var entry in _nodeSets) UpdateNodeSetCenterOfGravity(entry.Value);

            // update reference points
            FeNodeSet nodeSet;
            foreach (var entry in _referencePoints)
            {
                if (_nodeSets.TryGetValue(entry.Value.NodeSetName, out nodeSet))
                    entry.Value.UpdateCoordinates(nodeSet);
            }

            // update bounding box
            ComputeBoundingBox();

            if (copy) return translatedPartNames;
            else return null;
        }


        // Clone
        public FeMesh DeepCopy()
        {
            FeMesh copy = this.DeepClone();

            copy.Nodes = new Dictionary<int, FeNode>();
            foreach (var entry in _nodes)
            {
                copy.Nodes.Add(entry.Key, entry.Value.DeepCopy());
            }

            copy.Elements = new Dictionary<int, FeElement>();
            foreach (var entry in _elements)
            {
                copy.Elements.Add(entry.Key, entry.Value.DeepCopy());
            }
         
            return copy;
        }


    }
}