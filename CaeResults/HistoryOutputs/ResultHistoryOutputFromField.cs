﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;
using CaeGlobals;

namespace CaeResults
{
    [Serializable]
    public class ResultHistoryOutputFromField : ResultHistoryOutput
    {
        // Variables                                                                                                                
        private string _fieldName;
        private string _componentName;
        private ComplexResultTypeEnum _complexResultType;
        private double _complexAngleDeg;    // must be double
        private int _stepId;
        private int _stepIncrementId;


        // Properties                                                                                                               
        public string FieldName { get { return _fieldName; } set { _fieldName = value; } }
        public string ComponentName { get { return _componentName; } set { _componentName = value; } }
        public ComplexResultTypeEnum ComplexResultType { get { return _complexResultType; } set { _complexResultType = value; } }
        public double ComplexAngleDeg { get { return _complexAngleDeg; } set { _complexAngleDeg = value; } }
        public int StepId { get { return _stepId; } set { _stepId = value; } }
        public int StepIncrementId { get { return _stepIncrementId; } set { _stepIncrementId = value; } }


        // Constructors                                                                                                             
        public ResultHistoryOutputFromField(string name, string filedName, string componentName,
                                            string regionName, RegionTypeEnum regionType)
            : base(name, regionName, regionType)
        {
            _fieldName = filedName;
            _componentName = componentName;
            _complexResultType = ComplexResultTypeEnum.Real;
            _stepId = -1;
            _stepIncrementId = -1;
        }


        // Methods                                                                                                                  
    }
}
