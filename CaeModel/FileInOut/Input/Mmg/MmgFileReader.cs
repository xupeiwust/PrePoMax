﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using CaeMesh;

namespace FileInOut.Input
{
    static public class MmgFileReader
    {
        public static FeMesh Read(string fileName, MeshRepresentation meshRepresentation)
        {
            if (File.Exists(fileName))
            {
                FeNode node;
                LinearBeamElement beam;
                LinearTriangleElement triangle;
                Dictionary<int, FeNode> nodes = new Dictionary<int, FeNode>();
                Dictionary<int, FeElement> elements = new Dictionary<int, FeElement>();
                HashSet<int> surfaceNodeIds;
                HashSet<int> edgeNodeIds;
                Dictionary<int, HashSet<int>> surfaceIdNodeIds = new Dictionary<int, HashSet<int>>();
                Dictionary<int, HashSet<int>> edgeIdNodeIds = new Dictionary<int, HashSet<int>>();
                Dictionary<int, Dictionary<int, bool>> edgeIdNodeIdCount = new Dictionary<int, Dictionary<int, bool>>();
                Dictionary<int, bool> nodeIdCount;
                //
                int numOfNodes;
                int numOfElements;
                int numOfEdges;
                int elementId = 1;
                string[] splitter = new string[] { " " };
                string[] tmp;
                string[] lines = File.ReadAllLines(fileName);
                int surfaceId;
                int edgeId;
                int[] key;
                //
                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].ToUpper().StartsWith("VERTICES"))
                    {
                        i++;
                        if (i < lines.Length)
                        {
                            numOfNodes = int.Parse(lines[i]);
                            i++;
                            for (int j = 0; j < numOfNodes && i + j < lines.Length; j++)
                            {
                                tmp = lines[i + j].Split(splitter, StringSplitOptions.RemoveEmptyEntries);
                                if (tmp.Length >= 3)
                                {
                                    node = new FeNode();
                                    node.Id = j + 1;
                                    node.X = double.Parse(tmp[0]);
                                    node.Y = double.Parse(tmp[1]);
                                    node.Z = double.Parse(tmp[2]);
                                    nodes.Add(node.Id, node);
                                }
                            }
                        }
                    }
                    else if (lines[i].ToUpper().StartsWith("TRIANGLES"))
                    {
                        i++;
                        if (i < lines.Length)
                        {
                            numOfElements = int.Parse(lines[i]);
                            i++;
                            for (int j = 0; j < numOfElements && i + j < lines.Length; j++)
                            {
                                tmp = lines[i + j].Split(splitter, StringSplitOptions.RemoveEmptyEntries);
                                if (tmp.Length >= 4)
                                {
                                    triangle = new LinearTriangleElement(elementId++, new int[3]);
                                    triangle.NodeIds[0] = int.Parse(tmp[0]);
                                    triangle.NodeIds[1] = int.Parse(tmp[1]);
                                    triangle.NodeIds[2] = int.Parse(tmp[2]);
                                    surfaceId = int.Parse(tmp[3]) - 1;
                                    //
                                    elements.Add(triangle.Id, triangle);
                                    if (surfaceIdNodeIds.TryGetValue(surfaceId, out surfaceNodeIds))
                                        surfaceNodeIds.UnionWith(triangle.NodeIds);
                                    else
                                        surfaceIdNodeIds.Add(surfaceId, new HashSet<int>(triangle.NodeIds));
                                }
                            }
                        }
                    }
                    else if (lines[i].ToUpper().StartsWith("EDGES"))
                    {
                        i++;
                        if (i < lines.Length)
                        {
                            numOfEdges = int.Parse(lines[i]);
                            i++;
                            for (int j = 0; j < numOfEdges && i + j < lines.Length; j++)
                            {
                                tmp = lines[i + j].Split(splitter, StringSplitOptions.RemoveEmptyEntries);
                                if (tmp.Length >= 3)
                                {
                                    edgeId = int.Parse(tmp[2]);
                                    if (edgeId > 0)
                                    {
                                        edgeId--;
                                        //
                                        if (!edgeIdNodeIdCount.TryGetValue(edgeId, out nodeIdCount))
                                        {
                                            nodeIdCount = new Dictionary<int, bool>();
                                            edgeIdNodeIdCount.Add(edgeId, nodeIdCount);
                                        }
                                        //
                                        beam = new LinearBeamElement(elementId++, new int[2]);
                                        beam.NodeIds[0] = int.Parse(tmp[0]);
                                        beam.NodeIds[1] = int.Parse(tmp[1]);
                                        //
                                        if (!nodeIdCount.Remove(beam.NodeIds[0])) nodeIdCount.Add(beam.NodeIds[0], true);
                                        if (!nodeIdCount.Remove(beam.NodeIds[1])) nodeIdCount.Add(beam.NodeIds[1], true);
                                        //
                                        elements.Add(beam.Id, beam);
                                        if (edgeIdNodeIds.TryGetValue(edgeId, out edgeNodeIds))
                                            edgeNodeIds.UnionWith(beam.NodeIds);
                                        else
                                            edgeIdNodeIds.Add(edgeId, new HashSet<int>(beam.NodeIds));
                                    }
                                }
                            }
                        }
                    }
                }
                HashSet<int> vertexNodeIds = new HashSet<int>();
                foreach (var entry in edgeIdNodeIdCount) vertexNodeIds.UnionWith(entry.Value.Keys);
                //
                FeMesh mesh = new FeMesh(nodes, elements, meshRepresentation, null, null, false,
                                         ImportOptions.DetectEdges);
                //
                mesh.ConvertLineFeElementsToEdges(vertexNodeIds);
                //
                Dictionary<string, Dictionary<int,int>> partIdNewSurfIdOldSurfId = 
                    mesh.RenumberVisualizationSurfaces(surfaceIdNodeIds);
                Dictionary<string, Dictionary<int, int>> partIdNewEdgeIdOldEdgeId = 
                    mesh.RenumberVisualizationEdges(edgeIdNodeIds);
                //
                mesh.RemoveElementsByType<FeElement1D>();
                //
                return mesh;
            }
            //
            return null;
        }
        private static int[] GetSortedKey(int id1, int id2)
        {
            if (id1 < id2) return new int[] { id1, id2 };
            else return new int[] { id2, id1 };
        }
    }
}
