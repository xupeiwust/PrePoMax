﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization;


namespace CaeModel
{
    [Serializable]
    public class HeatTransferStep : StaticStep, ISerializable
    {
        // Variables                                                                                                                
        private bool _steadyState;                  //ISerializable
        private double _deltmx;                     //ISerializable


        // Properties                                                                                                               
        public bool SteadyState { get { return _steadyState; } set { _steadyState = value; } }
        public double Deltmx { get { return _deltmx; } set { _deltmx = value; } }


        // Constructors                                                                                                             
        public HeatTransferStep(string name)
            :base(name, false)
        {
            _steadyState = false;
            _deltmx = double.PositiveInfinity;
            //
            AddFieldOutput(new NodalFieldOutput("NF-Output-1", NodalFieldVariable.NT));
        }
        //ISerializable
        public HeatTransferStep(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            foreach (SerializationEntry entry in info)
            {
                switch (entry.Name)
                {
                    case "_steadyState":
                        _steadyState = (bool)entry.Value; break;
                    case "_deltmx":
                        _deltmx = (double)entry.Value; break;
                    default:
                        break;
                }
            }
        }


        // Methods                                                                                                                  
        public override bool IsBoundaryConditionSupported(BoundaryCondition boundaryCondition)
        {
            if (boundaryCondition is FixedBC || boundaryCondition is DisplacementRotation || boundaryCondition is SubmodelBC)
                return false;
            else if (boundaryCondition is TemperatureBC)
                return true;
            else throw new NotSupportedException();
        }
        public override bool IsLoadSupported(Load load)
        {
            if (load is CLoad || load is MomentLoad || load is DLoad || load is STLoad || load is ShellEdgeLoad ||
                load is GravityLoad || load is CentrifLoad || load is PreTensionLoad)
                return false;
            else if (load is RadiateLoad)
                return true;
            else throw new NotSupportedException();
        }
        public override bool IsDefinedFieldSupported(DefinedField definedField)
        {
            if (definedField is DefinedTemperature) return false;
            else throw new NotSupportedException();
        }
        // ISerialization
        public new void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            // using typeof() works also for null fields
            base.GetObjectData(info, context);
            //
            info.AddValue("_steadyState", _steadyState, typeof(bool));
            info.AddValue("_deltmx", _deltmx, typeof(double));
        }
    }
}
