﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using CaeMesh;
using CaeGlobals;

namespace CaeResults
{
    [Serializable]
    public static class DatFileReader
    {
        private static readonly string nameDisplacements = "Displacements";
        private static readonly string nameForces = "Forces";
        private static readonly string nameTotalForce = "Total force";
        private static readonly string nameStresses = "Stresses";
        private static readonly string nameStrains = "Strains";
        private static readonly string nameMechanicalStrains = "Mechanical strains";
        private static readonly string nameEquivalentPlasticStrains = "Equivalent plastic strain";
        private static readonly string nameInternalEnergyDensity = "Internal energy density";

        private static readonly string nameVolume = "Volume";
        private static readonly string nameTotalVolume = "Total volume";
        private static readonly string nameInternalEnergy = "Internal energy";
        private static readonly string nameTotalInternalEnergy = "Total internal energy";
        private static readonly string nameError = "Error";

        private static readonly string[] spaceSplitter = new string[] { " " };
        private static readonly string[] commaSplitter = new string[] { "," };
        private static readonly string[] underscoreSplitter = new string[] { "_" };
        private static readonly string[] parenthesesSplitter = new string[] { "(", ")" };
        private static readonly string[] componentsSplitter = new string[] { " ", "," };
        private static readonly string[] dataSplitter = new string[] { " ", "for set", "and time" };

        private static readonly Dictionary<string, string> compMap = new Dictionary<string, string>()
        {
            { "Id", "Id" },
            //
            { "U1", "UR1" },
            { "U2", "UR2" },
            { "U3", "UR3" },
            //
            { "RF1", "RM1" },
            { "RF2", "RM2" },
            { "RF3", "RM3" },
        };


