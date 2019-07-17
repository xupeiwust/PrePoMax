﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Kitware.VTK;

namespace vtkControl
{
    

    class vtkMaxStatusBlockWidget : vtkMaxTextWidget
    {
        // Variables                                                                                                                
        private string _text;
        private string _name;
        private DateTime _dateTime;
        private float _analysisTime;
        private float _animationScaleFactor;
        private float _deformationScaleFactor;
        private DataFieldType _fieldType;


        // Properties                                                                                                               
        public string Name { get { return _name; } set { _name = value; SetText(); } }
        public DateTime DateTime { get { return _dateTime; } set { _dateTime = value; SetText(); } }
        public float AnalysisTime { get { return _analysisTime; } set { _analysisTime = value; SetText(); } }
        public float DeformationScaleFactor { get { return _deformationScaleFactor; } set { _deformationScaleFactor = value; SetText(); } }
        public float AnimationScaleFactor { get { return _animationScaleFactor; } set { _animationScaleFactor = value; SetText(); } }
        public DataFieldType FieldType { get { return _fieldType; } set { _fieldType = value; SetText(); } }


        // Constructors                                                                                                             
        public vtkMaxStatusBlockWidget()
        {
            _text = "text";
            _name = "name";
            _dateTime = DateTime.Now;
            _deformationScaleFactor = 1;
            _animationScaleFactor = -1;

            // Text property
            vtkTextProperty textProperty = vtkTextProperty.New();
            textProperty.SetFontFamilyToArial();
            textProperty.SetFontSize(16);
            textProperty.SetColor(0, 0, 0);
            textProperty.SetLineOffset(-Math.Round(textProperty.GetFontSize() / 5.0));
            textProperty.SetLineSpacing(1.2);
            this.SetTextProperty(textProperty);
        }


        // Private methods                                                                                                          
        private void SetText()
        {
            string sysUIFormat = System.Globalization.CultureInfo.CurrentUICulture.DateTimeFormat.ShortDatePattern;

            _text = "Name: " + _name + "   ";
            _text += "Date: " + _dateTime.ToString(sysUIFormat) + "   Time: " + _dateTime.ToString("HH:mm:ss") + Environment.NewLine;

            if (_fieldType == DataFieldType.Static) _text += "Step: Static   Analysis time: " + _analysisTime.ToString();
            else if (_fieldType == DataFieldType.Frequency) _text += "Step: Frequency   Eigenfrequency: " + _analysisTime.ToString();
            else if (_fieldType == DataFieldType.Buckling) _text += "Step: Buckling   Buckling factor: " + _analysisTime.ToString();
           
            _text += Environment.NewLine;
            _text += "Deformation scale factor: " + _deformationScaleFactor.ToString();

            if (_animationScaleFactor >= 0)
            {
                _text += Environment.NewLine;
                _text += "Animation scale factor: " + _animationScaleFactor.ToString();
            }

            this.SetText(_text);
        }


        // Public methods                                                                                                           

    }
}