        // Methods                                                                                                                  
        static public HistoryResults Read(string fileName)
        {
            if (fileName != null && File.Exists(fileName))
            {
                List<string> lines = new List<string>();

                if (!CaeGlobals.Tools.WaitForFileToUnlock(fileName, 5000)) return null;

                using (FileStream fileStream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (StreamReader streamReader = new StreamReader(fileStream))
                {
                    while (!streamReader.EndOfStream) lines.Add(streamReader.ReadLine()); // faster than streamReader.ReadToEnd().Split ...

                    streamReader.Close();
                    fileStream.Close();
                }

                List<string> dataSetNames = new List<string>();
                dataSetNames.Add(nameDisplacements);
                dataSetNames.Add(nameForces);
                dataSetNames.Add(nameTotalForce);
                dataSetNames.Add(nameStresses);
                dataSetNames.Add(nameStrains);
                dataSetNames.Add(nameMechanicalStrains);
                dataSetNames.Add(nameEquivalentPlasticStrains);
                dataSetNames.Add(nameInternalEnergyDensity);
                dataSetNames.Add(nameInternalEnergy);
                dataSetNames.Add(nameTotalInternalEnergy);
                dataSetNames.Add(nameVolume);
                dataSetNames.Add(nameTotalVolume);

                List<string[]> dataSetLinesList = SplitToDataSetLinesList(dataSetNames, lines.ToArray());
                Repair(dataSetLinesList, dataSetNames);

                DatDataSet dataSet;
                List<DatDataSet> dataSets = new List<DatDataSet>();
                foreach (string[] dataSetLines in dataSetLinesList)
                {
                    dataSet = GetDatDataSet(dataSetNames, dataSetLines);
                    if (dataSet.FieldName != nameError) dataSets.Add(dataSet);
                }

                HistoryResults historyOutput = GetHistoryOutput(dataSets);
                AddVonMisesStressComponent(historyOutput);
                return historyOutput;
            }

            return null;
        }
        static private List<string[]> SplitToDataSetLinesList(List<string> dataSetNames, string[] lines)
        {
            // displacements (vx, vy, vz) for set DISP and time  0.1000000E+00
            //         5   5.080202E+00  0.000000E+00  0.000000E+00
            // forces (fx, fy, fz) for set FORCE and time  0.1000000E+00
            //         69 -2.678823E-05  6.029691E+01  1.673592E+01
            //         47  7.091853E-05  1.246206E+01  3.973111E+00
            //         48 -1.268813E-04 -5.285929E+01 -1.336375E+01
            // stresses (elem, integ.pnt.,sxx,syy,szz,sxy,sxz,syz) for set ELEMENTSET-1 and time  0.1000000E+01
            //         2030   1 -1.824212E-11 -1.000000E-01 -1.062898E-10 -6.614340E-11 -5.298272E-10  3.899325E-10
            //         2030   2 -2.853204E-09 -1.000000E-01 -3.195191E-11  1.074865E-09 -1.638492E-10 -3.539078E-10

            List<string> dataSet = new List<string>();
            List<string[]> dataSets = new List<string[]>();

            bool containsName;
            for (int i = 0; i < lines.Length; i++)
            {
                containsName = false;
                foreach (var name in dataSetNames)
                {
                    if (lines[i].ToLower().Trim().StartsWith(name.ToLower()))
                    {
                        containsName = true;
                        break;
                    }
                }
                if (containsName)
                {
                    dataSet = new List<string>();
                    dataSet.Add(lines[i]);
                    i++;

                    while (i < lines.Length && lines[i].Trim().Length == 0) i++;    // skip empty lines

                    while (i < lines.Length)
                    {
                        if (lines[i].Trim().Length == 0) break;                     // last line is empty
                        else dataSet.Add(lines[i]);
                        i++;
                    }

                    dataSets.Add(dataSet.ToArray());
                }
            }

            return dataSets;
        }
        static private void Repair(List<string[]> dataSetLinesList, List<string> dataSetNames)
        {
            foreach (var lines in dataSetLinesList)
            {
                if (lines.Length > 0)
                {
                    foreach (var name in dataSetNames)
                    {
                        if (lines[0].ToLower().Trim().StartsWith(name.ToLower()))
                        {
                            if (name == nameDisplacements)
                            {
                                //displacements (vx,vy,vz) for set NODESET-1 and time  0.1000000E+01
                                //       310 -2.462709E-03 -6.331758E-04 -4.384750E-05
                                lines[0] = lines[0].Replace("(vx,vy,vz)", "(Id,U1,U2,U3)");
                            }
                            else if (name == nameForces)
                            {
                                //forces (fx,fy,fz) for set NODESET-1 and time  0.1000000E+01
                                //       310 -2.582430E-13 -1.013333E-01  6.199805E-14
                                lines[0] = lines[0].Replace("(fx,fy,fz)", "(Id,RF1,RF2,RF3)");
                            }
                            else if (name == nameTotalForce)
                            {
                                //total force (fx,fy,fz) for set NODESET-1 and time  0.1000000E+01
                                //       -5.868470E-13 -1.000000E+00 -1.028019E-13
                                lines[0] = lines[0].Replace("(fx,fy,fz)", "(RF1,RF2,RF3)");
                            }
                            else if (name == nameStresses)
                            {
                                //stresses (elem, integ.pnt.,sxx,syy,szz,sxy,sxz,syz) for set SOLID_PART-1 and time  0.1000000E+01
                                //      1655   1  1.186531E-02 -3.997792E-02 -3.119545E-03  1.104426E-02  2.740127E-03 -9.467634E-03
                                lines[0] = lines[0].Replace("(elem, integ.pnt.,sxx,syy,szz,sxy,sxz,syz)",
                                                            "(Id,Int.Pnt.,S11,S22,S33,S12,S13,S23)");
                            }
                            else if (name == nameStrains)
                            {
                                //strains (elem, integ.pnt.,exx,eyy,ezz,exy,exz,eyz) forset SOLID_PART-1 and time  0.1000000E+01
                                //      1655   1  1.180693E-07 -2.028650E-07  2.530589E-08  6.836925E-08  1.696269E-08 -5.860917E-08
                                lines[0] = lines[0].Replace("(elem, integ.pnt.,exx,eyy,ezz,exy,exz,eyz)",
                                                            "(Id,Int.Pnt.,E11,E22,E33,E12,E13,E23)");
                                lines[0] = lines[0].Replace(" forset ", " for set ");
                            }
                            else if (name == nameMechanicalStrains)
                            {
                                // mechanical strains (elem, integ.pnt.,exx,eyy,ezz,exy,exz,eyz) forset ELEMENTSET-1 and time  0.1000000E+01
                                //      2030   1  1.428572E-07 -4.761905E-07  1.428571E-07 -1.951987E-14  2.100533E-15  1.625910E-15
                                lines[0] = lines[0].Replace("(elem, integ.pnt.,exx,eyy,ezz,exy,exz,eyz)",
                                                            "(Id,Int.Pnt.,E11,E22,E33,E12,E13,E23)");
                                lines[0] = lines[0].Replace(" forset ", " for set ");
                            }
                            else if (name == nameEquivalentPlasticStrains)
                            {
                                // equivalent plastic strain (elem, integ.pnt.,pe)for set ELEMENTSET-1 and time  0.6250000E-01
                                //      1682   1  0.000000E+00
                                lines[0] = lines[0].Replace("(elem, integ.pnt.,pe)",
                                                            "(Id,Int.Pnt.,PEEQ)");
                                lines[0] = lines[0].Replace(")for set ", ") for set ");
                            }
                            else if (name == nameInternalEnergyDensity)
                            {
                                // internal energy density (elem, integ.pnt.,eneset ELEMENTSET-1 and time  0.6250000E-01
                                //      3068   1  4.313000E-01
                                lines[0] = lines[0].Replace("(elem, integ.pnt.,eneset ",
                                                            "(Id,Int.Pnt.,ENER) for set ");
                            }
                            else if (name == nameInternalEnergy)
                            {
                                //internal energy (element, energy) for set SOLID_PART-1 and time  0.1000000E+01
                                //      1655  9.342906E-09
                                lines[0] = lines[0].Replace("(element, energy)", "(Id,ELSE)");
                            }
                            else if (name == nameTotalInternalEnergy)
                            {
                                //total internal energy for set SOLID_PART-1 and time  0.1000000E+01
                                //        3.249095E-04
                                lines[0] = lines[0].Replace("total internal energy for set",
                                                            "total internal energy (SE) for set");
                            }
                            else if (name == nameVolume)
                            {
                                //volume (element, volume) for set SOLID_PART-1 and time  0.1000000E+01
                                //      1655  1.538557E+00
                                lines[0] = lines[0].Replace("(element, volume)", "(Id,EVOL)");
                            }
                            else if (name == nameTotalVolume)
                            {
                                //total volume for set SOLID_PART-1 and time  0.1000000E+01
                                //        2.322033E+03
                                lines[0] = lines[0].Replace("total volume for set", "total volume (VOL) for set");
                            }
                        }
                    }
                    
                }
            }
        }
        static private string RepairSetName(string setName)
        {
            string[] tmp;
            if (setName.StartsWith(CaeMesh.Globals.InternalSelectionName.ToUpper()))
            {
                tmp = setName.Split(underscoreSplitter, StringSplitOptions.None);
                setName = "";
                for (int i = 2; i < tmp.Length; i++)
                {
                    setName += tmp[i];
                    if (i < tmp.Length - 1) setName += "_";
                }
            }
            else if (setName.StartsWith(CaeMesh.Globals.InternalName.ToUpper()))
            {
                tmp = setName.Split(underscoreSplitter, StringSplitOptions.None);
                setName = "";
                for (int i = 1; i < tmp.Length; i++)
                {
                    setName += tmp[i];
                    if (i < tmp.Length - 1) setName += "_";
                }
            }
            return setName;
        }
        static private DatDataSet GetDatDataSet(List<string> dataSetNames, string[] dataSetLines)
        {
            try
            {
                // displacements (vx, vy, vz) for set DISP and time  0.1000000E+00
                List<string> componentNames = new List<string>();
                DatDataSet dataSet = new DatDataSet();

                string firstLine = dataSetLines[0];
                foreach (var name in dataSetNames)
                {
                    if (firstLine.ToLower().Trim().StartsWith(name.ToLower()))
                    {
                        dataSet.FieldName = name;
                        break;
                    }
                }

                string[] tmp = firstLine.Split(parenthesesSplitter, StringSplitOptions.RemoveEmptyEntries);

                string[] tmp2 = tmp[1].Split(componentsSplitter, StringSplitOptions.RemoveEmptyEntries);
                componentNames = tmp2.ToList();

                tmp2 = tmp[2].Split(dataSplitter, StringSplitOptions.RemoveEmptyEntries);
                dataSet.SetName = RepairSetName(tmp2[0].Trim());
                dataSet.Time = double.Parse(tmp2[1]);
                //
                double[] values;
                List<double[]> allValues = new List<double[]>();
                for (int i = 1; i < dataSetLines.Length; i++)
                {
                    tmp = dataSetLines[i].Split(spaceSplitter, StringSplitOptions.RemoveEmptyEntries);
                    values = new double[tmp.Length];

                    for (int j = 0; j < tmp.Length; j++) values[j] = double.Parse(tmp[j]);

                    allValues.Add(values);
                }
                dataSet.Values = allValues.ToArray();

                dataSet.ComponentNames = componentNames.ToArray();

                return dataSet;
            }
            catch
            {
                return new DatDataSet() { FieldName = nameError };
            }
        }
        static private HistoryResults GetHistoryOutput(List<DatDataSet> dataSets)
        {
            HistoryResults historyOutput = new HistoryResults("HistoryOutput");
            HistoryResultSet set;
            HistoryResultField field;
            HistoryResultComponent component;
            HistoryResultEntries entries;
            //
            DatDataSet repairedDataSet;
            int offset;
            string valueId;
            string id;
            double time;
            double[] values;
            //
            foreach (var dataSet in dataSets)
            {
                repairedDataSet = RepairReferencePointDataSet(dataSet);
                //
                time = repairedDataSet.Time;
                // Get or create a set
                if (!historyOutput.Sets.TryGetValue(repairedDataSet.SetName, out set))
                {
                    set = new HistoryResultSet(repairedDataSet.SetName);
                    historyOutput.Sets.Add(set.Name, set);
                }
                // Get or create a field
                if (!set.Fields.TryGetValue(repairedDataSet.FieldName, out field))
                {
                    field = new HistoryResultField(repairedDataSet.FieldName);
                    set.Fields.Add(field.Name, field);
                    // Add MISES component
                    if (field.Name == nameStresses)
                    {
                        component = new HistoryResultComponent("MISES");
                        field.Components.Add(component.Name, component);
                    }
                }
                // For each value line in data set: id x y z
                for (int i = 0; i < repairedDataSet.Values.Length; i++)
                {
                    values = repairedDataSet.Values[i];
                    //
                    if (repairedDataSet.ComponentNames.Length > 0 && repairedDataSet.ComponentNames[0] == "Id")
                    {
                        // The first column is id column
                        if (repairedDataSet.ComponentNames[1] == "Int.Pnt.")
                        {
                            // The second column in In.Pnt. column
                            valueId = values[0].ToString() + "_" + values[1].ToString();
                            offset = 2;
                        }
                        else
                        {
                            valueId = values[0].ToString();
                            offset = 1;
                        }
                    }
                    // There is no id
                    else
                    {
                        valueId = null;
                        offset = 0;
                    }                    
                    //                                                                                  
                    // For ecah component
                    for (int j = 0; j < values.Length - offset; j++)
                    {
                        // Get or create a component
                        if (!field.Components.TryGetValue(repairedDataSet.ComponentNames[j + offset], out component))
                        {
                            component = new HistoryResultComponent(repairedDataSet.ComponentNames[j + offset]);
                            field.Components.Add(component.Name, component);
                        }
                        // For the case of total forces
                        if (valueId == null) id = component.Name;
                        else id = valueId;
                        // Get or create historyValues as component entries
                        if (!component.Entries.TryGetValue(id, out entries))
                        {
                            entries = new HistoryResultEntries(id);
                            component.Entries.Add(entries.Name, entries);
                        }
                        //
                        entries.Time.Add(time);
                        entries.Values.Add(values[j + offset]);
                    }
                }
            }

            return historyOutput;
        }
        static private DatDataSet RepairReferencePointDataSet(DatDataSet dataSet)
        {
            string setName = dataSet.SetName;
            string[] tmp;
            // Ref node
            tmp = setName.ToUpper().Split(new string[] { FeReferencePoint.RefName.ToUpper() }, 
                                          StringSplitOptions.RemoveEmptyEntries);
            if (tmp.Length == 2) dataSet.SetName = tmp[0];
            // Rot node
            tmp = setName.ToUpper().Split(new string[] { FeReferencePoint.RotName.ToUpper() },
                                          StringSplitOptions.RemoveEmptyEntries);
            if (tmp.Length == 2)
            {
                dataSet.SetName = tmp[0];
                for (int i = 0; i < dataSet.ComponentNames.Length; i++)
                {
                    dataSet.ComponentNames[i] = compMap[dataSet.ComponentNames[i]];
                }
            }
            //
            return dataSet;
        }
        //
        static private void AddVonMisesStressComponent(HistoryResults historyOutput)
        {
            foreach (var setsEntry in historyOutput.Sets)
            {
                foreach (var fieldEntry in setsEntry.Value.Fields)
                {
                    if (fieldEntry.Key == nameStresses)
                    {
                        HistoryResultComponent vonMisesCom = fieldEntry.Value.Components["MISES"];
                        HistoryResultComponent sgnMaxAbsPrinCom = new HistoryResultComponent("SGN-MAX-ABS-PRI");
                        HistoryResultComponent prinMaxCom = new HistoryResultComponent("PRINCIPAL-MAX");
                        HistoryResultComponent prinMidCom = new HistoryResultComponent("PRINCIPAL-MID");
                        HistoryResultComponent prinMinCom = new HistoryResultComponent("PRINCIPAL-MIN");
                        //
                        string[] entryNames = fieldEntry.Value.Components["S11"].Entries.Keys.ToArray();
                        double[][] values = new double[6][];
                        double[] vmArray;
                        double[] sgnMaxAbsPrinArray;
                        double[] prinMaxArray;
                        double[] prinMidArray;
                        double[] prinMinArray;
                        HistoryResultEntries hrEntries;
                        //
                        double s11;
                        double s22;
                        double s33;
                        double s12;
                        double s23;
                        double s31;
                        double I1;
                        double I2;
                        double I3;
                        double sp1, sp2, sp3;                        
                        //
                        foreach (var entryName in entryNames)
                        {
                            values[0] = fieldEntry.Value.Components["S11"].Entries[entryName].Values.ToArray();
                            values[1] = fieldEntry.Value.Components["S22"].Entries[entryName].Values.ToArray();
                            values[2] = fieldEntry.Value.Components["S33"].Entries[entryName].Values.ToArray();
                            values[3] = fieldEntry.Value.Components["S12"].Entries[entryName].Values.ToArray();
                            values[4] = fieldEntry.Value.Components["S23"].Entries[entryName].Values.ToArray();
                            values[5] = fieldEntry.Value.Components["S13"].Entries[entryName].Values.ToArray();
                            //
                            vmArray = new double[values[0].Length];
                            sgnMaxAbsPrinArray = new double[values[0].Length];
                            prinMaxArray = new double[values[0].Length];
                            prinMidArray = new double[values[0].Length];
                            prinMinArray = new double[values[0].Length];
                            //
                            for (int i = 0; i < vmArray.Length; i++)
                            {
                                vmArray[i] = Math.Sqrt(0.5 * (
                                                               Math.Pow(values[0][i] - values[1][i], 2)
                                                             + Math.Pow(values[1][i] - values[2][i], 2)
                                                             + Math.Pow(values[2][i] - values[0][i], 2)
                                                             + 6 * (
                                                                      Math.Pow(values[3][i], 2)
                                                                    + Math.Pow(values[4][i], 2)
                                                                    + Math.Pow(values[5][i], 2)
                                                                   )
                                                            )
                                                     );
                                //
                                s11 = values[0][i];
                                s22 = values[1][i];
                                s33 = values[2][i];
                                s12 = values[3][i];
                                s23 = values[4][i];
                                s31 = values[5][i];
                                //
                                I1 = s11 + s22 + s33;
                                I2 = s11 * s22 + s22 * s33 + s33 * s11 -
                                     Math.Pow(s12, 2.0) - Math.Pow(s23, 2.0) - Math.Pow(s31, 2.0);
                                I3 = s11 * s22 * s33 - s11 * Math.Pow(s23, 2.0) -
                                     s22 * Math.Pow(s31, 2.0) - s33 * Math.Pow(s12, 2.0) + 2.0 * s12 * s23 * s31;
                                //
                                sp1 = sp2 = sp3 = 0;
                                Tools.SolveQubicEquationDepressedCubic(1.0, -I1, I2, -I3, ref sp1, ref sp2, ref sp3);
                                Tools.Sort3_descending(ref sp1, ref sp2, ref sp3);
                                //
                                sgnMaxAbsPrinArray[i] = Math.Abs(sp1) > Math.Abs(sp3) ? (float)sp1 : (float)sp3;
                                prinMaxArray[i] = sp1;
                                prinMidArray[i] = sp2;
                                prinMinArray[i] = sp3;
                                //
                                if (double.IsNaN(sgnMaxAbsPrinArray[i])) sgnMaxAbsPrinArray[i] = 0;
                                if (double.IsNaN(prinMaxArray[i])) prinMaxArray[i] = 0;
                                if (double.IsNaN(prinMidArray[i])) prinMidArray[i] = 0;
                                if (double.IsNaN(prinMinArray[i])) prinMinArray[i] = 0;
                            }
                            //
                            hrEntries = new HistoryResultEntries(entryName);
                            hrEntries.Time = fieldEntry.Value.Components["S11"].Entries[entryName].Time;
                            hrEntries.Values = vmArray.ToList();
                            vonMisesCom.Entries.Add(entryName, hrEntries);
                            //
                            hrEntries = new HistoryResultEntries(entryName);
                            hrEntries.Time = fieldEntry.Value.Components["S11"].Entries[entryName].Time;
                            hrEntries.Values = sgnMaxAbsPrinArray.ToList();
                            sgnMaxAbsPrinCom.Entries.Add(entryName, hrEntries);
                            //
                            hrEntries = new HistoryResultEntries(entryName);
                            hrEntries.Time = fieldEntry.Value.Components["S11"].Entries[entryName].Time;
                            hrEntries.Values = prinMaxArray.ToList();
                            prinMaxCom.Entries.Add(entryName, hrEntries);
                            //
                            hrEntries = new HistoryResultEntries(entryName);
                            hrEntries.Time = fieldEntry.Value.Components["S11"].Entries[entryName].Time;
                            hrEntries.Values = prinMidArray.ToList();
                            prinMidCom.Entries.Add(entryName, hrEntries);
                            //
                            hrEntries = new HistoryResultEntries(entryName);
                            hrEntries.Time = fieldEntry.Value.Components["S11"].Entries[entryName].Time;
                            hrEntries.Values = prinMinArray.ToList();
                            prinMinCom.Entries.Add(entryName, hrEntries);
                        }
                        //
                        fieldEntry.Value.Components["MISES"] = vonMisesCom;
                        fieldEntry.Value.Components.Add(sgnMaxAbsPrinCom.Name, sgnMaxAbsPrinCom);
                        fieldEntry.Value.Components.Add(prinMaxCom.Name, prinMaxCom);
                        fieldEntry.Value.Components.Add(prinMidCom.Name, prinMidCom);
                        fieldEntry.Value.Components.Add(prinMinCom.Name, prinMinCom);
                    }
                }
            }
        }

     

    }
}
